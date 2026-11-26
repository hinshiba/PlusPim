;;; pluspim-dap.el --- Debug MIPS programs with PlusPim through dape  -*- lexical-binding: t; -*-

;; Copyright (C) 2026 hinshiba

;; This file is not part of GNU Emacs.

;; SPDX-License-Identifier: MIT

;;; Commentary:

;; Registers the `pluspim' entry of `dape-configs'.
;;
;; PlusPim wires the debuggee's stdin and stdout to the terminal it
;; runs in, so DAP cannot share them and goes over TCP instead.  The
;; entry's `fn' starts "pluspim debug listen 127.0.0.1:0" in the comint
;; buffer `pluspim-dap-buffer-name', reads the address that PlusPim
;; announces there, and hands it to dape as `host' and `port'.  The
;; files go to PlusPim as the `program' of `launch'.

;;; Code:

(require 'comint)
(require 'dape)
(require 'pluspim)

(defcustom pluspim-dap-buffer-name "*pluspim*"
  "Buffer that runs PlusPim and holds the debuggee's input and output."
  :type 'string
  :group 'pluspim)

(defcustom pluspim-dap-timeout 5
  "Seconds to wait for PlusPim to announce its DAP address."
  :type 'number
  :group 'pluspim)

(defun pluspim-dap--buffer-file ()
  "Return the file of the current buffer as the program to debug."
  (or buffer-file-name (user-error "Buffer %s visits no file" (buffer-name))))

(defun pluspim-dap--start (config)
  "Start PlusPim and return CONFIG with its address and `:program'.
CONFIG's `program' is a file name, or a list or vector of them, relative to
`default-directory'.  The previous PlusPim buffer is killed first so that its
process cannot hold the debuggee."
  (let ((files (let ((program (dape-config-get config 'program)))
                 (if (stringp program) (list program) (append program nil))))
        (kill-buffer-query-functions nil))
    (when-let* ((old (get-buffer pluspim-dap-buffer-name)))
      (kill-buffer old))
    (let* ((buffer (make-comint-in-buffer "pluspim"
                          pluspim-dap-buffer-name (pluspim-executable 'dap) nil
                          "debug" "listen" "127.0.0.1:0"))
           (proc (get-buffer-process buffer)))
      ;; The debuggee writes raw bytes; decoding them here replaces
      ;; --force-terminal-utf8, which only switches a console code page.
      (set-process-coding-system proc 'utf-8-dos 'utf-8-unix)
      (display-buffer buffer)
      (with-current-buffer buffer
        (goto-char (point-min))
        (while (not (re-search-forward
                     "^DAP server listening at: \\(.+\\):\\([0-9]+\\)\n" nil t))
          (unless (accept-process-output proc pluspim-dap-timeout)
            (user-error "PlusPim did not announce its DAP address; see %s"
                        pluspim-dap-buffer-name))
          (goto-char (point-min)))
        (let ((host (match-string-no-properties 1))
              (port (string-to-number (match-string-no-properties 2))))
          ;; PlusPim resolves a relative path only against `cwd', so send absolute ones.
          (plist-put (plist-put (plist-put config 'host host) 'port port)
                     :program (vconcat (mapcar #'expand-file-name files))))))))

(add-to-list 'dape-configs
             '(pluspim
               modes (pluspim-mips-mode)
               fn pluspim-dap--start
               program pluspim-dap--buffer-file
               :type "pluspim"
               :request "launch"
               :stopOnEntry t))

(provide 'pluspim-dap)
;;; pluspim-dap.el ends here

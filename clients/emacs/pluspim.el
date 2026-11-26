;;; pluspim.el --- Emacs client for the PlusPim MIPS debugger  -*- lexical-binding: t; -*-

;; Copyright (C) 2026 hinshiba

;; Author: hinshiba
;; URL: https://github.com/hinshiba/PlusPim
;; Version: 0.1.0
;; Package-Requires: ((emacs "29.1") (dape "0.28.0"))
;; Keywords: languages, tools, debugger

;; This file is not part of GNU Emacs.

;; SPDX-License-Identifier: MIT

;;; Commentary:

;; Editor integration for PlusPim, a MIPS emulator and debugger.
;;
;; This file holds what every PlusPim feature shares: the table of
;; executables keyed by purpose, the resolver over that table, and
;; `pluspim-mips-mode' for `.asm' and `.s' files.  Each purpose lives in
;; its own file, loaded once its client package is loaded:
;;
;; - pluspim-dap.el registers the `pluspim' configuration for dape.
;;
;; Debug the current file with M-x dape RET pluspim RET.

;;; Code:

(defgroup pluspim nil
  "Client for the PlusPim MIPS debugger."
  :group 'tools
  :prefix "pluspim-")

(defconst pluspim-tools '((dap . "pluspim"))
  "Executable name of each PlusPim tool, keyed by purpose.")

(defcustom pluspim-executable-paths nil
  "Explicit executable paths keyed by a purpose from `pluspim-tools'.
A purpose missing here is looked up on `exec-path'."
  :type '(alist :key-type (symbol :tag "Purpose") :value-type file))

(defun pluspim-executable (purpose)
  "Return the executable that serves PURPOSE, a key of `pluspim-tools'.
Try `pluspim-executable-paths' first, then `exec-path'."
  (let ((path (alist-get purpose pluspim-executable-paths))
        (name (alist-get purpose pluspim-tools)))
    (cond ((and path (file-executable-p path)) path)
          ((executable-find name))
          ((user-error "PlusPim %s executable not found in %s (%s) or PATH (%s)"
                       purpose 'pluspim-executable-paths path name)))))

;;;###autoload
(define-derived-mode pluspim-mips-mode asm-mode "MIPS"
  "Major mode for MIPS assembly as PlusPim reads it."
  (setq-local asm-comment-char ?#))

;;;###autoload
(add-to-list 'auto-mode-alist '("\\.\\(?:asm\\|s\\)\\'" . pluspim-mips-mode))

(provide 'pluspim)

;; After `provide', so that pluspim-dap's own require of us is a no-op.
;;;###autoload
(with-eval-after-load 'dape (require 'pluspim-dap))

;;; pluspim.el ends here

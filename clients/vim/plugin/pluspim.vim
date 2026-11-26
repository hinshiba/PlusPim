vim9script
# PlusPim commands. The work lives in autoload/ so startup stays cheap.

import autoload 'pluspim/dap.vim'

# :PlusPimDebug [file]...  Debugs the files, or the current file when none are given.
command -nargs=* -complete=file PlusPimDebug dap.Start([<f-args>])

vim9script
# Vim's own detection already maps .asm and .s to other assemblers, so override it instead of :setfiletype.
autocmd BufRead,BufNewFile *.asm,*.s set filetype=mips

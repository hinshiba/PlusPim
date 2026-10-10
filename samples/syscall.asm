.data
msg:
    .asciiz "Hello PlusPim!"

.text
main:
    la $a0, msg
    li $v0, 4
    syscall
    nop
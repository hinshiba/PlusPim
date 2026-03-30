.data
msg:
    .asciiz "Hello PlusPim!"


.text
main:
    move $a0, $a0
    la $a0, msg
    li $v0, 4
    syscall
    nop
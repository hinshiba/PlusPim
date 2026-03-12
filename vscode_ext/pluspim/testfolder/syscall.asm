.data
msg:
    .asciiz "Hello PlusPim!"


.text
main:
    la $a0, msg
    li $v0, 4
    syscall
    add $t0, $zero, $zero
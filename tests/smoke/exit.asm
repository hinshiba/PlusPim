# main から戻るだけのプログラム
.text
    .globl main
main:
    addiu   $a0, $zero, 42
    jr      $ra
    nop

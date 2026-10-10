# 1からNまでの総和を求める
.data
msg:
    .asciiz "sum(1..10) = "
endl:
    .asciiz "\n"

.text
    # ================================ MARK: sum
sum:
    # 1からnまでの総和を求める
    # args
    #   $a0: n
    # ret
    #   $v0: 1 + 2 + ... + n

    # -- 実装 --
    move    $v0, $zero              # acc = 0
    li      $t0, 1                  # i = 1
sum_loop:
    slt     $t1, $a0, $t0           # n < i なら終了
    bne     $t1, $zero, sum_ret
    addu    $v0, $v0, $t0
    addiu   $t0, $t0, 1
    j       sum_loop
sum_ret:
    jr      $ra
    # ================================ end: sum

print_int:
    # intを出力する
    # args
    #   $a0: 出力する値
    li      $v0, 1
    syscall
    jr      $ra

print_string:
    # 文字列を出力する
    # args
    #   $a0: 文字列の先頭アドレス
    li      $v0, 4
    syscall
    jr      $ra

    # ================================ MARK: main
main:
    # -- 退避 --
    addiu   $sp, $sp, -8
    sw      $ra, 4($sp)
    # -- 実装 --
    la      $a0, msg
    jal     print_string

    li      $a0, 10
    jal     sum
    move    $a0, $v0
    jal     print_int

    la      $a0, endl
    jal     print_string
    # -- 復元 --
    lw      $ra, 4($sp)
    addiu   $sp, $sp, 8
    jr      $ra
    # ================================ end: main

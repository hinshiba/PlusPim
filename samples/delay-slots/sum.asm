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
    blez    $a0, sum_ret            # n < 1 なら0
    move    $v0, $zero              # acc = 0
    li      $t0, 1                  # i = 1
sum_loop:
    addu    $v0, $v0, $t0
    slt     $t1, $t0, $a0           # i < n なら続ける
    bne     $t1, $zero, sum_loop
    addiu   $t0, $t0, 1
sum_ret:
    jr      $ra
    nop
    # ================================ end: sum

print_int:
    # intを出力する
    # args
    #   $a0: 出力する値
    li      $v0, 1
    syscall
    jr      $ra
    nop

print_string:
    # 文字列を出力する
    # args
    #   $a0: 文字列の先頭アドレス
    li      $v0, 4
    syscall
    jr      $ra
    nop

    # ================================ MARK: main
    .globl main
main:
    # -- 退避 --
    addiu   $sp, $sp, -8
    sw      $ra, 4($sp)
    # -- 実装 --
    la      $a0, msg
    jal     print_string
    nop

    jal     sum
    addiu   $a0, $zero, 10
    jal     print_int
    move    $a0, $v0

    # -- 復元 --
    lw      $ra, 4($sp)
    la      $a0, endl
    j       print_string            # print_stringからmainの呼び出し元へ直接戻る
    addiu   $sp, $sp, 8
    # ================================ end: main

# .word で確保した配列を走査して合計と最大値を求める
.data
arr:
    .word   5, 3, 9, 1, 7, 2, 8
arr_len:
    .word   7
msg_sum:
    .asciiz "sum = "
msg_max:
    .asciiz "max = "
endl:
    .asciiz "\n"

.text
    # ================================ MARK: array_sum
array_sum:
    # 配列の合計を求める
    # args
    #   $a0: 配列の先頭アドレス
    #   $a1: 要素数
    # ret
    #   $v0: 合計

    # -- 実装 --
    move    $v0, $zero              # acc = 0
    move    $t0, $zero              # i = 0
as_loop:
    beq     $t0, $a1, as_ret
    sll     $t1, $t0, 2             # i * 4
    addu    $t1, $a0, $t1           # &arr[i]
    lw      $t2, 0($t1)
    addu    $v0, $v0, $t2
    addiu   $t0, $t0, 1
    j       as_loop
as_ret:
    jr      $ra
    # ================================ end: array_sum

    # ================================ MARK: array_max
array_max:
    # 配列の最大値を求める．要素数は1以上を仮定する
    # args
    #   $a0: 配列の先頭アドレス
    #   $a1: 要素数
    # ret
    #   $v0: 最大値

    # -- 実装 --
    lw      $v0, 0($a0)             # max = arr[0]
    li      $t0, 1                  # i = 1
am_loop:
    beq     $t0, $a1, am_ret
    sll     $t1, $t0, 2
    addu    $t1, $a0, $t1
    lw      $t2, 0($t1)
    slt     $t3, $v0, $t2           # max < arr[i] ?
    beq     $t3, $zero, am_skip
    move    $v0, $t2
am_skip:
    addiu   $t0, $t0, 1
    j       am_loop
am_ret:
    jr      $ra
    # ================================ end: array_max

print_int:
    li      $v0, 1
    syscall
    jr      $ra

print_string:
    li      $v0, 4
    syscall
    jr      $ra

    # ================================ MARK: main
main:
    # -- 退避 --
    addiu   $sp, $sp, -16
    sw      $ra, 12($sp)
    sw      $s0, 8($sp)
    sw      $s1, 4($sp)
    # -- 実装 --
    la      $s0, arr                # 配列の先頭
    la      $t0, arr_len
    lw      $s1, 0($t0)             # 要素数

    la      $a0, msg_sum
    jal     print_string
    move    $a0, $s0
    move    $a1, $s1
    jal     array_sum               # 35
    move    $a0, $v0
    jal     print_int
    la      $a0, endl
    jal     print_string

    la      $a0, msg_max
    jal     print_string
    move    $a0, $s0
    move    $a1, $s1
    jal     array_max               # 9
    move    $a0, $v0
    jal     print_int
    la      $a0, endl
    jal     print_string
    # -- 復元 --
    lw      $s1, 4($sp)
    lw      $s0, 8($sp)
    lw      $ra, 12($sp)
    addiu   $sp, $sp, 16
    jr      $ra
    # ================================ end: main

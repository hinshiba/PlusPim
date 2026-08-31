# バブルソートで配列を昇順に並べ替える
.data
arr:
    .word   5, 3, 9, 1, 7, 2, 8
arr_len:
    .word   7
msg_before:
    .asciiz "before: "
msg_after:
    .asciiz "after : "
sep:
    .asciiz " "
endl:
    .asciiz "\n"

.text
    # ================================ MARK: bubble_sort
bubble_sort:
    # 配列を昇順に並べ替える
    # args
    #   $a0: 配列の先頭アドレス
    #   $a1: 要素数

    # -- 実装 --
    move    $t0, $zero              # i = 0
    addiu   $t1, $a1, -1            # n - 1
bs_outer:
    slt     $t2, $t0, $t1           # i < n-1 ?
    beq     $t2, $zero, bs_ret

    move    $t3, $zero              # j = 0
    subu    $t4, $t1, $t0           # n - 1 - i
bs_inner:
    slt     $t5, $t3, $t4           # j < n-1-i ?
    beq     $t5, $zero, bs_next

    sll     $t6, $t3, 2
    addu    $t6, $a0, $t6           # &arr[j]
    lw      $t7, 0($t6)             # arr[j]
    lw      $t8, 4($t6)             # arr[j+1]

    slt     $t9, $t8, $t7           # arr[j+1] < arr[j] なら交換
    beq     $t9, $zero, bs_noswap
    sw      $t8, 0($t6)
    sw      $t7, 4($t6)

bs_noswap:
    addiu   $t3, $t3, 1
    j       bs_inner

bs_next:
    addiu   $t0, $t0, 1
    j       bs_outer

bs_ret:
    jr      $ra
    # ================================ end: bubble_sort

    # ================================ MARK: print_array
print_array:
    # 配列を空白区切りで出力する
    # args
    #   $a0: 配列の先頭アドレス
    #   $a1: 要素数

    # -- 退避 --
    addiu   $sp, $sp, -16
    sw      $ra, 12($sp)
    sw      $s0, 8($sp)
    sw      $s1, 4($sp)
    # -- 実装 --
    move    $s0, $a0                # 現在位置
    move    $s1, $a1                # 残り要素数
pa_loop:
    beq     $s1, $zero, pa_end
    lw      $a0, 0($s0)
    jal     print_int
    la      $a0, sep
    jal     print_string
    addiu   $s0, $s0, 4
    addiu   $s1, $s1, -1
    j       pa_loop
pa_end:
    la      $a0, endl
    jal     print_string
    # -- 復元 --
    lw      $s1, 4($sp)
    lw      $s0, 8($sp)
    lw      $ra, 12($sp)
    addiu   $sp, $sp, 16
    jr      $ra
    # ================================ end: print_array

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
    la      $s0, arr
    la      $t0, arr_len
    lw      $s1, 0($t0)

    la      $a0, msg_before
    jal     print_string
    move    $a0, $s0
    move    $a1, $s1
    jal     print_array             # 5 3 9 1 7 2 8

    move    $a0, $s0
    move    $a1, $s1
    jal     bubble_sort

    la      $a0, msg_after
    jal     print_string
    move    $a0, $s0
    move    $a1, $s1
    jal     print_array             # 1 2 3 5 7 8 9
    # -- 復元 --
    lw      $s1, 4($sp)
    lw      $s0, 8($sp)
    lw      $ra, 12($sp)
    addiu   $sp, $sp, 16
    jr      $ra
    # ================================ end: main

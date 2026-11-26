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
    slt     $t5, $t3, $t4           # j < n-1-i ?

bs_inner:
    beq     $t5, $zero, bs_next
    sll     $t6, $t3, 2
    addu    $t6, $a0, $t6           # &arr[j]
    lw      $t7, 0($t6)             # arr[j]
    lw      $t8, 4($t6)             # arr[j+1]
    addiu   $t3, $t3, 1             # j++

    slt     $t9, $t8, $t7           # arr[j+1] < arr[j] なら交換
    beq     $t9, $zero, bs_inner
    slt     $t5, $t3, $t4           # 次の j < n-1-i ?
    sw      $t8, 0($t6)
    j       bs_inner
    sw      $t7, 4($t6)

bs_next:
    j       bs_outer
    addiu   $t0, $t0, 1

bs_ret:
    jr      $ra
    nop
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
    beq     $a1, $zero, pa_end
    move    $s1, $a1                # 残り要素数
pa_loop:
    lw      $a0, 0($s0)
    jal     print_int
    addiu   $s0, $s0, 4
    la      $a0, sep
    jal     print_string
    addiu   $s1, $s1, -1
    bne     $s1, $zero, pa_loop
    nop
pa_end:
    # -- 復元 --
    lw      $s1, 4($sp)
    lw      $s0, 8($sp)
    lw      $ra, 12($sp)
    la      $a0, endl
    j       print_string            # print_stringからprint_arrayの呼び出し元へ直接戻る
    addiu   $sp, $sp, 16
    # ================================ end: print_array

print_int:
    li      $v0, 1
    syscall
    jr      $ra
    nop

print_string:
    li      $v0, 4
    syscall
    jr      $ra
    nop

    # ================================ MARK: main
    .globl main
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
    nop
    move    $a0, $s0
    jal     print_array             # 5 3 9 1 7 2 8
    move    $a1, $s1

    move    $a0, $s0
    jal     bubble_sort
    move    $a1, $s1

    la      $a0, msg_after
    jal     print_string
    nop
    move    $a0, $s0
    move    $a1, $s1
    # -- 復元 --
    lw      $s1, 4($sp)
    lw      $s0, 8($sp)
    lw      $ra, 12($sp)
    j       print_array             # 1 2 3 5 7 8 9．print_arrayからmainの呼び出し元へ直接戻る
    addiu   $sp, $sp, 16
    # ================================ end: main

# 再帰でフィボナッチ数を求める
.data
msg:
    .asciiz "fib(10) = "
endl:
    .asciiz "\n"

.text
    # ================================ MARK: fib
fib:
    # フィボナッチ数を求める
    # fib(0) = 0, fib(1) = 1, fib(n) = fib(n-1) + fib(n-2)
    # args
    #   $a0: n
    # ret
    #   $v0: fib(n)

    # -- 実装 --
    slti    $t0, $a0, 2             # n < 2 ならベースケース
    bne     $t0, $zero, fib_base
    move    $v0, $a0                # n が 0 か 1 ならそのまま返す

    # -- 退避 --
    addiu   $sp, $sp, -12
    sw      $ra, 8($sp)
    sw      $a0, 4($sp)             # n

    jal     fib                     # fib(n-1)
    addiu   $a0, $a0, -1

    lw      $a0, 4($sp)
    sw      $v0, 0($sp)             # 結果を退避
    jal     fib                     # fib(n-2)
    addiu   $a0, $a0, -2

    # -- 復元 --
    lw      $t1, 0($sp)
    lw      $ra, 8($sp)
    addu    $v0, $v0, $t1           # fib(n-1) + fib(n-2)
    jr      $ra
    addiu   $sp, $sp, 12

fib_base:
    jr      $ra
    nop
    # ================================ end: fib

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
    addiu   $sp, $sp, -8
    sw      $ra, 4($sp)
    # -- 実装 --
    la      $a0, msg
    jal     print_string
    nop

    jal     fib                     # 55
    addiu   $a0, $zero, 10
    jal     print_int
    move    $a0, $v0

    # -- 復元 --
    lw      $ra, 4($sp)
    la      $a0, endl
    j       print_string            # print_stringからmainの呼び出し元へ直接戻る
    addiu   $sp, $sp, 8
    # ================================ end: main

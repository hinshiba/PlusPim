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

    # -- 退避 --
    addiu   $sp, $sp, -12
    sw      $ra, 8($sp)
    sw      $a0, 4($sp)             # n

    addiu   $a0, $a0, -1
    jal     fib                     # fib(n-1)
    sw      $v0, 0($sp)             # 結果を退避

    lw      $a0, 4($sp)
    addiu   $a0, $a0, -2
    jal     fib                     # fib(n-2)

    lw      $t1, 0($sp)
    addu    $v0, $v0, $t1           # fib(n-1) + fib(n-2)

    # -- 復元 --
    lw      $ra, 8($sp)
    addiu   $sp, $sp, 12
    jr      $ra

fib_base:
    move    $v0, $a0                # n が 0 か 1 ならそのまま返す
    jr      $ra
    # ================================ end: fib

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
    addiu   $sp, $sp, -8
    sw      $ra, 4($sp)
    # -- 実装 --
    la      $a0, msg
    jal     print_string

    li      $a0, 10
    jal     fib                     # 55
    move    $a0, $v0
    jal     print_int

    la      $a0, endl
    jal     print_string
    # -- 復元 --
    lw      $ra, 4($sp)
    addiu   $sp, $sp, 8
    jr      $ra
    # ================================ end: main

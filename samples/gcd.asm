# ユークリッドの互除法で最大公約数と最小公倍数を求める
.data
msg_gcd:
    .asciiz "gcd(1071, 462) = "
msg_lcm:
    .asciiz "lcm(1071, 462) = "
endl:
    .asciiz "\n"

.text
    # ================================ MARK: gcd
gcd:
    # 最大公約数を求める
    # args
    #   $a0: a
    #   $a1: b
    # ret
    #   $v0: gcd(a, b)

    # -- 実装 --
gcd_loop:
    beq     $a1, $zero, gcd_ret     # b == 0 なら a が答え
    div     $a0, $a1
    mfhi    $t0                     # a % b
    move    $a0, $a1
    move    $a1, $t0
    j       gcd_loop
gcd_ret:
    move    $v0, $a0
    jr      $ra
    # ================================ end: gcd

    # ================================ MARK: lcm
lcm:
    # 最小公倍数を求める
    # args
    #   $a0: a
    #   $a1: b
    # ret
    #   $v0: lcm(a, b)

    # -- 退避 --
    addiu   $sp, $sp, -16
    sw      $ra, 12($sp)
    sw      $s0, 8($sp)
    sw      $s1, 4($sp)
    # -- 実装 --
    move    $s0, $a0
    move    $s1, $a1

    jal     gcd                     # $v0 = gcd(a, b)

    div     $s0, $v0                # オーバーフローを避けるため先に割る
    mflo    $t0                     # a / gcd
    mult    $t0, $s1
    mflo    $v0                     # (a / gcd) * b
    # -- 復元 --
    lw      $s1, 4($sp)
    lw      $s0, 8($sp)
    lw      $ra, 12($sp)
    addiu   $sp, $sp, 16
    jr      $ra
    # ================================ end: lcm

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
    la      $a0, msg_gcd
    jal     print_string
    li      $a0, 1071
    li      $a1, 462
    jal     gcd                     # 21
    move    $a0, $v0
    jal     print_int
    la      $a0, endl
    jal     print_string

    la      $a0, msg_lcm
    jal     print_string
    li      $a0, 1071
    li      $a1, 462
    jal     lcm                     # 23562
    move    $a0, $v0
    jal     print_int
    la      $a0, endl
    jal     print_string
    # -- 復元 --
    lw      $ra, 4($sp)
    addiu   $sp, $sp, 8
    jr      $ra
    # ================================ end: main

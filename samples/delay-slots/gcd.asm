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
    beq     $a1, $zero, gcd_ret     # b == 0 なら a が答え
    move    $v0, $a0                # $v0 を a として回す
gcd_loop:
    div     $v0, $a1
    mfhi    $t0                     # a % b
    move    $v0, $a1
    bne     $t0, $zero, gcd_loop
    move    $a1, $t0
gcd_ret:
    jr      $ra
    nop
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
    jal     gcd                     # $v0 = gcd(a, b)
    move    $s1, $a1

    div     $s0, $v0                # オーバーフローを避けるため先に割る
    mflo    $t0                     # a / gcd
    mult    $t0, $s1
    # -- 復元 --
    lw      $s1, 4($sp)
    lw      $s0, 8($sp)
    lw      $ra, 12($sp)
    mflo    $v0                     # (a / gcd) * b
    jr      $ra
    addiu   $sp, $sp, 16
    # ================================ end: lcm

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
    la      $a0, msg_gcd
    jal     print_string
    nop
    li      $a0, 1071
    jal     gcd                     # 21
    addiu   $a1, $zero, 462
    jal     print_int
    move    $a0, $v0
    la      $a0, endl
    jal     print_string
    nop

    la      $a0, msg_lcm
    jal     print_string
    nop
    li      $a0, 1071
    jal     lcm                     # 23562
    addiu   $a1, $zero, 462
    jal     print_int
    move    $a0, $v0

    # -- 復元 --
    lw      $ra, 4($sp)
    la      $a0, endl
    j       print_string            # print_stringからmainの呼び出し元へ直接戻る
    addiu   $sp, $sp, 8
    # ================================ end: main

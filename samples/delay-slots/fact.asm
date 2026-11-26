.data
dialog:
    .align  2
    .asciiz "The factorial of 10 is "
endl:
    .align  2
    .asciiz "\n"
.text
    .align  2

    # ================================ MARK: _fact
_fact:
    # 階乗を計算する
    # args
    #   $a0: n
    #   $a1: acc
    # ret
    #   $v0: n!

    # -- 実装 --
    # n == 0 then return acc
    slti    $t0,            $a0,        1
    bne     $t0,            $zero,      $ret__fact
    move    $v0,            $a1
    # n != 0
    mult    $a0,            $a1
    mflo    $a1
    j       _fact
    addiu   $a0,            $a0,        -1

    $ret__fact:
    jr      $ra
    nop
    # ================================ end: _fact

    # ================================ MARK: fact
fact:
    # 階乗を計算する
    # args
    #   $a0: n
    # ret
    #   $v0: n!

    # -- 実装 --
    j       _fact
    addiu   $a1,            $zero,      1
    # ================================ end: fact


print_int:
    # intを出力する
    # args
    #   $a0: 引数
    li      $v0,            1
    syscall
    jr      $ra
    nop


print_string:
    # 文字列を出力する
    # args
    #   $a0: 文字列の先頭アドレス
    li      $v0,            4
    syscall
    jr      $ra
    nop


read_int:
    # intを読み込む
    # ret
    #   $v0: 読み込んだint
    li      $v0,            5
    syscall
    jr      $ra
    nop

    # ================================ MARK: main
    .globl main
main:
    # 10の階乗を計算する
    # -- 退避 --
    addiu   $sp,            $sp,        -24
    sw      $ra,            20($sp)         # 場所が決まっている
    sw      $fp,            16($sp)         # 場所が決まっている
    # -- 実装 --

    la      $a0,            dialog
    jal     print_string
    move    $fp,            $sp             # fpの設定

    jal     fact                            # fact(n)
    addiu   $a0,            $zero,      10  # n
    jal     print_int
    move    $a0,            $v0

    # -- 復元 --
    lw      $ra,            20($sp)
    lw      $fp,            16($sp)
    la      $a0,            endl
    j       print_string                    # print_stringからmainの呼び出し元へ直接戻る
    addiu   $sp,            $sp,        24  # pop
    # ================================ end: main

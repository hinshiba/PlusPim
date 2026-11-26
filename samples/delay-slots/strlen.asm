# 文字列をバイト単位で走査して長さを数え，大文字に変換して出力する
.data
src:
    .asciiz "Hello, PlusPim!"
dst:
    .space  64
msg_len:
    .asciiz "length = "
endl:
    .asciiz "\n"

.text
    # ================================ MARK: strlen
strlen:
    # NULL終端文字列の長さを数える
    # args
    #   $a0: 文字列の先頭アドレス
    # ret
    #   $v0: 長さ．NULLは含まない

    # -- 実装 --
    move    $v0, $zero
sl_loop:
    lb      $t0, 0($a0)
    addiu   $a0, $a0, 1
    bne     $t0, $zero, sl_loop     # NULL終端
    addiu   $v0, $v0, 1
    jr      $ra
    addiu   $v0, $v0, -1            # NULLの分を引く
    # ================================ end: strlen

    # ================================ MARK: to_upper
to_upper:
    # 英小文字を大文字に変換しながらコピーする
    # args
    #   $a0: コピー元の先頭アドレス
    #   $a1: コピー先の先頭アドレス

    # -- 実装 --
tu_loop:
    lb      $t0, 0($a0)
    addiu   $a0, $a0, 1
    beq     $t0, $zero, tu_ret
    sb      $t0, 0($a1)             # そのままコピーする．NULLならそれが終端になる

    slti    $t1, $t0, 97            # 'a' 未満か
    bne     $t1, $zero, tu_next
    slti    $t1, $t0, 123           # 'z' 以下か
    beq     $t1, $zero, tu_next
    addiu   $t0, $t0, -32
    sb      $t0, 0($a1)             # 小文字なので大文字で上書きする

tu_next:
    j       tu_loop
    addiu   $a1, $a1, 1
tu_ret:
    jr      $ra
    nop
    # ================================ end: to_upper

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
    la      $a0, msg_len
    jal     print_string
    nop
    la      $a0, src
    jal     strlen                  # 15
    nop
    jal     print_int
    move    $a0, $v0
    la      $a0, endl
    jal     print_string
    nop

    la      $a0, src
    la      $a1, dst
    jal     to_upper                # "HELLO, PLUSPIM!"
    nop
    la      $a0, dst
    jal     print_string
    nop
    # -- 復元 --
    lw      $ra, 4($sp)
    la      $a0, endl
    j       print_string            # print_stringからmainの呼び出し元へ直接戻る
    addiu   $sp, $sp, 8
    # ================================ end: main

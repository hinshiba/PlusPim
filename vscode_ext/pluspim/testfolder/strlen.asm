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
    beq     $t0, $zero, sl_ret      # NULL終端
    addiu   $v0, $v0, 1
    addiu   $a0, $a0, 1
    j       sl_loop
sl_ret:
    jr      $ra
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
    beq     $t0, $zero, tu_ret

    slti    $t1, $t0, 97            # 'a' 未満か
    bne     $t1, $zero, tu_store
    slti    $t1, $t0, 123           # 'z' 以下か
    beq     $t1, $zero, tu_store
    addiu   $t0, $t0, -32           # 小文字なので大文字にする

tu_store:
    sb      $t0, 0($a1)
    addiu   $a0, $a0, 1
    addiu   $a1, $a1, 1
    j       tu_loop
tu_ret:
    sb      $zero, 0($a1)           # NULL終端
    jr      $ra
    # ================================ end: to_upper

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
    la      $a0, msg_len
    jal     print_string
    la      $a0, src
    jal     strlen                  # 15
    move    $a0, $v0
    jal     print_int
    la      $a0, endl
    jal     print_string

    la      $a0, src
    la      $a1, dst
    jal     to_upper                # "HELLO, PLUSPIM!"
    la      $a0, dst
    jal     print_string
    la      $a0, endl
    jal     print_string
    # -- 復元 --
    lw      $ra, 4($sp)
    addiu   $sp, $sp, 8
    jr      $ra
    # ================================ end: main

# 1反復3命令のループを n 回まわす．履歴にレジスタとメモリの書き込みの両方を積む
# ベンチマークは n の初期値 33333 を書き換えて命令数を決め，done の行にブレークポイントを置く
.data
n:
    .word   33333
buf:
    .word   0

.text
    .globl main
main:
    la      $t1, n
    lw      $t0, 0($t1)
    la      $t1, buf
loop:
    sw      $t0, 0($t1)
    addiu   $t0, $t0, -1
    bne     $t0, $zero, loop
done:
    nop
    jr      $ra

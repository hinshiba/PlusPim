.ktext
__exception_handler:
    mfc0 $k0, $13        # Cause レジスタ読み取り
    srl  $k0, $k0, 2
    andi $k0, $k0, 0x1F  # ExcCode 抽出

    # ExcCode == 8 (Syscall) なら runtime_call! を呼ぶ
    # どの例外も EPC+4 にスキップして復帰する
    addiu $k1, $zero, 8
    bne  $k0, $k1, __skip
    mfc0 $k0, $14        # EPC
    runtime_call!

__skip:
    addiu $k0, $k0, 4
    mtc0 $k0, $14        # EPC ← EPC+4
    eret

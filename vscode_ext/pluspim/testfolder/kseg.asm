.ktext
__exception_handler:
    mfc0 $k0, $13        # Cause レジスタ読み取り
    srl  $k0, $k0, 2
    andi $k0, $k0, 0x1F  # ExcCode 抽出

    # ExcCode == 8 (Syscall) ?
    addiu $k1, $zero, 8
    beq  $k0, $k1, __handle_syscall
    nop

    # その他の例外: EPC+4 にスキップして復帰
    mfc0 $k0, $14        # EPC
    addiu $k0, $k0, 4
    mtc0 $k0, $14        # EPC ← EPC+4
    eret

__handle_syscall:
    runtime_call!
    mfc0 $k0, $14        # EPC
    addiu $k0, $k0, 4
    mtc0 $k0, $14        # EPC ← EPC+4
    eret
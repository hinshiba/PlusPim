using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using PlusPim.Asm;


BenchmarkSwitcher.FromAssembly(typeof(LexBench).Assembly).Run(args);

[InProcess]
[MemoryDiagnoser]
public class LexBench {

    [Params("# .word で確保した配列を走査して合計と最大値を求める\n.data\narr:\n    .word   5, 3, 9, 1, 7, 2, 8\narr_len:\n    .word   7\nmsg_sum:\n    .asciiz \"sum = \"\nmsg_max:\n    .asciiz \"max = \"\nendl:\n    .asciiz \"\\n\"\n\n.text\n    # ================================ MARK: array_sum\narray_sum:\n    # 配列の合計を求める\n    # args\n    #   $a0: 配列の先頭アドレス\n    #   $a1: 要素数\n    # ret\n    #   $v0: 合計\n\n    # -- 実装 --\n    move    $v0, $zero              # acc = 0\n    move    $t0, $zero              # i = 0\nas_loop:\n    beq     $t0, $a1, as_ret\n    sll     $t1, $t0, 2             # i * 4\n    addu    $t1, $a0, $t1           # &arr[i]\n    lw      $t2, 0($t1)\n    addu    $v0, $v0, $t2\n    addiu   $t0, $t0, 1\n    j       as_loop\nas_ret:\n    jr      $ra\n    # ================================ end: array_sum\n\n    # ================================ MARK: array_max\narray_max:\n    # 配列の最大値を求める．要素数は1以上を仮定する\n    # args\n    #   $a0: 配列の先頭アドレス\n    #   $a1: 要素数\n    # ret\n    #   $v0: 最大値\n\n    # -- 実装 --\n    lw      $v0, 0($a0)             # max = arr[0]\n    li      $t0, 1                  # i = 1\nam_loop:\n    beq     $t0, $a1, am_ret\n    sll     $t1, $t0, 2\n    addu    $t1, $a0, $t1\n    lw      $t2, 0($t1)\n    slt     $t3, $v0, $t2           # max < arr[i] ?\n    beq     $t3, $zero, am_skip\n    move    $v0, $t2\nam_skip:\n    addiu   $t0, $t0, 1\n    j       am_loop\nam_ret:\n    jr      $ra\n    # ================================ end: array_max\n\nprint_int:\n    li      $v0, 1\n    syscall\n    jr      $ra\n\nprint_string:\n    li      $v0, 4\n    syscall\n    jr      $ra\n\n    # ================================ MARK: main\n    .globl main\nmain:\n    # -- 退避 --\n    addiu   $sp, $sp, -16\n    sw      $ra, 12($sp)\n    sw      $s0, 8($sp)\n    sw      $s1, 4($sp)\n    # -- 実装 --\n    la      $s0, arr                # 配列の先頭\n    la      $t0, arr_len\n    lw      $s1, 0($t0)             # 要素数\n\n    la      $a0, msg_sum\n    jal     print_string\n    move    $a0, $s0\n    move    $a1, $s1\n    jal     array_sum               # 35\n    move    $a0, $v0\n    jal     print_int\n    la      $a0, endl\n    jal     print_string\n\n    la      $a0, msg_max\n    jal     print_string\n    move    $a0, $s0\n    move    $a1, $s1\n    jal     array_max               # 9\n    move    $a0, $v0\n    jal     print_int\n    la      $a0, endl\n    jal     print_string\n    # -- 復元 --\n    lw      $s1, 4($sp)\n    lw      $s0, 8($sp)\n    lw      $ra, 12($sp)\n    addiu   $sp, $sp, 16\n    jr      $ra\n    # ================================ end: main\n")]
    public string Line = "";

    [Benchmark]
    public object Lex() {
        return Lexer.Lex(this.Line);
    }
}

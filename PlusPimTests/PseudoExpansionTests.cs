using PlusPim.Application;
using PlusPim.Debuggers.PlusPimDbg;
using PlusPim.Logging;
using Xunit;

namespace PlusPimTests;

/// <summary>
/// 疑似命令の展開先の記録 (<see cref="PlusPimDbg.GetPseudoExpansions"/>) のテスト
/// </summary>
public class PseudoExpansionTests {
    private static (int Line, string Mnemonic, (uint Address, string Text)[] Instructions)[] Simplify(PseudoExpansionInfo[] expansions) {
        return [.. expansions.Select(e => (e.Line, e.Mnemonic, e.Instructions.Select(i => (i.Address, i.Text)).ToArray()))];
    }

    [Fact]
    public void GetPseudoExpansions_ListsPseudoLinesWithGlobalAddresses() {
        FileInfo first = TestHelpers.WriteTempAsm("""
            .data
            x:
              .word 1
            .text
            main:
              la $t0, x
            .ktext
              move $k0, $k1
            """);
        FileInfo second = TestHelpers.WriteTempAsm("""
            .text
              addiu $t0, $zero, 1
              li $t1, 0x12345
              bogus $t0
              NOP
            """);
        try {
            PlusPimDbg debugger = new([first, second], Logger.Null);

            Assert.Equal([
                (6, "la", new[] { (0x00400000u, "lui $t0, 0x1000"), (0x00400004u, "ori $t0, $t0, 0x0000") }),
                (8, "move", new[] { (0x80000180u, "addu $k0, $k1, $zero") }),
            ], Simplify(debugger.GetPseudoExpansions(first)));

            // 2つ目のファイルは1つ目の命令の後に配置される．解析できない行は含まない
            Assert.Equal([
                (3, "li", new[] { (0x0040000Cu, "lui $t1, 0x0001"), (0x00400010u, "ori $t1, $t1, 0x2345") }),
                (5, "nop", new[] { (0x00400014u, "sll $zero, $zero, 0") }),
            ], Simplify(debugger.GetPseudoExpansions(second)));

            Assert.Empty(debugger.GetPseudoExpansions(new FileInfo(Path.Combine(Path.GetTempPath(), "not-loaded.s"))));
        } finally {
            first.Delete();
            second.Delete();
        }
    }
}

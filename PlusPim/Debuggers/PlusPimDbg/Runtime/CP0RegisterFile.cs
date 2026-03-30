using PlusPim.Debuggers.PlusPimDbg.Program.records;

namespace PlusPim.Debuggers.PlusPimDbg.Runtime;

internal record class CP0RegisterFile {
    /// <summary>
    /// アドレス例外を引き起こしたアドレス
    /// </summary>
    public Address? BadVAddr { get; init; }

    /// <summary>
    /// StatusレジスタのExlビットの値
    /// </summary>
    public bool Exl { get; init; }

    /// <summary>
    /// 例外番号
    /// </summary>
    public ExcCode Exc { get; init; }

    /// <summary>
    /// 例外を引き起こした命令のインデックス
    /// </summary>
    public InstructionIndex Epc { get; init; }

    public static readonly CP0RegisterFile Default = new() {
        BadVAddr = Address.InValid,
        Exl = false,
        Exc = ExcCode.RI,
        Epc = InstructionIndex.Invalid
    };
}

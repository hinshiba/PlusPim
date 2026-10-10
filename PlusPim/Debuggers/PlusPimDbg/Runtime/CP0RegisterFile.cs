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
    /// 例外を引き起こした命令のPC
    /// </summary>
    public Address Epc { get; init; }

    public static readonly CP0RegisterFile Default = new() {
        BadVAddr = null,
        Exl = false,
        Exc = ExcCode.RI,
        Epc = Address.InValid
    };

    /// <summary>
    /// 対応している CP0 レジスタ番号か (8: BadVAddr，12: Status，13: Cause，14: EPC)
    /// </summary>
    public static bool IsSupported(int regNum) {
        return regNum is 8 or 12 or 13 or 14;
    }
}

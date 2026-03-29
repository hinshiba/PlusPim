namespace PlusPim.Application;

/// <summary>
/// スタックフレームに関する情報を表すクラス
/// </summary>
internal sealed class StackFrameInfo {
    /// <summary>
    /// スタックフレームに一意なID 小さいほうが呼び出し元に近い
    /// </summary>
    public required int FrameId { get; init; }

    /// <summary>
    /// このフレームが対応する関数・ラベル名
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 停止中の行番号
    /// </summary>
    public required int Line { get; init; }

    /// <summary>
    /// レジスタのスナップショットまたは実行中の値
    /// </summary>
    public required uint[] Registers { get; init; }

    /// <summary>
    /// プログラムカウンタの値
    /// </summary>
    public required uint PC { get; init; }

    /// <summary>
    /// HIレジスタの値
    /// </summary>
    public required uint HI { get; init; }

    /// <summary>
    /// LOレジスタの値
    /// </summary>
    public required uint LO { get; init; }

    // CP0レジスタ (ライブフレームのみ設定)
    public uint? CP0BadVAddr { get; init; }
    public uint? CP0Status { get; init; }
    public uint? CP0Cause { get; init; }
    public uint? CP0EPC { get; init; }
}

using PlusPim.Instructions;

namespace PlusPim.Dbg;

/// <summary>
/// 有効にする遅延スロット
/// </summary>
[Flags]
public enum DelaySlotsConfig: byte {
    /// <summary>遅延スロットは存在しない</summary>
    None = 0,
    /// <summary>1命令分の分岐遅延スロットが配置される</summary>
    Branch = 1 << 0,
    /// <summary>1命令分のロード遅延スロットが配置される</summary>
    Load = 1 << 1,
    All = Branch | Load,
}

/// <summary>遅延スロットの後に行う移動の種類</summary>
public enum Transfer: byte {
    /// <summary>ジャンプする. 呼び出し元へ戻るならフレームを取り出す</summary>
    Jump,
    /// <summary>呼び出す. フレームを積む</summary>
    Call,
    /// <summary>分岐が成立しなかった. フレームは変えない</summary>
    Fallthrough,
}

/// <summary>
/// 実行した分岐またはジャンプ
/// </summary>
/// <param name="Site">分岐またはジャンプの命令のアドレス. 遅延スロットで例外が起きたときのEPC</param>
/// <param name="Target">遅延スロットの後に移る先</param>
/// <param name="Transfer">移るときのコールスタックの操作</param>
public readonly record struct Branch(Address Site, Address Target, Transfer Transfer);

/// <summary>
/// 完了していないロード
/// </summary>
/// <param name="Reg">ロード先</param>
/// <param name="Value">ロードした値</param>
public readonly record struct DelayedLoad(GprId Reg, uint Value);

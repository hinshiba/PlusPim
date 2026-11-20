using PlusPim.Image;
using PlusPim.Instructions;

namespace PlusPim.Asm;

/// <summary>新しいセクションを始める</summary>
/// <param name="Kind">配置先のセグメント</param>
/// <param name="Origin">指定された先頭. なければ前のセクションに続ける</param>
internal readonly record struct SwitchSegment(SegmentKind Kind, Address? Origin);

/// <summary>次に配置するものの先頭にラベルを置く</summary>
internal readonly record struct DefineLabel(string Name);

/// <summary>1行分の命令を配置する</summary>
/// <param name="Templates">行を構成する命令. 疑似命令なら展開後の列</param>
/// <param name="Pseudo">疑似命令ならそのニーモニック</param>
internal readonly record struct PlaceCode(Template[] Templates, string? Pseudo);

/// <summary>確定したバイト列を配置する</summary>
internal readonly record struct Emit(byte[] Bytes);

/// <summary>領域を確保する</summary>
internal readonly record struct Reserve(uint Size);

/// <summary>2の <paramref name="Log2"/> 乗の倍数まで進める</summary>
internal readonly record struct AlignTo(int Log2);

/// <summary>ラベルを他のファイルへ公開する</summary>
internal readonly record struct DeclareGlobal(string Name);

/// <summary>
/// 配置への効果.
/// </summary>
internal readonly union LayoutEffect(SwitchSegment, DefineLabel, PlaceCode, Emit, Reserve, AlignTo, DeclareGlobal);

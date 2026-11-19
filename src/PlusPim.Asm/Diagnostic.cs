using PlusPim.Image;
using System.Collections.Immutable;

namespace PlusPim.Asm;

/// <summary>
/// アセンブラで発生する警告の番号
/// </summary>
/// <remarks>警告の番号と回復操作はドキュメントと一致する必要がある.</remarks>
public enum WarningCode: ushort {
    UnparsableLine = 1,
    UnknownMnemonic = 2,
    UnknownDirective = 3,
    InvalidOperand = 4,
    MissingSegment = 5,
    MisplacedDirective = 6,
    DuplicateLabel = 7,
    UndefinedGlobal = 8,
    DuplicateGlobal = 9,
    UndefinedSymbol = 10,
    MissingEntry = 11,
    InvalidOperands = 12,
    MisplacedInstruction = 13,
    DuplicateSource = 14,
}

/// <summary>
/// アセンブラで発生するエラーの番号. 回復しない.
/// </summary>
/// <remarks>番号は <see cref="WarningCode"/> と同じく一致させる.</remarks>
public enum ErrorCode: ushort {
    SourceNotFound = 1,
    ValueOutOfRange = 2,
    NonAsciiString = 3,
    MisalignedText = 4,
    SectionOutOfRange = 5,
    SectionOverlap = 6,
}

/// <summary>診断の番号</summary>
public readonly union DiagnosticCode(WarningCode, ErrorCode);

/// <summary>
/// アセンブラの診断.
/// </summary>
/// <param name="Code">診断の番号</param>
/// <param name="Location">対象となる場所. 行に結びつかない診断では <see langword="null"/>.</param>
/// <param name="Args">表示に埋め込む値</param>
public readonly record struct Diagnostic(DiagnosticCode Code, SourceLocation? Location, ImmutableArray<string> Args);

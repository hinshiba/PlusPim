using PlusPim.Adapter.Controller.Dap.Wire;
using PlusPim.Instructions;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PlusPim.Adapter.Controller.Dap.Protocol;

/// <summary>
/// <see langword="int"/> を包むNewtype
/// </summary>
/// <typeparam name="TSelf">実装する型</typeparam>
internal interface IInt32Newtype<TSelf> where TSelf : struct, IInt32Newtype<TSelf> {
    /// <summary>通信上の値</summary>
    int Value { get; }

    /// <summary>通信上の値から生成を試みる</summary>
    /// <param name="value">通信上の値</param>
    /// <param name="result">生成した値</param>
    /// <returns>不変条件を満たせば <see langword="true"/></returns>
    static abstract bool TryCreate(int value, out TSelf result);
}

/// <summary>
/// <see cref="IInt32Newtype{TSelf}"/> をJSONの数値として読み書きする
/// </summary>
/// <typeparam name="T">読み書きする型</typeparam>
internal sealed class Int32NewtypeConverter<T>: JsonConverter<T> where T : struct, IInt32Newtype<T> {
    /// <inheritdoc/>
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        return T.TryCreate(reader.GetInt32(), out T value) ? value : WireError.Throw<T>();
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) {
        writer.WriteNumberValue(value.Value);
    }
}

/// <summary>
/// メッセージの1以上の通し番号. 封筒で手で読み書きするので変換器を持たない
/// </summary>
internal readonly record struct Seq: IInt32Newtype<Seq> {
    private Seq(int value) {
        this.Value = value;
    }

    /// <inheritdoc/>
    public int Value { get; }

    /// <inheritdoc/>
    public static bool TryCreate(int value, out Seq result) {
        result = 0 < value ? new Seq(value) : default;
        return 0 < value;
    }
}

/// <summary>
/// ソース上の1始まりの行
/// </summary>
[JsonConverter(typeof(Int32NewtypeConverter<SourceLine>))]
internal readonly record struct SourceLine: IInt32Newtype<SourceLine> {
    private SourceLine(int value) {
        this.Value = value;
    }

    /// <summary>1始まりの行番号</summary>
    public int Value { get; }

    /// <inheritdoc/>
    public static bool TryCreate(int value, out SourceLine result) {
        result = 0 < value ? new SourceLine(value) : default;
        return 0 < value;
    }
}

/// <summary>
/// 停止中のフレーム表の添字であるフレームの識別子
/// </summary>
[JsonConverter(typeof(Int32NewtypeConverter<FrameId>))]
internal readonly record struct FrameId: IInt32Newtype<FrameId> {
    private FrameId(int value) {
        this.Value = value;
    }

    /// <summary>フレーム表の添字</summary>
    public int Value { get; }

    /// <summary>フレーム表の添字から作る</summary>
    /// <param name="index">0以上の添字</param>
    public static FrameId Of(int index) {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        return new FrameId(index);
    }

    /// <inheritdoc/>
    public static bool TryCreate(int value, out FrameId result) {
        result = 0 <= value ? new FrameId(value) : default;
        return 0 <= value;
    }
}

/// <summary>
/// 変数の参照. 0は「子なし」, それ以外はフレームとスコープの添字を符号化した値
/// </summary>
[JsonConverter(typeof(Int32NewtypeConverter<VariablesReference>))]
internal readonly record struct VariablesReference: IInt32Newtype<VariablesReference> {
    private VariablesReference(int value) {
        this.Value = value;
    }

    /// <summary>1つのフレームが持てるスコープの数</summary>
    public const int MaxScopes = 4;

    /// <summary>子を持たないことを表す参照</summary>
    public static VariablesReference None => default;

    /// <inheritdoc/>
    public int Value { get; }

    /// <summary>参照先のフレームとスコープの添字. <see cref="None"/> では <see langword="null"/></summary>
    public (int Frame, int Scope)? Target => this.Value == 0 ? null : Math.DivRem(this.Value - 1, MaxScopes);

    /// <summary>フレームとスコープの添字から作る</summary>
    /// <param name="frame">0以上のフレームの添字</param>
    /// <param name="scope"><see cref="MaxScopes"/> 未満のスコープの添字</param>
    public static VariablesReference Of(int frame, int scope) {
        ArgumentOutOfRangeException.ThrowIfNegative(frame);
        ArgumentOutOfRangeException.ThrowIfNegative(scope);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(scope, MaxScopes);
        return new VariablesReference(checked((frame * MaxScopes) + scope + 1));
    }

    /// <inheritdoc/>
    public static bool TryCreate(int value, out VariablesReference result) {
        result = 0 <= value ? new VariablesReference(value) : default;
        return 0 <= value;
    }
}

/// <summary>
/// 変数の値として見せる32ビットの値
/// </summary>
/// <param name="Value">値</param>
[JsonConverter(typeof(WordConverter))]
internal readonly record struct Word(uint Value);

/// <summary>
/// <c>readMemory</c> の要求を, 32ビットのアドレス空間で読める範囲と読めない量に分けたもの
/// </summary>
internal readonly record struct MemoryWindow {
    /// <summary>1回の応答で返す最大のバイト数</summary>
    public const int MaxBytes = 64 * 1024;

    private const long SpaceEnd = 1L << 32;

    private MemoryWindow(Address start, int readable, int unreadable) {
        this.Start = start;
        this.Readable = readable;
        this.Unreadable = unreadable;
    }

    /// <summary>読む範囲の先頭. 範囲が空なら参照のアドレス</summary>
    public Address Start { get; }

    /// <summary>読めるバイト数. <see cref="Start"/> から数えて32ビットを超えない</summary>
    public int Readable { get; }

    /// <summary>読めるバイトの後ろにある, 読めないバイト数</summary>
    public int Unreadable { get; }

    /// <summary>要求の値から範囲を作る. 失敗しない</summary>
    /// <remarks>アドレス0より前の部分は先頭を進めて捨て, 空間の末尾を超える部分を読めない量とする</remarks>
    /// <param name="reference">メモリの参照</param>
    /// <param name="offset">参照からのずれ</param>
    /// <param name="count">要求されたバイト数. <see cref="MaxBytes"/> で切り詰める</param>
    public static MemoryWindow Create(Address reference, int offset, uint count) {
        long start = reference.Addr + (long)offset;
        long end = start + Math.Min(count, MaxBytes);
        long readStart = Math.Clamp(start, 0, SpaceEnd);
        long readEnd = Math.Clamp(end, readStart, SpaceEnd);
        return readStart < readEnd
            ? new MemoryWindow(new Address((uint)readStart), (int)(readEnd - readStart), (int)(end - readEnd))
            : new MemoryWindow(reference, 0, (int)(end - start));
    }
}

/// <summary>
/// <c>launch</c> の <c>noDebug</c> と <c>stopOnEntry</c> から決まる起動の方針
/// </summary>
internal enum StartAction: byte {
    /// <summary>入口で止まる</summary>
    StopAtEntry,
    /// <summary>入口で止まらずに実行する</summary>
    Run,
    /// <summary>デバッグせずに実行する. ブレークポイントと例外フィルタを適用しない</summary>
    NoDebug,
}

/// <summary>
/// クライアントに見せる例外フィルタ. ランタイムエラーでは常に止まるので, そのフィルタはない
/// </summary>
[JsonConverter(typeof(ExceptionFilterConverter))]
internal enum ExceptionFilter: byte { Fatal, Break, Syscall }

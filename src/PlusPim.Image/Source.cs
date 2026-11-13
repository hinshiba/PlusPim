namespace PlusPim.Image;

/// <summary>ソースファイルの番号</summary>
public readonly record struct SourceId(int Index);

/// <summary>ソース上の行 (1-index)</summary>
public readonly record struct SourceLocation(SourceId Sid, int Line);

/// <summary>
/// 正規化済みの完全修飾のソースファイルのパス
/// </summary>
public readonly record struct SourcePath {
    public string Value { get; }

    private SourcePath(string value) {
        this.Value = value;
    }

    /// <summary>
    /// <paramref name="baseDir"/> を基準にパスを完全修飾して正規化する
    /// </summary>
    /// <remarks>
    /// CLIはカレントディレクトリ, <c>.include</c> はインクルード元を基準に解決する必要があるため.
    /// 辞書のキーとして比較するので, Windowsのドライブ文字を大文字にそろえる. (VS Codeなど)
    /// </remarks>
    /// <param name="raw">ユーザが与えたパス</param>
    /// <param name="baseDir">完全修飾のディレクトリ. そうでなければ <see cref="ArgumentException"/></param>
    /// <param name="path">正規化済みのパス</param>
    /// <returns><paramref name="raw"/> が空かNULを含むとき <see langword="false"/></returns>
    public static bool TryResolve(string raw, string baseDir, out SourcePath path) {
        if(raw.Length == 0 || raw.Contains('\0')) {
            path = default;
            return false;
        }
        string full = Path.GetFullPath(raw, baseDir);
        if(full is [char drive, ':', ..] && char.IsAsciiLetterLower(drive)) {
            full = string.Concat(char.ToUpperInvariant(drive).ToString(), full.AsSpan(1));
        }
        path = new SourcePath(full);
        return true;
    }

    public override string ToString() {
        return this.Value;
    }
}

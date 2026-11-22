using PlusPim.Asm;
using PlusPim.Image;

namespace PlusPim;

/// <summary>
/// ファイルシステムからソースを読む
/// </summary>
internal sealed class FileSourceReader: ISourceReader {
    public static readonly FileSourceReader Instance = new();

    private FileSourceReader() { }

    public string? Read(SourcePath path) {
        try {
            return File.ReadAllText(path.Value);
        } catch(Exception e) when(e is IOException or UnauthorizedAccessException) {
            return null;
        }
    }
}

using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Newtonsoft.Json;

namespace PlusPim.EditorController.DebugAdapter;

/// <summary>
/// 疑似命令の展開先を返すカスタム要求 <c>pluspimPseudoExpansions</c>
/// </summary>
/// <remarks>
/// <c>initialized</c> イベントの後から有効で，それより前は "Program is not loaded." で失敗する．
/// 読み込んでいないファイルは空の <c>lines</c> を返す．
/// セッション中にプログラムは変わらないので，クライアントはファイルごとに結果を保持してよい
/// </remarks>
internal sealed class PseudoExpansionsRequest: DebugRequestWithResponse<PseudoExpansionsArguments, PseudoExpansionsResponse> {
    public const string RequestType = "pluspimPseudoExpansions";

    public PseudoExpansionsRequest() : base(RequestType) {
    }
}

/// <summary>
/// <c>pluspimPseudoExpansions</c> の引数
/// </summary>
internal sealed class PseudoExpansionsArguments: DebugRequestArguments {
    /// <summary>
    /// 対象のソースファイル．<c>path</c> は setBreakpoints と同じ絶対パス
    /// </summary>
    [JsonProperty("source")]
    public Source? Source { get; set; }
}

/// <summary>
/// <c>pluspimPseudoExpansions</c> の応答
/// </summary>
internal sealed class PseudoExpansionsResponse: ResponseBody {
    /// <summary>
    /// 解析できた疑似命令の行 (行の順)．1命令に展開される行 (<c>move</c>，<c>nop</c> など) も含む
    /// </summary>
    [JsonProperty("lines")]
    public List<PseudoExpansionLine> Lines { get; set; } = [];
}

/// <summary>
/// 疑似命令の1行
/// </summary>
internal sealed class PseudoExpansionLine {
    /// <summary>
    /// 1始まりの行番号
    /// </summary>
    [JsonProperty("line")]
    public int Line { get; set; }

    /// <summary>
    /// 疑似命令のニーモニック (小文字)
    /// </summary>
    [JsonProperty("mnemonic")]
    public string Mnemonic { get; set; } = "";

    /// <summary>
    /// 展開先の命令 (アドレス順)
    /// </summary>
    [JsonProperty("instructions")]
    public List<PseudoExpansionInstruction> Instructions { get; set; } = [];
}

/// <summary>
/// 疑似命令の展開先の1命令
/// </summary>
internal sealed class PseudoExpansionInstruction {
    /// <summary>
    /// 命令のアドレス (<c>0x%08X</c>)
    /// </summary>
    [JsonProperty("address")]
    public string Address { get; set; } = "";

    /// <summary>
    /// 命令のアセンブリでの表記 (<c>lui $t0, 0x1000</c> など)．表記に未対応の命令は <c>?</c>
    /// </summary>
    [JsonProperty("text")]
    public string Text { get; set; } = "";
}

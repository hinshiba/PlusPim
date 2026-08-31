using System.Text;

namespace PlusPim.EditorController.DebugAdapter;

/// <summary>
/// stdioモード時にConsole.Outを差し替えてデバッギの標準出力をDAPのOutputEventへ転送する
/// </summary>
internal sealed class DebuggeeOutputWriter: TextWriter {
    private readonly DebugAdapter _adapter;

    internal DebuggeeOutputWriter(DebugAdapter adapter) {
        this._adapter = adapter;
    }

    public override Encoding Encoding => Encoding.UTF8;

    public override void Write(char value) {
        this._adapter.SendDebuggeeOutput(value.ToString());
    }

    public override void Write(string? value) {
        if(value is null) {
            return;
        }
        this._adapter.SendDebuggeeOutput(value);
    }

    public override void Write(char[] buffer, int index, int count) {
        if(buffer is null || count <= 0) {
            return;
        }
        this._adapter.SendDebuggeeOutput(new string(buffer, index, count));
    }
}

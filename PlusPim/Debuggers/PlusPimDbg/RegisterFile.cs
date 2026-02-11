namespace PlusPim.Debuggers.PlusPimDbg;

/// <summary>
/// 32本のMIPSレジスタを管理するクラス
/// </summary>
/// <remarks>$zero (レジスタ0) への書き込みは無視される</remarks>
internal sealed class RegisterFile {
    private readonly int[] _values = new int[32];

    public int this[RegisterID id] {
        get => this._values[(int)id];
        set {
            // $zero保護: 書き込みを無視
            if(id == RegisterID.Zero) {
                return;
            }
            this._values[(int)id] = value;
        }
    }

    public RegisterFile Clone() {
        RegisterFile clone = new();
        Array.Copy(this._values, clone._values, 32);
        return clone;
    }

    /// <summary>
    /// DAP層との境界で使用する配列表現
    /// </summary>
    public int[] ToArray() {
        return (int[])this._values.Clone();
    }
}

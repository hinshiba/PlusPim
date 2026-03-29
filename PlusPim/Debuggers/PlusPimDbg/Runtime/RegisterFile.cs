namespace PlusPim.Debuggers.PlusPimDbg.Runtime;

/// <summary>
/// 32本のMIPSの汎用レジスタを管理する
/// </summary>
internal sealed class RegisterFile {
    private readonly uint[] _values = new uint[32];

    /// <summary>
    /// 簡単にアクセスするためのインデクサ
    /// </summary>
    /// <param name="id">レジスタ番号<see cref="RegisterID"/>を参照すること</param>
    /// <returns>そのレジスタの値</returns>
    /// <remarks>$zeroへの書き込みは無視される．
    /// $zeroに書き込んでもエミュレータで例外等は発生しない</remarks>
    public uint this[RegisterID id] {
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
    /// 受け渡し等のために配列化する
    /// </summary>
    public uint[] ToArray() {
        return (uint[])this._values.Clone();
    }
}

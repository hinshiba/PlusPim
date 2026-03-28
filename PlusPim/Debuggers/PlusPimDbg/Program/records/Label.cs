namespace PlusPim.Debuggers.PlusPimDbg.Program.records;

/// <summary>
/// ラベルを表す値型
/// </summary>
/// <param name="Name">ラベルのシンボル名</param>
/// <param name="Addr">ラベルのアドレス</param>
internal readonly record struct Label(string Name, Address Addr) {
    public override string ToString() {
        return $"{this.Name}: @ 0x{this.Addr.Addr:X}";
    }

    public static readonly Label Invalid = new("<invalid>", Address.InValid);
}

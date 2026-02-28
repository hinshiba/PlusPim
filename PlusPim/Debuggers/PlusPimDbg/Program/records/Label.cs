namespace PlusPim.Debuggers.PlusPimDbg.Program.records;

/// <summary>
/// ラベルを表す値型
/// </summary>
/// <param name="Name">ラベルのシンボル名</param>
/// <param name="Addr">ラベルのアドレス</param>
internal readonly record struct Label(string Name, Address Addr) {
    public override string ToString() {
        return $"{this.Name}: is (0x{this.Addr.Addr:X})";
    }
}

// TODO ラベルのパーサー

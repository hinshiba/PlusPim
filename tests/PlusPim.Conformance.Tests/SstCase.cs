using System.Buffers.Binary;
using System.Text;

namespace PlusPim.Conformance.Tests;

/// <summary>
/// 分岐遅延スロットの状態
/// </summary>
/// <param name="Slot">次の命令が分岐遅延スロットにある</param>
/// <param name="Taken">分岐が成立した</param>
/// <param name="Target">成立したときの移り先</param>
internal readonly record struct SstBranch(bool Slot, bool Taken, uint Target);

/// <summary>
/// R3000の1命令の前後の状態
/// </summary>
/// <param name="Gpr">汎用レジスタ32本</param>
/// <param name="Load">完了していないロードの先と値. なければ <see langword="null"/></param>
internal sealed record SstState(uint[] Gpr, uint Hi, uint Lo, uint Epc, uint Cause, uint Pc, SstBranch Branch, (int Reg, uint Value)? Load);

/// <summary>バスの操作の種類</summary>
internal enum SstAccess: uint {
    Read = 1,
    Write = 2,
    Fetch = 4,
}

/// <summary>
/// 1回のバスの操作
/// </summary>
/// <param name="Size">バイト数. 値は下位からこのバイト数だけ有効</param>
internal readonly record struct SstCycle(SstAccess Access, uint Address, int Size, uint Value);

/// <summary>
/// SingleStepTestsの1ケース
/// </summary>
/// <param name="Opcode">実行する命令語</param>
/// <param name="Address">命令の番地</param>
internal sealed record SstCase(string Name, uint Opcode, uint Address, SstState Initial, SstState Final, SstCycle[] Cycles) {
    /// <summary>
    /// <c>*.json.bin</c> を読む. 形式は <c>SingleStepTests/r3000</c> の <c>transcode_json.py</c> による
    /// </summary>
    public static SstCase[] Parse(ReadOnlySpan<byte> file) {
        Reader r = new(file);
        SstCase[] cases = new SstCase[r.U32()];
        for(int i = 0; i < cases.Length; i++) {
            string name = r.Name(51);
            uint opcode = r.U32();
            uint address = r.U32();
            SstState initial = r.State();
            SstState final = r.State();
            SstCycle[] cycles = new SstCycle[r.U32()];
            for(int k = 0; k < cycles.Length; k++) {
                // 値とアドレスは64ビットで書かれるが, 上位は使わない
                uint value = (uint)r.U64();
                SstAccess access = (SstAccess)r.U32();
                uint addr = (uint)r.U64();
                cycles[k] = new(access, addr, (int)r.U32(), value);
            }
            cases[i] = new(name, opcode, address, initial, final, cycles);
        }
        return cases;
    }

    private ref struct Reader(ReadOnlySpan<byte> s) {
        private ReadOnlySpan<byte> _s = s;

        public uint U32() {
            uint v = BinaryPrimitives.ReadUInt32LittleEndian(this._s);
            this._s = this._s[4..];
            return v;
        }

        public ulong U64() {
            ulong v = BinaryPrimitives.ReadUInt64LittleEndian(this._s);
            this._s = this._s[8..];
            return v;
        }

        /// <summary>先頭1バイトが長さの固定長の文字列</summary>
        public string Name(int size) {
            string v = Encoding.ASCII.GetString(this._s.Slice(1, this._s[0]));
            this._s = this._s[size..];
            return v;
        }

        public SstState State() {
            uint[] gpr = new uint[32];
            for(int i = 0; i < gpr.Length; i++) {
                gpr[i] = this.U32();
            }
            uint hi = this.U32(), lo = this.U32(), epc = this.U32();
            // PSXに固有のTARは比べない
            _ = this.U32();
            uint cause = this.U32(), pc = this.U32();
            uint target = this.U32();
            bool slot = this.U32() != 0;
            bool taken = this.U32() != 0;
            int reg = unchecked((int)this.U32());
            uint value = this.U32();
            return new(gpr, hi, lo, epc, cause, pc, new(slot, taken, target), reg < 0 ? null : (reg, value));
        }
    }
}

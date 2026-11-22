using PlusPim.Instructions;
using System.Globalization;

namespace PlusPim.Asm;

/// <summary>文字列リテラル</summary>
internal readonly record struct Str(string Value);

/// <summary><c>offset($base)</c></summary>
internal readonly record struct Mem(long Offset, GprId Base);

/// <summary>
/// 構文上のオペランド.
/// </summary>
internal readonly union Arg(GprId, long, LabelRef, Str, Mem) {
    /// <summary>診断に埋め込む表記</summary>
    public string Display() {
        return this switch {
            LabelRef s => s.Name,
            Str s => s.Value,
            _ => this.Value?.ToString() ?? "",
        };
    }
}

/// <summary>
/// トークン列を <see cref="LayoutEffect"/> の列にする
/// </summary>
internal static class Parser {
    /// <summary>
    /// 1行を解析して効果を <paramref name="output"/> へ追加する.
    /// </summary>
    public static void Parse(ReadOnlySpan<Token> tokens, Reporter r, List<LayoutEffect> output) {
        int mark = output.Count;
        for(; !tokens.IsEmpty && LabelName(tokens[0]) is string label; tokens = tokens[1..]) {
            output.Add(new DefineLabel(label));
        }
        if(tokens.IsEmpty) {
            return;
        }
        LayoutEffect[]? effects = (tokens[0], ParseArgs(tokens[1..])) switch {
            (Directive d, Arg[] args) => FromDirective(d.Name, args, r),
            (Ident m, Arg[]) when NamesCp0ByGpr(m.Name, tokens[^1]) => OperandMismatch(m.Name, r),
            (Ident m, Arg[] args) => FromInstruction(m.Name, args, r),
            _ => Unparsable(r),
        };
        if(effects is null) {
            output.RemoveRange(mark, output.Count - mark);
        } else {
            output.AddRange(effects);
        }
    }

    /// <summary>
    /// ラベル宣言の名前を返す
    /// </summary>
    /// <returns><paramref name="t"/> がラベル宣言でなければ <see langword="null"/></returns>
    private static string? LabelName(Token t) {
        return t switch {
            Label l => l.Name,
            LocalLabel l => l.Name,
            _ => null,
        };
    }

    /// <summary>
    /// CP0レジスタを汎用レジスタの別名(<c>$t0</c>など)で書いたか.
    /// 解析後の <see cref="GprId"/> は番号と名前を区別できないので綴りで判定する.
    /// </summary>
    /// <remarks>本来なら字句解析が行うべきであるが，汎用レジスタ関連の命令が圧倒的に多いため今のところはこちらのほうが簡潔</remarks>
    private static bool NamesCp0ByGpr(string mnemonic, Token last) {
        return mnemonic is "mfc0" or "mtc0" && last is Ident { Name: ['$', char c, ..] } && !char.IsAsciiDigit(c);
    }

    /// <summary>オペランドの誤りを報告して行を捨てる</summary>
    private static LayoutEffect[]? OperandMismatch(string mnemonic, Reporter r) {
        r.Report(WarningCode.OperandMismatch, mnemonic);
        return null;
    }

    /// <summary>字句か構文のエラーを報告して行を捨てる</summary>
    private static LayoutEffect[]? Unparsable(Reporter r) {
        r.Report(WarningCode.UnparsableLine);
        return null;
    }

    /// <summary>
    /// アセンブラ指令 <c>.name</c> を <see cref="LayoutEffect"/> にする
    /// </summary>
    /// <returns>構文のエラーで行を捨てるなら <see langword="null"/></returns>
    private static LayoutEffect[]? FromDirective(string name, Arg[] args, Reporter r) {
        if(!Directives.ByName.TryGetValue(name, out Directives.Parse? parse)) {
            r.Report(WarningCode.UnknownDirective, "." + name);
            return null;
        }
        LayoutEffect[]? effects = parse(args, r);
        if(effects is null) {
            r.Report(WarningCode.OperandMismatch, "." + name);
        }
        return effects;
    }

    /// <summary>
    /// 実命令か疑似命令を <see cref="LayoutEffect"/> にする
    /// </summary>
    /// <returns>構文のエラーで行を捨てるなら <see langword="null"/></returns>
    private static LayoutEffect[]? FromInstruction(string mnemonic, Arg[] args, Reporter r) {
        Template[]? templates;
        string? pseudo = null;
        if(Pseudos.ByMnemonic.TryGetValue(mnemonic, out Pseudos.Expand? expand)) {
            templates = expand(args, r);
            pseudo = mnemonic;
        } else if(InstructionSet.ByMnemonic.TryGetValue(mnemonic, out FromOperands? factory)) {
            templates = FromReal(factory, args, r);
        } else {
            r.Report(WarningCode.UnknownMnemonic, mnemonic);
            return null;
        }
        if(templates is null) {
            r.Report(WarningCode.OperandMismatch, mnemonic);
            return null;
        }
        return templates.Length == 0 ? null : [new PlaceCode(templates, pseudo)];
    }

    /// <summary>
    /// 実命令のオペランドの種類を検証して展開可能な命令にする
    /// </summary>
    /// <returns>
    /// 展開可能な命令.
    /// オペランドの形が合わなければ <see langword="null"/>.
    /// 値の範囲の誤りを報告したなら空の配列.
    /// </returns>
    private static Template[]? FromReal(FromOperands factory, Arg[] args, Reporter r) {
        // オペランドの検証
        // 文字列リテラルは実命令のオペランドにならないので早期リターン
        if(args.Any(a => a is Str)) {
            return null;
        }
        AsmOperand[] ops = new AsmOperand[args.Length];
        bool inRange = true;
        for(int i = 0; i < args.Length; i++) {
            AsmOperand? op = args[i] switch {
                GprId g => g,
                LabelRef s => s,
                long v => ToImmediate(v, r) is Immediate imm ? imm : null,
                Mem m => ToImmediate(m.Offset, r) is Immediate off ? new Memory(m.Base, off) : null,
                _ => null,
            };
            inRange &= op.HasValue;
            ops[i] = op.GetValueOrDefault();
        }
        if(!inRange) {
            return [];
        }

        Template t = new(factory, ops);
        // 仮アドレスでラベル参照を解決してオペランドの種類が合うか確認する
        return t.Resolve(_ => default) is null ? null : [t];
    }

    /// <summary>
    /// 16bit即値へ変換する.
    /// </summary>
    /// <remarks>符号の解釈は命令による</remarks>
    private static Immediate? ToImmediate(long v, Reporter r) {
        if(v is < short.MinValue or > ushort.MaxValue) {
            r.Report(ErrorCode.ValueOutOfRange, v.ToString(CultureInfo.InvariantCulture));
            return null;
        }
        return new Immediate(unchecked((ushort)v));
    }

    /// <summary><c>arg (, arg)*</c>. 構文の誤りなら <see langword="null"/></summary>
    private static Arg[]? ParseArgs(ReadOnlySpan<Token> t) {
        List<Arg> args = [];
        while(!t.IsEmpty) {
            Arg arg;
            switch(t) {
                case [Ident reg, ..] when GprIds.TryParse(reg.Name, out GprId g):
                    arg = g;
                    t = t[1..];
                    break;
                case [Ident sym, ..]:
                    arg = new LabelRef(sym.Name);
                    t = t[1..];
                    break;
                case [Directive sym, ..]:
                    arg = new LabelRef("." + sym.Name);
                    t = t[1..];
                    break;
                case [StrLiteral s, ..]:
                    arg = new Str(s.Value);
                    t = t[1..];
                    break;
                case [IntLiteral offset, LParen, Ident reg, RParen, ..] when GprIds.TryParse(reg.Name, out GprId g):
                    arg = new Mem(offset.Value, g);
                    t = t[4..];
                    break;
                case [LParen, Ident reg, RParen, ..] when GprIds.TryParse(reg.Name, out GprId g):
                    arg = new Mem(0, g);
                    t = t[3..];
                    break;
                case [IntLiteral v, ..]:
                    arg = v.Value;
                    t = t[1..];
                    break;
                default:
                    return null;
            }
            args.Add(arg);
            if(t is [Comma, _, ..]) {
                t = t[1..];
            } else if(!t.IsEmpty) {
                return null;
            }
        }
        return [.. args];
    }
}

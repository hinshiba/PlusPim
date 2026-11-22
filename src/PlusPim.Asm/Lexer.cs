using System.Buffers;
using System.Text;

namespace PlusPim.Asm;
/// <summary>
/// トークン
/// </summary>
internal readonly union Token(Comma, LParen, RParen, Here, IntLiteral, StrLiteral, Ident, Directive, Label, LocalLabel, NumLabel, Bad);

/// <summary>
/// 値を持たないケースの共有インスタンス
/// </summary>
internal abstract class Unit<T> where T : Unit<T>, new() {
    public static readonly T Instance = new();
}

internal sealed class Comma: Unit<Comma>;
internal sealed class LParen: Unit<LParen>;
internal sealed class RParen: Unit<RParen>;
/// <summary><c>.</c>. ロケーションカウンタ</summary>
internal sealed class Here: Unit<Here>;

/// <summary>絶対値が32bit以内の整数リテラル</summary>
internal sealed record IntLiteral(long Value);
internal sealed record StrLiteral(string Value);
internal sealed record Ident(string Name);
/// <summary><c>.name</c>. <see cref="Name"/> は<c>.</c>を含まない文字列</summary>
internal sealed record Directive(string Name);
/// <summary><c>name:</c>. <see cref="Name"/> は<c>:</c>を含まない文字列</summary>
internal sealed record Label(string Name);
/// <summary><c>.name:</c> か <c>$name:</c>. <see cref="Name"/> は接頭辞を含み, <c>:</c> は含まない</summary>
internal sealed record LocalLabel(string Name);
/// <summary><c>123:</c></summary>
internal sealed record NumLabel(long Value);
internal sealed record Bad(string Text);

/// <summary>
/// 1行をトークン列にする
/// </summary>
internal static class Lexer {
    private const string Alnum = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ_abcdefghijklmnopqrstuvwxyz";
    // 0x1g を 0x1 と g に分けず1語の誤りとして報告するため
    private static readonly SearchValues<char> NumChars = SearchValues.Create(Alnum);
    private static readonly SearchValues<char> NameChars = SearchValues.Create(Alnum + ".$-");

    /// <summary>
    /// 字句解析を行い, 1行をトークン列にする.
    /// </summary>
    public static Token[] Lex(ReadOnlySpan<char> s) {
        List<Token> tokens = [];
        for(s = s.TrimStart(); !s.IsEmpty && s[0] != '#';) {
            (Token t, int n) = Scan(s);
            tokens.Add(t);
            s = s[n..].TrimStart();
        }
        return [.. tokens];
    }

    /// <summary>
    /// 単一のトークンへの変換を試み, 消費した長さを返す
    /// </summary>
    private static (Token, int) Scan(ReadOnlySpan<char> s) {
        // 先頭の文字で分岐する
        return s[0] switch {
            ',' => (Comma.Instance, 1),
            '(' => (LParen.Instance, 1),
            ')' => (RParen.Instance, 1),
            '"' => StrLit(s),
            (>= '0' and <= '9') or '+' or '-' => Number(s),
            (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or '_' or '.' or '$' => Name(s),
            _ => (new Bad(s[..1].ToString()), 1),
        };
    }

    /// <summary>
    /// 数字からはじまる文字列をトークンに変換する.
    /// 数値か数値の一時ラベルとなる.
    /// </summary>
    private static (Token, int) Number(ReadOnlySpan<char> s) {
        int n = 1 + Run(s[1..], NumChars);
        ReadOnlySpan<char> w = s[..n];
        bool label = s[n..].StartsWith(':') && !w.ContainsAnyExceptInRange('0', '9');
        return (ParseInt(w), label) switch {
            (long v, true) => (new NumLabel(v), n + 1),
            (long v, false) => (new IntLiteral(v), n),
            _ => (new Bad(w.ToString()), n),
        };
    }

    /// <summary>
    /// 符号, 2進接頭辞, 16進接頭辞, 桁区切り<c>_</c>を含む文字列を整数に変換する.
    /// </summary>
    /// <returns>進数における範囲外の桁を含むか, 絶対値が32bitを超えれば <see langword="null"/></returns>
    private static long? ParseInt(ReadOnlySpan<char> w) {
        bool neg = w[0] == '-';
        if(w[0] is '+' or '-') {
            w = w[1..];
        }
        uint radix = w is ['0', 'x' or 'X', ..] ? 16u : w is ['0', 'b' or 'B', ..] ? 2u : 10u;
        if(radix != 10) {
            w = w[2..];
        }
        ulong v = 0;
        bool any = false;
        foreach(char c in w) {
            // 先頭ではない <c>_</c> を許容する
            if(c == '_' && any) {
                continue;
            }
            // 0x20bitによってアルファベットを小文字化する. 数字は不変なので許容.
            uint d = (uint)"0123456789abcdef".IndexOf((char)(c | 0x20));
            if(radix <= d) {
                return null;
            }
            // 直前のvは2^32未満なのでulongでも大丈夫
            v = (v * radix) + d;
            if(uint.MaxValue < v) {
                return null;
            }
            any = true;
        }
        return !any || w[^1] == '_' ? null : neg ? -(long)v : (long)v;
    }

    /// <summary>
    /// 数字でない文字からはじまる文字列をトークンに変換する
    /// </summary>
    private static (Token, int) Name(ReadOnlySpan<char> s) {
        int n = 1 + Run(s[1..], NameChars);
        // runtime_call! のための末尾の !
        if(s[n..].StartsWith('!')) {
            return (new Ident(s[..(n + 1)].ToString()), n + 1);
        }
        // レジスタと <c>$</c> で始まる参照は構文解析で区別する
        return (s[0], n, s[n..].StartsWith(':')) switch {
            ('.', 1, _) => (Here.Instance, 1),
            ('$', 1, _) => (new Bad("$"), 1),
            ('.' or '$', _, true) => (new LocalLabel(s[..n].ToString()), n + 1),
            (_, _, true) => (new Label(s[..n].ToString()), n + 1),
            ('.', _, false) => (new Directive(s[1..n].ToString()), n),
            _ => (new Ident(s[..n].ToString()), n),
        };
    }

    /// <summary>
    /// <paramref name="s"/> の先頭から <paramref name="set"/> が続く長さを返す.
    /// </summary>
    private static int Run(ReadOnlySpan<char> s, SearchValues<char> set) {
        int n = s.IndexOfAnyExcept(set);
        return n < 0 ? s.Length : n;
    }

    /// <summary>
    /// 文字列リテラルをトークンに変換する.
    /// 閉じなければ行末までを <see cref="Bad"/> にする.
    /// </summary>
    private static (Token, int) StrLit(ReadOnlySpan<char> s) {
        StringBuilder sb = new();
        int i = 1;
        // エスケープのない区間を一括でコピー
        while(s[i..].IndexOfAny('"', '\\') is int k and >= 0) {
            k += i;
            _ = sb.Append(s[i..k]);
            if(s[k] == '"') {
                return (new StrLiteral(sb.ToString()), k + 1);
            }
            if(k + 1 == s.Length) {
                break;
            }
            _ = sb.Append(s[k + 1] switch {
                'n' => "\n",
                't' => "\t",
                '0' => "\0",
                '\\' => "\\",
                '"' => "\"",
                _ => s.Slice(k, 2).ToString(),
            });
            i = k + 2;
        }
        return (new Bad(s.ToString()), s.Length);
    }
}

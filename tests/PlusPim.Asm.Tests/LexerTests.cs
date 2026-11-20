namespace PlusPim.Asm.Tests;

/// <summary>
/// 1行の字句解析 (docs/tests/assembler/values.md)
/// </summary>
/// <remarks>改行コードは行の分割で除かれるので <see cref="SourceTests"/> で扱う</remarks>
public class LexerTests {
    /// <summary>トークン列を空白区切りの短い表記にする</summary>
    private static string Lex(string line) {
        return string.Join(' ', Lexer.Lex(line).Select(t => t switch {
            Comma => ",",
            LParen => "(",
            RParen => ")",
            Here => ".",
            IntLiteral x => $"int:{x.Value}",
            StrLiteral x => $"str:{x.Value}",
            Ident x => $"id:{x.Name}",
            Directive x => $"dir:{x.Name}",
            Label x => $"label:{x.Name}",
            LocalLabel x => $"local:{x.Name}",
            NumLabel x => $"num:{x.Value}",
            Bad x => $"bad:{x.Text}",
        }));
    }

    [Theory]
    [InlineData("", "")]
    [InlineData(" \t ", "")]
    [InlineData("# add $t0, $t1, $t2", "")]
    [InlineData("  # comment", "")]
    [InlineData("nop # comment", "id:nop")]
    [InlineData("nop#comment", "id:nop")]
    [InlineData("main:", "label:main")]
    [InlineData("main: nop", "label:main id:nop")]
    [InlineData(".asciiz \"a#b\" # c", "dir:asciiz str:a#b")]
    [InlineData(".asciiz \"a\\\\\" # c \"q\"", "dir:asciiz str:a\\")]
    [InlineData("add @", "id:add bad:@")]
    public void Line(string line, string expected) {
        Assert.Equal(expected, Lex(line));
    }

    [Theory]
    [InlineData("42", 42)]
    [InlineData("+42", 42)]
    [InlineData("-42", -42)]
    [InlineData("0x2a", 42)]
    [InlineData("0X2A", 42)]
    [InlineData("-0x2a", -42)]
    [InlineData("0b101010", 42)]
    [InlineData("4_2", 42)]
    [InlineData("0", 0)]
    [InlineData("4294967295", 4294967295)]
    [InlineData("-4294967295", -4294967295)]
    [InlineData("0xffffffff", 4294967295)]
    public void IntLiteral_Accepted(string text, long value) {
        Assert.Equal($"int:{value}", Lex(text));
    }

    [Theory]
    [InlineData("4294967296")]
    [InlineData("-4294967296")]
    [InlineData("9999999999")]
    [InlineData("10000000000")]
    [InlineData("0x100000000")]
    [InlineData("0x")]
    [InlineData("-")]
    [InlineData("+")]
    [InlineData("0x1g")]
    [InlineData("0b2")]
    [InlineData("1abc")]
    [InlineData("42_")]
    [InlineData("0x_2a")]
    public void IntLiteral_Rejected_AsOneWord(string text) {
        Assert.Equal($"bad:{text}", Lex(text));
    }

    [Theory]
    [InlineData("ADD", "id:ADD")]
    [InlineData("runtime_call!", "id:runtime_call!")]
    [InlineData("runtime_call", "id:runtime_call")]
    [InlineData("add!", "id:add!")]
    [InlineData("syscall $t0", "id:syscall id:$t0")]
    [InlineData("mfc0 $t0, $12", "id:mfc0 id:$t0 , id:$12")]
    [InlineData("j label", "id:j id:label")]
    public void Mnemonic(string line, string expected) {
        Assert.Equal(expected, Lex(line));
    }

    [Theory]
    [InlineData("add $t0, $t1, $t2", "id:add id:$t0 , id:$t1 , id:$t2")]
    [InlineData("add $t0,$t1,$t2", "id:add id:$t0 , id:$t1 , id:$t2")]
    [InlineData("add\t$t0 ,\t$t1 , $t2", "id:add id:$t0 , id:$t1 , id:$t2")]
    [InlineData("add $t0 $t1 $t2", "id:add id:$t0 id:$t1 id:$t2")]
    [InlineData("add $t0, $t1, $t2,", "id:add id:$t0 , id:$t1 , id:$t2 ,")]
    [InlineData("add , $t0, $t1, $t2", "id:add , id:$t0 , id:$t1 , id:$t2")]
    public void Separator(string line, string expected) {
        Assert.Equal(expected, Lex(line));
    }

    [Theory]
    [InlineData("4($sp)", "int:4 ( id:$sp )")]
    [InlineData("-4($sp)", "int:-4 ( id:$sp )")]
    [InlineData("0x10($t0)", "int:16 ( id:$t0 )")]
    [InlineData("($t0)", "( id:$t0 )")]
    [InlineData("4 ( $t0 )", "int:4 ( id:$t0 )")]
    [InlineData("-32769($t0)", "int:-32769 ( id:$t0 )")]
    [InlineData("65536($t0)", "int:65536 ( id:$t0 )")]
    [InlineData("4($t0", "int:4 ( id:$t0")]
    [InlineData("4(label)", "int:4 ( id:label )")]
    [InlineData("label($t0)", "id:label ( id:$t0 )")]
    public void MemoryOperand(string text, string expected) {
        Assert.Equal(expected, Lex(text));
    }

    /// <summary>レジスタとして正しいかは構文解析が決めるので, 綴りをそのまま残す</summary>
    [Theory]
    [InlineData("$t0")]
    [InlineData("$8")]
    [InlineData("$zero")]
    [InlineData("$ra")]
    [InlineData("$0")]
    [InlineData("$31")]
    [InlineData("$32")]
    [InlineData("$08")]
    [InlineData("$T0")]
    [InlineData("$t10")]
    [InlineData("$s8")]
    public void Register_KeepsSpelling(string text) {
        Assert.Equal($"id:{text}", Lex(text));
    }

    [Fact]
    public void Dollar_Alone_IsBad() {
        Assert.Equal("bad:$ bad:$", Lex("$ $"));
    }

    [Theory]
    [InlineData("main:", "label:main")]
    [InlineData("_loop:", "label:_loop")]
    [InlineData("a$b:", "label:a$b")]
    [InlineData("Loop: loop:", "label:Loop label:loop")]
    [InlineData(".L1:", "local:.L1")]
    [InlineData("$t0:", "local:$t0")]
    [InlineData("$31:", "local:$31")]
    [InlineData("$32:", "local:$32")]
    [InlineData("123:", "num:123")]
    [InlineData("1abc:", "bad:1abc bad::")]
    [InlineData("0x1:", "int:1 bad::")]
    [InlineData("label :", "id:label bad::")]
    [InlineData("a: b: c:", "label:a label:b label:c")]
    [InlineData("a:b:", "label:a label:b")]
    [InlineData("runtime_call!:", "id:runtime_call! bad::")]
    public void LabelDefinition(string line, string expected) {
        Assert.Equal(expected, Lex(line));
    }

    [Theory]
    [InlineData(".text", "dir:text")]
    [InlineData(".TEXT", "dir:TEXT")]
    [InlineData(".foo", "dir:foo")]
    [InlineData(".global", "dir:global")]
    [InlineData(".utf-8z \"a\"", "dir:utf-8z str:a")]
    [InlineData(".globl a, b", "dir:globl id:a , id:b")]
    [InlineData(".globl 1", "dir:globl int:1")]
    [InlineData(".text 0x00400100", "dir:text int:4194560")]
    [InlineData(".word .", "dir:word .")]
    [InlineData(".word 1, 2, 3", "dir:word int:1 , int:2 , int:3")]
    public void DirectiveName(string line, string expected) {
        Assert.Equal(expected, Lex(line));
    }

    [Theory]
    [InlineData("\"abc\"", "abc")]
    [InlineData("\"\"", "")]
    [InlineData("\"a b\"", "a b")]
    [InlineData("\"a#b\"", "a#b")]
    [InlineData("\"a,b\"", "a,b")]
    [InlineData("\"\n\t\0\\\\\\\"\"", "\n\t\0\\\"")]
    [InlineData("\"\a\"", "\a")]
    [InlineData("\"\x7f\u0080\u07ff\u0800\uffff\U00010000\"", "\x7f\u0080\u07ff\u0800\uffff\U00010000")]
    [InlineData("\"\ud800\"", "\ud800")]
    public void String_Accepted(string text, string value) {
        Assert.Equal([new StrLiteral(value)], Lexer.Lex(text).Select(t => t.Value));
    }

    [Theory]
    [InlineData("\"abc")]
    [InlineData("\"abc\\")]
    [InlineData("\"abc\\\"")]
    [InlineData("\"a, b # c")]
    public void String_Unclosed_RestIsBad(string text) {
        Assert.Equal($"id:x bad:{text}", Lex("x " + text));
    }

    [Fact]
    public void String_FollowedByTokens() {
        Assert.Equal("dir:ascii str:a , str:b", Lex(".ascii \"a\",\"b\""));
    }
}

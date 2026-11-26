using PlusPim.Instructions;
using System.Collections.Immutable;
using System.Diagnostics;

namespace PlusPim.Dbg;

/// <summary>
/// メモリを除く状態
/// </summary>
/// <remarks>
/// 代入で複製できる値型で, 履歴に自動的に反映される.
/// </remarks>
public struct Core {
    public Registers Registers;

    /// <summary>ライブフレームを含まないコールスタック. 先頭が最も新しいフレーム</summary>
    public ImmutableStack<Frame> CallStack { readonly get; private set; } = ImmutableStack<Frame>.Empty;

    /// <summary><see cref="CallStack"/> のフレームの数</summary>
    public int Depth { readonly get; private set; }

    public Status Status { readonly get; internal set; } = new Running();

    /// <summary>直前に実行した分岐. 値があれば次に実行する命令は分岐遅延スロット上である</summary>
    public Branch? Branch { readonly get; internal set; }

    /// <summary>直前の命令が始めたロード. 値があれば次に実行する命令はロード遅延スロット上である</summary>
    public DelayedLoad? Load { readonly get; internal set; }

    /// <summary><see cref="InputTape"/> の次に読む位置</summary>
    internal int InputCursor;

    public Core() { }

    public readonly Address Pc => this.Registers.Pc;

    internal void Push(Frame frame) {
        this.CallStack = this.CallStack.Push(frame);
        this.Depth++;
    }

    private Frame Pop() {
        this.CallStack = this.CallStack.Pop(out Frame frame);
        this.Depth--;
        return frame;
    }

    /// <summary>
    /// 先頭が呼び出しのフレームで, その戻り先が <paramref name="target"/> なら取り出す
    /// </summary>
    /// <param name="target">ジャンプ先</param>
    /// <param name="link">呼び出した命令から戻り先までのバイト数</param>
    internal void PopCallReturningTo(Address target, uint link) {
        if(!this.CallStack.IsEmpty && this.CallStack.Peek() is { Code: null } f && f.CallSite + link == target) {
            _ = this.Pop();
        }
    }

    /// <summary>
    /// 最も新しい例外のフレームまでを取り出す
    /// </summary>
    internal void PopException() {
        while(!this.CallStack.IsEmpty) {
            if(this.Pop().Code is not null) {
                return;
            }
        }
        // カーネル空間に入るときは必ず例外フレームがあるはずである
        throw new UnreachableException("eret without an exception frame.");
    }
}

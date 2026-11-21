using PlusPim.Instructions;
using System.Collections.Immutable;
using System.Diagnostics;

namespace PlusPim.Dbg;

/// <summary>
/// デバッガの可変状態
/// </summary>
/// <remarks>
/// 再実行で履歴を生成するので状態はここに集約すること.
/// 状態を足すときは <see cref="Fork"/> にも足すこと.
/// </remarks>
internal sealed class State(Memory memory) {
    public Registers Registers;
    public Memory Memory { get; } = memory;
    /// <summary>先頭が最も新しいフレーム</summary>
    public ImmutableStack<Frame> CallStack { get; private set; } = ImmutableStack<Frame>.Empty;
    public int Depth { get; private set; }
    public Status Status = new Running();
    /// <summary><see cref="InputTape"/> の次に読む位置</summary>
    public int InputCursor;

    public void Push(Frame frame) {
        this.CallStack = this.CallStack.Push(frame);
        this.Depth++;
    }

    public Frame Pop() {
        this.CallStack = this.CallStack.Pop(out Frame frame);
        this.Depth--;
        return frame;
    }

    /// <summary>
    /// 先頭が呼び出しのフレームで, その戻り先が <paramref name="target"/> なら取り出す
    /// </summary>
    public void PopCallReturningTo(Address target) {
        if(!this.CallStack.IsEmpty && this.CallStack.Peek() is { Code: null } f && f.CallSite + 4 == target) {
            _ = this.Pop();
        }
    }

    /// <summary>
    /// 最も新しい例外のフレームまでを取り出す
    /// </summary>
    public void PopException() {
        while(!this.CallStack.IsEmpty) {
            if(this.Pop().Code is not null) {
                return;
            }
        }
        // カーネル空間に入るときは必ず例外フレームがあるはずである
        throw new UnreachableException("eret without an exception frame.");
    }

    public State Fork() {
        Debug.Assert(this.Depth == this.CallStack.Count());
        return new State(this.Memory.Fork()) {
            Registers = this.Registers,
            CallStack = this.CallStack,
            Depth = this.Depth,
            Status = this.Status,
            InputCursor = this.InputCursor,
        };
    }
}

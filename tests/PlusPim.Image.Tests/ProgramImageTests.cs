using PlusPim.Instructions;
using PlusPim.Instructions.Instructions;

namespace PlusPim.Image.Tests;

public class ProgramImageTests {
    private static readonly Address T = MemoryMap.Text.Base;
    private static readonly Address D = MemoryMap.Data.Base;

    private static IExecute[] Code(int n) {
        return [.. Enumerable.Range(0, n).Select(_ => new Syscall())];
    }

    private static ProgramImage Create((Address, IExecute[])[] text, params (Address, ReadOnlyMemory<byte>)[] data) {
        return ProgramImage.Create(T, text, data);
    }

    [Fact]
    public void Fetch_FindsInstructionsAcrossSections() {
        IExecute[] a = Code(2);
        IExecute[] b = Code(1);
        ProgramImage image = Create([(T + 0x100u, b), (T, a)]);

        Assert.Same(a[0], image.Fetch(T));
        Assert.Same(a[1], image.Fetch(T + 4u));
        Assert.Same(b[0], image.Fetch(T + 0x100u));
    }

    [Fact]
    public void Fetch_ReturnsNullWhereNoInstructionStarts() {
        ProgramImage image = Create([(T + 8u, Code(2))]);

        Assert.Null(image.Fetch(T));            // 最初の区間より前
        Assert.Null(image.Fetch(T + 9u));       // 非整列
        Assert.Null(image.Fetch(T + 16u));      // 区間の末尾
        Assert.Null(image.Fetch(MemoryMap.KText.Base));
    }

    [Fact]
    public void Create_AcceptsKernelText() {
        IExecute[] k = Code(1);
        Assert.Same(k[0], Create([(MemoryMap.KText.Base, k)]).Fetch(MemoryMap.KText.Base));
    }

    [Fact]
    public void Create_SortsDataAndDropsEmptySections() {
        ProgramImage image = Create([(T, [])], (D + 8u, new byte[] { 2 }), (D, new byte[] { 1 }), (D + 4u, ReadOnlyMemory<byte>.Empty));

        Assert.Equal([D, D + 8u], image.Data.Select(s => s.Base));
        Assert.Null(image.Fetch(T));
    }

    [Fact]
    public void Create_CopiesData() {
        byte[] bytes = [1, 2];
        ProgramImage image = Create([], (D, bytes));
        bytes[0] = 9;

        Assert.Equal([1, 2], image.Data[0].Bytes.ToArray());
    }

    [Fact]
    public void Create_RejectsBrokenInvariants() {
        Assert.Throws<ArgumentException>(() => Create([(T + 2u, Code(1))]));
        Assert.Throws<ArgumentException>(() => Create([(T, Code(2)), (T + 4u, Code(1))]));
        Assert.Throws<ArgumentException>(() => Create([(D, Code(1))]));
        Assert.Throws<ArgumentException>(() => Create([(MemoryMap.Text.End - 4u, Code(2))]));
        Assert.Throws<ArgumentException>(() => Create([], (T, new byte[] { 0 })));
        Assert.Throws<ArgumentException>(() => Create([], (D, new byte[4]), (D + 3u, new byte[1])));
        Assert.Throws<ArgumentException>(() => Create([], (MemoryMap.Data.End - 1u, new byte[2])));
    }

    [Fact]
    public void Create_AcceptsSectionsEndingAtSegmentEnd() {
        Assert.NotNull(Create([(MemoryMap.Text.End - 4u, Code(1))]).Fetch(MemoryMap.Text.End - 4u));
        Assert.Single(Create([], (MemoryMap.Data.End - 1u, new byte[1])).Data);
    }
}

namespace PlusPim.Image;

internal static class Sorted {
    /// <summary>
    /// 昇順の <paramref name="keys"/> で <paramref name="key"/> 以下の最大の要素の位置. なければ -1
    /// </summary>
    public static int Floor(uint[] keys, uint key) {
        int i = Array.BinarySearch(keys, key);
        return i >= 0 ? i : ~i - 1;
    }
}

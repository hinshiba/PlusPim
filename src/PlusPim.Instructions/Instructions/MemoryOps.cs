using PlusPim.Instructions.Formats;

namespace PlusPim.Instructions.Instructions;

public readonly struct Lb: ILoadOp {
    public static string Mnemonic => "lb";
    public static LoadKind Kind => LoadKind.B;
}

public readonly struct Lbu: ILoadOp {
    public static string Mnemonic => "lbu";
    public static LoadKind Kind => LoadKind.BU;
}

public readonly struct Lh: ILoadOp {
    public static string Mnemonic => "lh";
    public static LoadKind Kind => LoadKind.H;
}

public readonly struct Lhu: ILoadOp {
    public static string Mnemonic => "lhu";
    public static LoadKind Kind => LoadKind.HU;
}

public readonly struct Lw: ILoadOp {
    public static string Mnemonic => "lw";
    public static LoadKind Kind => LoadKind.W;
}

public readonly struct Lwl: ILoadOp {
    public static string Mnemonic => "lwl";
    public static LoadKind Kind => LoadKind.Left;
}

public readonly struct Lwr: ILoadOp {
    public static string Mnemonic => "lwr";
    public static LoadKind Kind => LoadKind.Right;
}

public readonly struct Sb: IStoreOp {
    public static string Mnemonic => "sb";
    public static StoreKind Kind => StoreKind.B;
}

public readonly struct Sh: IStoreOp {
    public static string Mnemonic => "sh";
    public static StoreKind Kind => StoreKind.H;
}

public readonly struct Sw: IStoreOp {
    public static string Mnemonic => "sw";
    public static StoreKind Kind => StoreKind.W;
}

public readonly struct Swl: IStoreOp {
    public static string Mnemonic => "swl";
    public static StoreKind Kind => StoreKind.Left;
}

public readonly struct Swr: IStoreOp {
    public static string Mnemonic => "swr";
    public static StoreKind Kind => StoreKind.Right;
}

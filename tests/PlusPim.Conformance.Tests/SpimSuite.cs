using PlusPim.Dbg;

namespace PlusPim.Conformance.Tests;

/// <summary>
/// SPIMのテストプログラムと実行の条件
/// </summary>
/// <param name="File"><c>third_party/spim/</c> のファイル名</param>
/// <param name="Slots">SPIMの起動のオプションに対応する遅延スロット</param>
/// <param name="Excluded">PlusPimで実行しない行</param>
internal sealed record SpimProgram(string File, DelaySlotsConfig Slots, Excluded[] Excluded);

/// <summary>
/// 実行するSPIMのテストプログラムの一覧
/// </summary>
/// <remarks>
/// 除外はPlusPimが対応しない命令と構文を含む <c>Testing ...</c> のブロック単位で行う.
/// 行番号は <c>third_party/spim/</c> の固定したリビジョンのものである.
/// </remarks>
internal static class SpimSuite {
    private const string Fpu = "CP1(FPU)の命令. PlusPimは対応しない";
    private const string NotMips1 = "MIPS-I(R3000)にない命令";
    private const string Coprocessor = "CP0のcfc/ctcとCP2, CP3. PlusPimは対応しない";
    private const string TextAccess = "PlusPimの仕様: テキストのアドレスへのロードとストアはTextAccessになる";
    private const string Misaligned = "非整列のジャンプ先. PlusPimはR3000と同じくジャンプ先の取り出しでAdELにするので, EPC + 4へ戻るハンドラでは非整列のまま進む";
    /// <summary>R3000と異なるPlusPimの挙動. 修正したら除外を外す</summary>
    private const string KsegAccess = "差分: ユーザーモードでの0x80000000以上へのロードとストアがAdEL, AdESでなくTextAccessになる";

    public static readonly SpimProgram[] Programs = [
        new("tt.core.s", DelaySlotsConfig.None, [
            new(57, 57, TextAccess),
            new(60, 60, TextAccess),
            new(82, 97, "CP0のCountレジスタ. PlusPimは対応しない"),
            new(98, 162, "複数の文字列をとる.asciizと, レジスタをとるjal. SPIMの方言"),
            new(432, 448, Coprocessor),
            new(449, 488, NotMips1 + ": clo, clz"),
            new(489, 572, "3オペランドのdiv, divu. 0除算を検査する疑似命令はPlusPimにない"),
            new(583, 591, ".ktextの命令が例外ベクタに置かれる"),
            new(636, 638, Misaligned),
            new(655, 657, Misaligned),
            new(660, 678, "オフセット付きのla. SPIMの方言"),
            new(679, 708, NotMips1 + ": ld"),
            new(733, 734, KsegAccess),
            new(758, 759, KsegAccess),
            new(762, 776, NotMips1 + ": ll, sc"),
            new(847, 848, KsegAccess),
            new(857, 980, NotMips1 + ": madd, maddu. 続くmfc1, mtc1は" + Fpu),
            new(1012, 1164, NotMips1 + ": movf, movn, movt, movz, msub, msubu"),
            new(1295, 1327, "mulo. PlusPimにない疑似命令"),
            new(1390, 1392, "SPIMのテストの誤り: 除外したSDのブロックのラベルsd_を表示する"),
            new(1402, 1506, NotMips1 + ": sd. 続くswc1, s.s, sdc1, s.dは" + Fpu),
            new(1740, 1749, NotMips1 + ": ssnop"),
            new(1832, 1833, KsegAccess),
            new(1835, 1838, TextAccess),
            new(1847, 1856, NotMips1 + ": sync"),
            new(1857, 1936, "浮動小数点数を扱うsyscall. " + Fpu),
            new(2014, 2025, "TLBの命令. PlusPimは対応しない"),
            new(2142, 4002, Fpu),
            new(4357, 4398, "li.d, li.s. " + Fpu),
            new(4492, 4547, "remu, rol, ror. PlusPimにない疑似命令"),
        ]),
        new("tt.le.s", DelaySlotsConfig.None, [
            new(89, 89, KsegAccess),
            new(132, 132, KsegAccess),
            new(172, 173, KsegAccess),
            new(207, 208, KsegAccess),
            new(244, 244, KsegAccess),
            new(272, 273, KsegAccess),
            new(498, 524, "非整列の.wordでの前方参照. PlusPimは値を捨てる"),
        ]),
        new("tt.bare.s", DelaySlotsConfig.All, [
            new(44, 60, "rfeで戻る例外ハンドラ. PlusPimは0x80000180のハンドラを使う"),
            new(78, 111, "bc1fl, bc1tl. " + Fpu),
            new(112, 349, NotMips1 + ": beql, bgezl, bgtzl, blezl, bltzl, bnel"),
            new(359, 364, "SPIMとR3000の差: ロード遅延スロットの命令がロード先へ書くと, SPIMはロードの値を残し, R3000は命令の値を残す. SSTのADDで確認"),
            new(388, 403, "3オペランドのdiv. 0除算を検査する疑似命令はPlusPimにない"),
        ]),
        new("tt.alu.bare.s", DelaySlotsConfig.All, [
            new(362, 362, "分岐の遅延スロットのj. MIPSで未定義で, PlusPimはTransferInDelaySlotにする. JALのテストが番地を直書きするのでnopで埋める", "nop"),
            new(1568, 1659, Coprocessor),
            new(1697, 1784, "lwc0, swc0とCP2, CP3. PlusPimは対応しない"),
            new(1829, 1852, "rfeで戻る例外ハンドラ. PlusPimは0x80000180のハンドラを使う"),
        ]),
    ];
}

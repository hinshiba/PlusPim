# MIPS 命令 対応状況チェックリスト

## R形式命令

### 算術・論理演算

- [x] `add` — Add (rd = rs + rt, オーバーフロー例外)
- [x] `addu` — Add Unsigned (rd = rs + rt)
- [x] `sub` — Subtract (rd = rs - rt, オーバーフロー例外)
- [x] `subu` — Subtract Unsigned (rd = rs - rt)
- [x] `and` — AND (rd = rs & rt)
- [x] `or` — OR (rd = rs | rt)
- [x] `xor` — XOR (rd = rs ^ rt)
- [x] `nor` — NOR (rd = ~(rs | rt))

### シフト

- [x] `sll` — Shift Left Logical (rd = rt << shamt)
- [x] `srl` — Shift Right Logical (rd = rt >>> shamt)
- [x] `sra` — Shift Right Arithmetic (rd = rt >> shamt)
- [x] `sllv` — Shift Left Logical Variable (rd = rt << rs)
- [x] `srlv` — Shift Right Logical Variable (rd = rt >>> rs)
- [x] `srav` — Shift Right Arithmetic Variable (rd = rt >> rs)

### 比較

- [x] `slt` — Set on Less Than (rd = (rs < rt) ? 1 : 0, 符号付き)
- [x] `sltu` — Set on Less Than Unsigned (rd = (rs < rt) ? 1 : 0, 符号なし)

### 乗除算

- [x] `mult` — Multiply (Hi, Lo = rs * rt, 符号付き)
- [ ] `multu` — Multiply Unsigned (Hi, Lo = rs * rt, 符号なし)
- [x] `div` — Divide (Lo = rs / rt, Hi = rs % rt, 符号付き)
- [ ] `divu` — Divide Unsigned (Lo = rs / rt, Hi = rs % rt, 符号なし)

### Hi/Lo レジスタ操作

- [x] `mfhi` — Move From Hi (rd = Hi)
- [x] `mflo` — Move From Lo (rd = Lo)
- [x] `mthi` — Move To Hi (Hi = rs)
- [x] `mtlo` — Move To Lo (Lo = rs)

### ジャンプ (レジスタ)

- [x] `jr` — Jump Register (PC = rs)
- [ ] `jalr` — Jump and Link Register (rd = PC+8, PC = rs)

### システム

- [x] `syscall` — System Call
- [ ] `break` — Breakpoint

### トラップ

- [ ] `tge` — Trap if Greater or Equal (符号付き)
- [ ] `tgeu` — Trap if Greater or Equal Unsigned
- [ ] `tlt` — Trap if Less Than (符号付き)
- [ ] `tltu` — Trap if Less Than Unsigned
- [ ] `teq` — Trap if Equal
- [ ] `tne` — Trap if Not Equal

---

## I形式命令 (Immediate)

### 算術・論理演算 (即値)

- [x] `addi` — Add Immediate (rt = rs + imm, オーバーフロー例外)
- [x] `addiu` — Add Immediate Unsigned (rt = rs + imm)
- [x] `andi` — AND Immediate (rt = rs & imm, ゼロ拡張)
- [x] `ori` — OR Immediate (rt = rs | imm, ゼロ拡張)
- [x] `xori` — XOR Immediate (rt = rs ^ imm, ゼロ拡張)
- [x] `lui` — Load Upper Immediate (rt = imm << 16)

### 比較 (即値)

- [x] `slti` — Set on Less Than Immediate (rt = (rs < imm) ? 1 : 0, 符号付き)
- [x] `sltiu` — Set on Less Than Immediate Unsigned

### 分岐

- [x] `beq` — Branch on Equal (if rs == rt)
- [x] `bne` — Branch on Not Equal (if rs != rt)
- [ ] `bgez` — Branch on Greater or Equal Zero (if rs >= 0)
- [ ] `bgtz` — Branch on Greater Than Zero (if rs > 0)
- [ ] `blez` — Branch on Less or Equal Zero (if rs <= 0)
- [ ] `bltz` — Branch on Less Than Zero (if rs < 0)
- [ ] `bgezal` — Branch on >= Zero and Link
- [ ] `bltzal` — Branch on < Zero and Link

### ロード

- [ ] `lb` — Load Byte (符号拡張)
- [ ] `lbu` — Load Byte Unsigned (ゼロ拡張)
- [ ] `lh` — Load Halfword (符号拡張)
- [ ] `lhu` — Load Halfword Unsigned (ゼロ拡張)
- [x] `lw` — Load Word
- [ ] `lwl` — Load Word Left
- [ ] `lwr` — Load Word Right

### ストア

- [ ] `sb` — Store Byte
- [ ] `sh` — Store Halfword
- [x] `sw` — Store Word
- [ ] `swl` — Store Word Left
- [ ] `swr` — Store Word Right

### トラップ (即値)

- [ ] `tgei` — Trap if Greater or Equal Immediate
- [ ] `tgeiu` — Trap if Greater or Equal Immediate Unsigned
- [ ] `tlti` — Trap if Less Than Immediate
- [ ] `tltiu` — Trap if Less Than Immediate Unsigned
- [ ] `teqi` — Trap if Equal Immediate
- [ ] `tnei` — Trap if Not Equal Immediate

---

## J形式命令 (Jump)

- [x] `j` — Jump (PC = target)
- [x] `jal` — Jump and Link (ra = PC+8, PC = target)

---

## 疑似命令 (Pseudo Instructions)

### データ移動

- [x] `move` — Move (rd = rs) → `addu rd, rs, $zero`
- [x] `li` — Load Immediate → `lui` + `ori`
- [x] `la` — Load Address → `lui` + `ori`

### 算術

- [ ] `mul` — Multiply (rd = rs * rt) → `mult` + `mflo`
- [ ] `div` (3オペランド) — Divide (rd = rs / rt) → `div` + `mflo`
- [ ] `rem` — Remainder (rd = rs % rt) → `div` + `mfhi`
- [ ] `abs` — Absolute Value
- [ ] `neg` — Negate (rd = -rs) → `sub rd, $zero, rs`
- [ ] `negu` — Negate Unsigned → `subu rd, $zero, rs`

### 論理

- [ ] `not` — NOT (rd = ~rs) → `nor rd, rs, $zero`

### 分岐

- [ ] `b` — Branch Unconditional → `beq $zero, $zero, label`
- [ ] `bal` — Branch and Link → `bgezal $zero, label`
- [ ] `beqz` — Branch if Equal Zero → `beq rs, $zero, label`
- [ ] `bnez` — Branch if Not Equal Zero → `bne rs, $zero, label`
- [ ] `bge` — Branch if Greater or Equal (符号付き)
- [ ] `bgeu` — Branch if Greater or Equal Unsigned
- [ ] `bgt` — Branch if Greater Than (符号付き)
- [ ] `bgtu` — Branch if Greater Than Unsigned
- [ ] `ble` — Branch if Less or Equal (符号付き)
- [ ] `bleu` — Branch if Less or Equal Unsigned
- [ ] `blt` — Branch if Less Than (符号付き)
- [ ] `bltu` — Branch if Less Than Unsigned

### 比較・セット

- [ ] `seq` — Set if Equal
- [ ] `sne` — Set if Not Equal
- [ ] `sge` — Set if Greater or Equal (符号付き)
- [ ] `sgeu` — Set if Greater or Equal Unsigned
- [ ] `sgt` — Set if Greater Than (符号付き)
- [ ] `sgtu` — Set if Greater Than Unsigned
- [ ] `sle` — Set if Less or Equal (符号付き)
- [ ] `sleu` — Set if Less or Equal Unsigned

### ロード

- [ ] `ulh` — Unaligned Load Halfword
- [ ] `ulhu` — Unaligned Load Halfword Unsigned
- [ ] `ulw` — Unaligned Load Word
- [ ] `ush` — Unaligned Store Halfword
- [ ] `usw` — Unaligned Store Word

### その他

- [x] `nop` — No Operation → `sll $zero, $zero, 0`

---

## アセンブラ指令 (Assembler Directives)

### セクション

- [x] `.text` — テキスト(コード)セクション開始
- [x] `.data` — データセクション開始
- [x] `.ktext` — カーネルテキストセクション開始
- [x] `.kdata` — カーネルデータセクション開始

### データ定義

- [x] `.byte` — バイトデータ定義
- [ ] `.half` — ハーフワード(2バイト)データ定義
- [x] `.word` — ワード(4バイト)データ定義
- [x] `.ascii` — ASCII文字列 (NULLなし)
- [x] `.asciiz` — ASCII文字列 (NULL終端)
- [x] `.space` — 領域確保 (バイト数指定)

### アラインメント・配置

- [x] `.align` — アラインメント指定
- [ ] `.globl` — グローバルシンボル宣言

### マクロ・制御

- [ ] `.macro` — マクロ定義開始
- [ ] `.end_macro` — マクロ定義終了
- [ ] `.eqv` — シンボル定数定義
- [ ] `.include` — ファイルインクルード
- [ ] `.set` — アセンブラオプション設定 (e.g., `noat`, `noreorder`)
- [ ] `.extern` — 外部シンボル宣言

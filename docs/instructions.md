# MIPS 命令 対応状況チェックリスト

ハイフンの入ったチェックリストは実装予定がないことを示します．

## 実命令

### R形式命令

#### 算術・論理演算

- [x] `add`: Add (rd = rs + rt, オーバーフロー例外)
- [x] `addu`: Add Unsigned (rd = rs + rt)
- [x] `sub`: Subtract (rd = rs - rt, オーバーフロー例外)
- [x] `subu`: Subtract Unsigned (rd = rs - rt)
- [x] `and`: AND (rd = rs & rt)
- [x] `or`: OR (rd = rs | rt)
- [x] `xor`: XOR (rd = rs ^ rt)
- [x] `nor`: NOR (rd = ~(rs | rt))

#### シフト

- [x] `sll`: Shift Left Logical (rd = rt << shamt)
- [x] `srl`: Shift Right Logical (rd = rt >>> shamt)
- [x] `sra`: Shift Right Arithmetic (rd = rt >> shamt)
- [x] `sllv`: Shift Left Logical Variable (rd = rt << rs)
- [x] `srlv`: Shift Right Logical Variable (rd = rt >>> rs)
- [x] `srav`: Shift Right Arithmetic Variable (rd = rt >> rs)

#### 比較

- [x] `slt`: Set on Less Than (rd = (rs < rt) ? 1 : 0, 符号付き)
- [x] `sltu`: Set on Less Than Unsigned (rd = (rs < rt) ? 1 : 0, 符号なし)

#### 乗除算

- [x] `mult`: Multiply (Hi, Lo = rs * rt, 符号付き)
- [x] `multu`: Multiply Unsigned (Hi, Lo = rs * rt, 符号なし)
- [x] `div`: Divide (Lo = rs / rt, Hi = rs % rt, 符号付き)
- [x] `divu`: Divide Unsigned (Lo = rs / rt, Hi = rs % rt, 符号なし)

#### Hi/Lo レジスタ操作

- [x] `mfhi`: Move From Hi (rd = Hi)
- [x] `mflo`: Move From Lo (rd = Lo)
- [x] `mthi`: Move To Hi (Hi = rs)
- [x] `mtlo`: Move To Lo (Lo = rs)

#### ジャンプ (レジスタ)

- [x] `jr`: Jump Register (PC = rs)
- [x] `jalr`: Jump and Link Register (rd = PC+4, PC = rs. 遅延スロットなし. `jalr rs` は rd = `$ra`)

#### システム

- [x] `syscall`: System Call
- [x] `break`: Breakpoint

#### コプロセッサ0 (例外処理)

- [x] `mfc0`: Move From Coprocessor 0 (rt = CP0[rd])
- [x] `mtc0`: Move To Coprocessor 0 (CP0[rd] = rt)
- [x] `eret`: Exception Return (PC = EPC, ユーザーモードへ復帰)

#### トラップ

- [-] `tge`: Trap if Greater or Equal (符号付き)
- [-] `tgeu`: Trap if Greater or Equal Unsigned
- [-] `tlt`: Trap if Less Than (符号付き)
- [-] `tltu`: Trap if Less Than Unsigned
- [-] `teq`: Trap if Equal
- [-] `tne`: Trap if Not Equal

---

### I形式命令

#### 算術・論理演算

- [x] `addi`: Add Immediate (rt = rs + imm, オーバーフロー例外)
- [x] `addiu`: Add Immediate Unsigned (rt = rs + imm)
- [x] `andi`: AND Immediate (rt = rs & imm, ゼロ拡張)
- [x] `ori`: OR Immediate (rt = rs | imm, ゼロ拡張)
- [x] `xori`: XOR Immediate (rt = rs ^ imm, ゼロ拡張)
- [x] `lui`: Load Upper Immediate (rt = imm << 16)

#### 比較

- [x] `slti`: Set on Less Than Immediate (rt = (rs < imm) ? 1 : 0, 符号付き)
- [x] `sltiu`: Set on Less Than Immediate Unsigned

#### 分岐

- [x] `beq`: Branch on Equal (if rs == rt)
- [x] `bne`: Branch on Not Equal (if rs != rt)
- [x] `bgez`: Branch on Greater or Equal Zero (if rs >= 0, 符号付き比較, 遅延スロットなし)
- [x] `bgtz`: Branch on Greater Than Zero (if rs > 0, 符号付き比較, 遅延スロットなし)
- [x] `blez`: Branch on Less or Equal Zero (if rs <= 0, 符号付き比較, 遅延スロットなし)
- [x] `bltz`: Branch on Less Than Zero (if rs < 0, 符号付き比較, 遅延スロットなし)
- [ ] `bgezal`: Branch on >= Zero and Link
- [ ] `bltzal`: Branch on < Zero and Link

#### ロード

- [x] `lb`: Load Byte (符号拡張)
- [x] `lbu`: Load Byte Unsigned (ゼロ拡張)
- [x] `lh`: Load Halfword (符号拡張)
- [x] `lhu`: Load Halfword Unsigned (ゼロ拡張)
- [x] `lw`: Load Word
- [x] `lwl`: Load Word Left (リトルエンディアン, アラインメント例外なし)
- [x] `lwr`: Load Word Right (リトルエンディアン, アラインメント例外なし)

#### ストア

- [x] `sb`: Store Byte
- [x] `sh`: Store Halfword
- [x] `sw`: Store Word
- [x] `swl`: Store Word Left (リトルエンディアン, アラインメント例外なし)
- [x] `swr`: Store Word Right (リトルエンディアン, アラインメント例外なし)

#### トラップ

- [-] `tgei`: Trap if Greater or Equal Immediate
- [-] `tgeiu`: Trap if Greater or Equal Immediate Unsigned
- [-] `tlti`: Trap if Less Than Immediate
- [-] `tltiu`: Trap if Less Than Immediate Unsigned
- [-] `teqi`: Trap if Equal Immediate
- [-] `tnei`: Trap if Not Equal Immediate

---

### J形式命令

- [x] `j`: Jump (PC = target)
- [x] `jal`: Jump and Link (ra = PC+4, PC = target. 遅延スロットなし)

---

## 疑似命令

### データ移動

- [x] `move`: Move (rd = rs) → `addu rd, rs, $zero`
- [x] `li`: Load Immediate → `lui` + `ori`
- [x] `la`: Load Address → `lui` + `ori`

### 算術

- [x] `mul`: Multiply (rd = rs * rt) → `mult` + `mflo`
- [-] `div`: (3オペランド) - Divide (rd = rs / rt)
- [ ] `divi`: (3オペランド) - Divide (rd = rs / rt) → `div` + `mflo`
- [x] `rem`: Remainder (rd = rs % rt) → `div` + `mfhi`
- [ ] `abs`: Absolute Value
- [x] `neg`: Negate (rd = -rs) → `sub rd, $zero, rs`
- [x] `negu`: Negate Unsigned → `subu rd, $zero, rs`

### 論理

- [x] `not`: NOT (rd = ~rs) → `nor rd, rs, $zero`

### 分岐

- [x] `b`: Branch Unconditional → `beq $zero, $zero, label`
- [ ] `bal`: Branch and Link → `bgezal $zero, label`
- [x] `beqz`: Branch if Equal Zero → `beq rs, $zero, label`
- [x] `bnez`: Branch if Not Equal Zero → `bne rs, $zero, label`
- [x] `bge`: Branch if Greater or Equal (符号付き)
- [x] `bgeu`: Branch if Greater or Equal Unsigned
- [x] `bgt`: Branch if Greater Than (符号付き)
- [x] `bgtu`: Branch if Greater Than Unsigned
- [x] `ble`: Branch if Less or Equal (符号付き)
- [x] `bleu`: Branch if Less or Equal Unsigned
- [x] `blt`: Branch if Less Than (符号付き)
- [x] `bltu`: Branch if Less Than Unsigned

### 比較・セット

- [x] `seq`: Set if Equal
- [x] `sne`: Set if Not Equal
- [x] `sge`: Set if Greater or Equal (符号付き)
- [x] `sgeu`: Set if Greater or Equal Unsigned
- [x] `sgt`: Set if Greater Than (符号付き)
- [x] `sgtu`: Set if Greater Than Unsigned
- [x] `sle`: Set if Less or Equal (符号付き)
- [x] `sleu`: Set if Less or Equal Unsigned

### ロード

- [ ] `ulh`: Unaligned Load Halfword
- [ ] `ulhu`: Unaligned Load Halfword Unsigned
- [ ] `ulw`: Unaligned Load Word
- [ ] `ush`: Unaligned Store Halfword
- [ ] `usw`: Unaligned Store Word

### その他

- [x] `clear`: Clear (rt = 0) → `addu rt, $zero, $zero`
- [x] `nop`: No Operation → `sll $zero, $zero, 0`

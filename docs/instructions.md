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
- [x] `jalr`: Jump and Link Register (rd = PC+4, PC = rs. `jalr rs` は rd = `$ra`)

#### システム

- [x] `syscall`: System Call
- [x] `break`: Breakpoint

#### コプロセッサ0 (例外処理)

- [x] `mfc0`: Move From Coprocessor 0 (rt = CP0[rd])
- [x] `mtc0`: Move To Coprocessor 0 (CP0[rd] = rt)
- [x] `eret`: Exception Return (PC = EPC, ユーザーモードへ復帰)

#### トラップ

- [x] `tge`: Trap if Greater or Equal (符号付き)
- [x] `tgeu`: Trap if Greater or Equal Unsigned
- [x] `tlt`: Trap if Less Than (符号付き)
- [x] `tltu`: Trap if Less Than Unsigned
- [x] `teq`: Trap if Equal
- [x] `tne`: Trap if Not Equal

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
- [x] `bgez`: Branch on Greater or Equal Zero (if rs >= 0, 符号付き)
- [x] `bgtz`: Branch on Greater Than Zero (if rs > 0, 符号付き)
- [x] `blez`: Branch on Less or Equal Zero (if rs <= 0, 符号付き)
- [x] `bltz`: Branch on Less Than Zero (if rs < 0, 符号付き)
- [x] `bgezal`: Branch on >= Zero and Link (ra = PC+4, if rs >= 0. 不成立でもraを書く)
- [x] `bltzal`: Branch on < Zero and Link (ra = PC+4, if rs < 0. 不成立でもraを書く)

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

- [x] `tgei`: Trap if Greater or Equal Immediate
- [x] `tgeiu`: Trap if Greater or Equal Immediate Unsigned
- [x] `tlti`: Trap if Less Than Immediate
- [x] `tltiu`: Trap if Less Than Immediate Unsigned
- [x] `teqi`: Trap if Equal Immediate
- [x] `tnei`: Trap if Not Equal Immediate

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
- [x] `divi`: (3オペランド) - Divide (rd = rs / rt) → `div` + `mflo`
- [x] `rem`: Remainder (rd = rs % rt) → `div` + `mfhi`
- [x] `abs`: Absolute Value → `sra $at, rs, 31` + `xor rd, rs, $at` + `subu rd, rd, $at` (INT_MINはINT_MINのまま. 例外なし)
- [x] `neg`: Negate (rd = -rs) → `sub rd, $zero, rs`
- [x] `negu`: Negate Unsigned → `subu rd, $zero, rs`

### 論理

- [x] `not`: NOT (rd = ~rs) → `nor rd, rs, $zero`

### 分岐

- [x] `b`: Branch Unconditional → `beq $zero, $zero, label`
- [x] `bal`: Branch and Link → `bgezal $zero, label`
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

- [x] `ulh`: Unaligned Load Halfword → `lbu $at, off(base)` + `lb rt, off+1(base)` + `sll rt, rt, 8` + `or rt, rt, $at`
- [x] `ulhu`: Unaligned Load Halfword Unsigned → `ulh`の`lb`を`lbu`にしたもの
- [x] `ulw`: Unaligned Load Word → `lwl rt, off+3(base)` + `lwr rt, off(base)`. rtがbaseと同じなら`$at`に読んで`addu rt, $at, $zero`
- [x] `ush`: Unaligned Store Halfword → `sb rt, off(base)` + `srl $at, rt, 8` + `sb $at, off+1(base)`
- [x] `usw`: Unaligned Store Word → `swl rt, off+3(base)` + `swr rt, off(base)`

`ulh`，`ulhu`，`ulw`，`ush`，`usw`のオフセットは，展開で足す値を含めて符号付き16bitに収まらなければならない．

### その他

- [x] `clear`: Clear (rt = 0) → `addu rt, $zero, $zero`
- [x] `nop`: No Operation → `sll $zero, $zero, 0`

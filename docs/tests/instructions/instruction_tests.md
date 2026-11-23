# MIPS 命令 レベルテスト仕様

特に記載がない限り，テスト用パラメータは [テストパラメータ一覧](values.md) を，
共通の振る舞いは [実行モデルと確認事項](execution_model.md)を参照する．

テストパラメータを適用することで，振る舞いが直ちに理解できる場合は記載していない．

ただし例外の発生条件を除く．

空のチェックリストの命令は現時点では実装予定がない命令である．
これらのテストは免除する．

## R形式命令

### 算術演算

- `add $rd $rs $rt`
- `sub $rd $rs $rt`
- `addu $rd $rs $rt`
- `subu $rd $rs $rt`

#### 振る舞い

異常系: `Ov`
- `add`，`sub`: 算術オーバーフロー発生時

### 論理演算

- `and $rd $rs $rt`
- `or $rd $rs $rt`
- `xor $rd $rs $rt`
- `nor $rd $rs $rt`

### シフト

- `sll $rd $rt shamt`
- `srl $rd $rt shamt`
- `sra $rd $rt shamt`
- `sllv $rd $rt $rs`
- `srlv $rd $rt $rs`
- `srav $rd $rt $rs`

### 比較

- `slt $rd $rs $rt`
- `sltu $rd $rs $rt`

### 乗除算

- `mult $rs $rt`
- `multu $rs $rt`
- `div $rs $rt`
- `divu $rs $rt`

**注**: ゼロ除算における仕様は教育目的を考え不定とせず，ランタイムエラー `DivisionByZero` を発生させる．
`div` の `0x80000000 / -1`は MIPS32 の定義どおり LO = `0x80000000`，HI = `0` とする．

### Hi/Lo レジスタ操作

- `mfhi $rd`
- `mflo $rd`
- `mthi $rs`
- `mtlo $rs`

代表値/境界値をすべて用いない(バグ混入が低リスクであるため)

### ジャンプ

- `jr $rs`
- [ ] `jalr`

レジスタであるがジャンプに準ずる値でテストすること．

[ジャンプの振る舞いモデル](jump_model.md) を追加で参照せよ．

### システム

- `syscall`: 異常系 `Sys` のみ
- `break`: 異常系 `Bp` のみ

### CP0 操作

- `mfc0 $rt n`
- `mtc0 $rt n`
- `eret`


### トラップ

- `tge $rs $rt`
- `tgeu $rs $rt`
- `tlt $rs $rt`
- `tltu $rs $rt`
- `teq $rs $rt`
- `tne $rs $rt`

異常系: `Tr`
条件が成立した場合．レジスタは変化しない

---

## I形式命令

### 算術演算

- `addi $rt $rs imm`
- `addiu $rt $rs imm`

異常系: `Ov`
`addi`: 算術オーバーフロー発生時

### 論理演算

- `andi $rt $rs imm`
- `ori $rt $rs imm`
- `xori $rt $rs imm`

### 即値代入

- `lui $rt imm`

検証
- 下位16bitが0になること

### 比較

- `slti $rt $rs imm`
- `sltiu $rt $rs imm`

### 分岐

- `beq $rs $rt offset`
- `bne $rs $rt offset`
- `bgez $rs offset`
- `bgtz $rs offset`
- `blez $rs offset`
- `bltz $rs offset`
- `bgezal $rs offset`
- `bltzal $rs offset`

`beq`，`bne`は代表値をすべて用いない(バグ混入が低リスクより)

`bgezal`，`bltzal`は成否によらず`$ra`に`PC + 4`を書く．成立時はサブルーチンの呼び出しとしてコールスタックに積む．
[ジャンプの振る舞いモデル](jump_model.md)を追加で参照せよ．

### ロード

- `lb $rt offset($rs)`
- `lbu $rt offset($rs)`
- `lh $rt offset($rs)`
- `lhu $rt offset($rs)`
- `lw $rt offset($rs)`
- `lwl $rt offset($rs)`
- `lwr $rt offset($rs)`

[メモリアクセスの振る舞いモデル](mem_model.md) を追加で参照せよ．

異常系: `AdEL`
`lh`，`lhu`，`lw`: アライメント違反発生時

異常系: ランタイムエラー `TextAccess`
テキストかカーネルテキストのアドレスを読む場合．アライメント違反では `AdEL` が優先される

### ストア

- `sb $rt offset($rs)`
- `sh $rt offset($rs)`
- `sw $rt offset($rs)`
- `swl $rt offset($rs)`
- `swr $rt offset($rs)`

[メモリアクセスの振る舞いモデル](mem_model.md) を追加で参照せよ．

異常系: `AdES`
`sh`，`sw`: アライメント違反発生時

異常系: ランタイムエラー `TextAccess`
テキストかカーネルテキストのアドレスに書く場合．アライメント違反では `AdES` が優先される．メモリは変化しない

### トラップ (即値)

- `tgei $rs imm`
- `tgeiu $rs imm`
- `tlti $rs imm`
- `tltiu $rs imm`
- `teqi $rs imm`
- `tnei $rs imm`

即値は常に符号拡張する．`tgeiu`，`tltiu`は符号拡張した即値と符号なしで比べる

異常系: `Tr`
条件が成立した場合．レジスタは変化しない

---

## J形式命令

- `j $target`
- `jal $target`

[ジャンプの振る舞いモデル](jump_model.md) を追加で参照せよ．

---

## ランタイム

- `runtime_call!`

[ランタイムコールの振る舞いモデル](runtime_call_model.md) を参照

異常系: `CpU`
`runtime_call!`: ユーザーモードから呼び出した場合
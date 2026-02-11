# PlusPim 残課題と詳細タスク分割

## リファクタリング

包括的なリファクタリングの実施を計画せよ．
つぎの全てを検討せよ
* 不要抽象化の廃止
	* 実行コンテキストインターフェースの廃止: そもそも実行コンテキストはデバッガ固有なのでインターフェース不要
	* Mnemonicクラスの廃止: IInstructionを実装しているものが命令であるとしたほうがシンプルである
* プリミティブ信仰の撤廃
	* 汎用レジスタのint[]からの脱却 (クラスorレコード化)
		* RegisterIDのファイル場所もついでに改善
	* excecutionIndex: PCとインデックスどちらも持つクラスにして必要に応じて取得できるようにすべき
		* PC計算のカプセル化も兼ねる
	* ラベルのstringからの脱却: シンボル名と配置場所と指す命令インデックスを同時に管理
* Applicationからの漏出阻止
	* Continue() や ReverseContinue()のループをApplicationへ移動
	* フレームIDからフレームを検索するロジック
* 重複実装の削減
	* ParsedProgramとExcecutionContextがどちらもラベル解決を持っている -> ExcecutionContextに統一
	* ReadRsとReadRtがRTypeInstructionとBranchInstructionで重複 -> 拡張メソッド化
	* PlusPimDbg内でLoad完了判断のためのNullチェックが散財 -> Load時にコンストラクトされるようにアプリケーションで調整
	* 現在は重複ではないがJrInstructionがTryParseSingleRegOperand持っている．しかしまだ追加していない命令でこれを用いるものがある
ここにないもので改善点があれば追加で上げよ

## 実装順序


### メモリアクセス命令

### 命令の追加実装
疑似命令以外はすべて実装するつもりで

### .data セクション対応
15. .data セクションのパース
16. 文字列リテラル（.asciiz）のメモリ配置
17. syscall 命令の基盤
18. print_string (syscall 4) の実装

### syscall
print_string

---

## 課題1: 未実装の命令

### 1-2. R形式命令の追加（3レジスタ形式）
| タスク          | ファイル                                       | 見積難易度 |
| --------------- | ---------------------------------------------- | ---------- |
| nor 命令の実装  | `Instructions/RType/NorInstruction.cs` (新規)  | 小         |
| xor 命令の実装  | `Instructions/RType/XorInstruction.cs` (新規)  | 小         |
| subu 命令の実装 | `Instructions/RType/SubuInstruction.cs` (新規) | 小         |

### 1-3. R形式命令（シフト命令・特殊形式）
シフト命令は `$rd, $rt, shamt` 形式（3レジスタではない）

| タスク                                  | ファイル                                      | 見積難易度 |
| --------------------------------------- | --------------------------------------------- | ---------- |
| srl 命令の実装                          | `Instructions/RType/SrlInstruction.cs` (新規) | 小         |
| sra 命令の実装                          | `Instructions/RType/SraInstruction.cs` (新規) | 小         |
| sllv/srlv/srav 命令の実装（オプション） | 各ファイル                                    | 小         |

### 1-4. R形式命令（乗除算・特殊レジスタ）
| タスク                       | ファイル                                       | 見積難易度 |
| ---------------------------- | ---------------------------------------------- | ---------- |
| mult 命令の実装（HI/LO使用） | `Instructions/RType/MultInstruction.cs` (新規) | 中         |
| div 命令の実装（HI/LO使用）  | `Instructions/RType/DivInstruction.cs` (新規)  | 中         |
| mfhi 命令の実装              | `Instructions/RType/MfhiInstruction.cs` (新規) | 小         |
| mflo 命令の実装              | `Instructions/RType/MfloInstruction.cs` (新規) | 小         |

### 1-5. I形式命令の基盤
| タスク                                   | ファイル                                        | 見積難易度 |
| ---------------------------------------- | ----------------------------------------------- | ---------- |
| ITypeInstruction 基底クラスの作成        | `Instructions/IType/ITypeInstruction.cs` (新規) | 中         |
| `$rt, $rs, immediate` 形式のパーサー実装 | 同上                                            | 中         |

### 1-6. I形式命令（算術・論理）
| タスク                | ファイル                                        | 見積難易度 |
| --------------------- | ----------------------------------------------- | ---------- |
| addi 命令の実装       | `Instructions/IType/AddiInstruction.cs` (新規)  | 小         |
| addiu 命令の実装      | `Instructions/IType/AddiuInstruction.cs` (新規) | 小         |
| andi 命令の実装       | `Instructions/IType/AndiInstruction.cs` (新規)  | 小         |
| ori 命令の実装        | `Instructions/IType/OriInstruction.cs` (新規)   | 小         |
| slti/sltiu 命令の実装 | 各ファイル                                      | 小         |
| lui 命令の実装        | `Instructions/IType/LuiInstruction.cs` (新規)   | 小         |

### 1-7. I形式命令（分岐命令）

| タスク                         | ファイル   | 見積難易度 |
| ------------------------------ | ---------- | ---------- |
| bgtz/bltz/blez/bgez 命令の実装 | 各ファイル | 小         |

### 1-9. メモリアクセス命令
メモリ実装に依存

| タスク                         | ファイル                                     | 見積難易度 |
| ------------------------------ | -------------------------------------------- | ---------- |
| lw 命令の実装                  | `Instructions/IType/LwInstruction.cs` (新規) | 中         |
| sw 命令の実装                  | `Instructions/IType/SwInstruction.cs` (新規) | 中         |
| lb/sb 命令の実装（オプション） | 各ファイル                                   | 小         |
| lh/sh 命令の実装（オプション） | 各ファイル                                   | 小         |

### 1-10. システムコール
| タスク                          | ファイル                                    | 見積難易度 |
| ------------------------------- | ------------------------------------------- | ---------- |
| syscall 命令の基盤実装          | `Instructions/SyscallInstruction.cs` (新規) | 中         |
| print_string (syscall 4) の実装 | 同上                                        | 中         |
| DAP経由の出力連携               | `IApplication` に出力イベント追加           | 中         |

---

## 課題2: メモリ未実装

| タスク                                   | ファイル                                   | 見積難易度 |
| ---------------------------------------- | ------------------------------------------ | ---------- |
| IExecutionContext にメモリアクセス追加   | `Debuggers/PlusPimDbg/ExecutionContext.cs` | 中         |
| メモリモデル実装（Dictionary<int, int>） | `Debuggers/PlusPimDbg/PlusPimDbg.cs`       | 中         |
| メモリコンテキスト実装                   | 新規クラスまたは既存拡張                   | 中         |
| .data セクションのパース（オプション）   | `ParsedProgram.cs`                         | 大         |


---

## 実装チェックリスト（今回のスコープ）

- [ ] メモリモデル実装 `Dictionary<int, int>` (`PlusPimDbg.cs`)
- [ ] IExecutionContext にメモリアクセス追加

- [ ] ITypeInstruction 基底クラス (`Instructions/IType/ITypeInstruction.cs`)


- [ ] addi 命令
- [ ] addiu 命令
- [ ] ori 命令
- [ ] lui 命令
- [ ] srl 命令
- [ ] sra 命令
- [ ] lw 命令
- [ ] sw 命令


### .data セクション対応
- [ ] ParsedProgram で .data/.text セクション識別
- [ ] .asciiz ディレクティブのパース
- [ ] 文字列データのメモリ配置
- [ ] syscall 命令の基盤
- [ ] print_string (syscall 4) 実装
- [ ] DAP 経由の出力連携

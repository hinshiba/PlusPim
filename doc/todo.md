# PlusPim Todo

## 高優先度

### 履歴整合性

- [ ] 例外ハンドラへの遷移ステップが履歴に積まれない (#4, `PlusPimDbg.cs:43-48`)
  - [ ] 履歴エントリに種類 (命令の実行 / 例外ハンドラへの遷移) を持たせる
  - [ ] 遷移ステップを `_history` に積む
  - [ ] Back で PC と `LastException` を遷移前の値に戻す
- [ ] 命令フェッチ失敗 (RI, AdEL) のステップを Back しても状態が戻らない (`PlusPimDbg.cs:108-112`)
  - [ ] `instruction is null` で早期 return しているため `IsTerminated` が復元されない
  - [ ] `LastException` と CP0 も復元する
- [ ] 例外を起こした命令の Undo で CP0 と `LastException` が戻らない (#4)
  - [ ] `RuntimeContext` に CP0 と `LastException` をまとめて保存・復元する API を用意する (`GetCP0Snapshot` / `RestoreCP0` の拡張)
  - [ ] `PlusPimDbg.Step` で実行前に保存し, `Back` で一元的に復元する
  - [ ] 一元化で次の2つが直ることを確認する
    - [ ] 不整列 load/store の Undo が何もしない (`MemoryInstruction.cs:88-92`)
    - [ ] add / addi / sub のオーバーフローの Undo がレジスタしか戻さない (`ITypeInstruction.cs:33-37`, `RType3RegInstruction.cs`)
  - [ ] 二重例外 (カーネルモード中の例外) の Undo で `LastException` が残らないことを確認する
  - [ ] 各命令が個別に持つ例外の復元処理を整理する (`SyscallInstruction.Undo`, `BreakInstruction.Undo`, `RuntimeCall._wasCpU`)
- [ ] テスト
  - [ ] `AssertSnapshotEqual` (`TestHelpers.cs:79-90`) で CP0 と `LastException` も比較する
  - [ ] `ExecuteUndo_Syscall_RestoresException` / `ExecuteUndo_AddOverflow_RestoresState` で例外状態を検証する
  - [ ] 不整列 `lw` → ハンドラへ遷移 → StepBack で, `lw` の位置かつユーザーモードに戻る
  - [ ] 例外発生後に先頭まで StepBack → 再実行で, 最初の命令から正しく実行される
  - [ ] syscall → ハンドラ → `eret` の往復をすべて StepBack すると初期状態と一致する

### ランタイムバグ

#### アセンブル時

- [ ] パースに失敗した行があるとラベルアドレスがずれる (#7)
  - [ ] pass 1 はニーモニックが既知なら命令として数え (`ParsedProgram.cs:172`, `InstructionRegistry.GetInstructionCount`), pass 2 は失敗行を捨てる (`TextSegmentBuilder.cs:25-30`). 数える処理と生成する処理を一致させる
  - [ ] パースに失敗した行があれば launch を失敗させ, ファイル名と行番号付きでエラーを表示する
  - [ ] 不正なラベルを指定した `la` (2命令と数えられて捨てられる) も検出されることを確認する
  - [ ] テスト
- [ ] `$40` のような存在しないレジスタ番号を受け付け, 実行時に `IndexOutOfRangeException` で落ちる (#2, `OperandParser.cs:44`, `RegisterFile.cs:17`)
  - [ ] `Enum.TryParse<RegisterID>` が数値文字列をそのまま通す. 17 箇所あるのでレジスタ名の解析を1つの関数にまとめる
  - [ ] `$0`-`$31` と既知のレジスタ名だけを受け付ける
  - [ ] テスト
- [ ] `li` が 16 進数を受け付けない (#7, `LiInstructionParser.cs:51`). 他の即値 (`Immediate.TryParse`) は `0x` を受け付けるので不整合
  - [ ] 32bit 即値のパーサを用意し, 10進 / 負数 / `0x` に対応する
  - [ ] `0x80000000` 以上 (`int` の範囲外) も受け付ける
  - [ ] テスト

#### 実行時

- [ ] C# の例外が起きるとデバッグセッション全体が落ちる (#2)
  - [ ] `PlusPimDbg.Step` で `Execute` を try/catch する
  - [ ] 内部エラーとして停止し, 内容を Debug Console に表示する (`StopReason` に追加するか, 例外として扱うか決める)
- [ ] `read_string` で入力が `$a1` より短いと `ArgumentOutOfRangeException` で落ちる (#2, `RuntimeCall.cs:92`)
- [ ] 未知の syscall 番号が何もせず無視され, ログにしか出ない (`RuntimeCall.cs:107-109`)
  - [ ] ユーザーに見える形で通知する (Debug Console への出力, または例外として停止)

#### VS Code 拡張・起動

- [ ] セッション終了時にプログラムの出力がターミナルごと消える (#12, `extension.ts:17-23`, `87-90`)
  - [ ] 方針を決める: `DebugAdapterExecutable` + `--stdio` で Debug Console に出す / ターミナルを残す
  - [ ] `--stdio` にする場合, `read_int` / `read_string` の入力をどこから受け取るか調べる (stdin は DAP が使う)
  - [ ] `fact.asm` を最後まで Continue して出力が残ることを確認する
- [ ] ポートが既定で 4711 固定のため, 使用中だと起動できない (#18, `extension.ts:52`)
  - [ ] `--stdio` 化, または空きポートを動的に選ぶ
- [ ] 拡張機能のポート確認と本接続を 50ms のタイミングで区別している (#18, `Program.cs:128-137`)
  - [ ] 低速な環境で誤判定するか確認する (未検証)
  - [ ] `--stdio` 化するなら確認処理ごと削除する

### MIPS準拠ミス

- [ ] 自分自身への jump / branch (`end: j end`) で PC が +4 され, 次の命令が実行される (#6, `PlusPimDbg.cs:61`)
  - [ ] PC の前後比較をやめ, 命令が「PC を設定したか」を明示的に返すようにする (`IInstruction` の変更, または制御フロー命令用のインタフェース)
  - [ ] `j`, `jal`, `jr`, `beq`, `bne`, `eret` を対応させる
  - [ ] `pcAutoIncremented` を使う Back も合わせて直す
  - [ ] テスト: `end: j end`, `self: beq $zero, $zero, self`
- [ ] `div` / `divu` のゼロ除算で `DivideByZeroException` になりセッションが落ちる (#2, `InstructionRegistry.cs:126-129`)
  - [ ] MIPS ではゼロ除算は例外を起こさず, HI / LO は不定. MARS と SPIM の挙動を確認して合わせる
  - [ ] テスト: `div $t0, $zero`, `divu $t0, $zero`
- [ ] `div` のオーバーフロー (INT_MIN ÷ -1) で `OverflowException` になりセッションが落ちる (#2)

  ```asm
  lui   $t0, 0x8000     # $t0 = 0x80000000 (INT_MIN)
  addiu $t1, $zero, -1  # $t1 = -1
  div   $t0, $t1        # C# の int.MinValue / -1 は unchecked でも OverflowException
  ```

  - [ ] MIPS では例外を起こさない (結果は不定). MARS と SPIM の挙動を確認し, `int.MinValue / -1` と `% -1` を特別扱いする
  - [ ] テスト
- [ ] `print_int` (syscall 1) が負数を符号なしで表示する (#10, `RuntimeCall.cs:38`). `li $a0, -5` で `4294967291` と表示される
  - [ ] `int` にキャストして表示する
  - [ ] テスト
- [ ] `read_string` (syscall 8) がバッファを1バイト超えて書き込む (#2, `RuntimeCall.cs:85-99`). SPIM は最大 `$a1 - 1` 文字 + NUL
  - [ ] 書き込みを `min(入力長, $a1 - 1)` バイト + NUL に制限する
  - [ ] Undo のために退避する範囲 (現在は `maxLength + 1` バイト) も合わせる
  - [ ] `$a1` が 0, 1 のときの挙動を SPIM に合わせる
  - [ ] テスト
- [ ] `$sp`, `$gp` が 0 で始まる (#14). MARS は `$sp = 0x7FFFEFFC`, `$gp = 0x10008000`
  - [ ] `RuntimeContext` で初期値を設定する
  - [ ] 先頭まで StepBack したときも初期値に戻ることを確認する
- [ ] CP0の関連命令での特権検査

### パッケージング

- [ ] Debug ビルドが VSIX に入る (#18)
  - [ ] `scripts/vsix.mjs` の除外リストに `bin/debug/**` を追加する
  - [ ] Debug バイナリを優先するのは開発モード (`ExtensionMode.Development`) のときだけにする (`extension.ts:67-70`)
  - [ ] 生成した VSIX の中身を確認する
- [ ] `dotnet:debug:win` スクリプトの出力先 `bin/debug/win-x64` と, 拡張機能が探す `bin/debug/PlusPim.exe` が一致しない (`package.json`)
- [ ] .NET ランタイムが入っていない環境 (Windows / Linux) で VSIX をインストールし, 起動を確認する

### ドキュメンテーション

- [ ] README
  - [x] ルートフォルダ名 `OOP10` を直す (#19, `README.md:70`)
  - [ ] 前提条件に Node.js と pnpm, セットアップに `pnpm install` と `pnpm run compile` を追加する (#19)
  - [x] syscall を使うにはカーネルハンドラ (`.ktext`) が必要なことを書き, `kseg.asm` を `program` に加えた launch.json の例を載せる (#3, `README.md:83-95`)
  - [x] サンプルのカーネルハンドラ (`testfolder/kseg.asm`) の場所と内容を説明する (#3)
  - [x] 対応している syscall の一覧 (1, 4, 5, 8, 10) を載せる
  - [ ] 「複数ファイルの対応: 順番に依存しないリンク」(`README.md:25`) を実際の範囲に直す. ラベルはファイルごとに閉じており, ファイルをまたいで探すのは `main` だけ (#8)
  - [x] 未実装の一覧 (`README.md:29-31`) を更新する
  - [ ] ステップ実行はソース行単位ではなく機械命令単位であることを書く. 擬似命令 (`la`, `li` など) は複数命令に展開されるため, 1行に複数回のステップが必要 (#11)
  - [ ] syscall を StepIn / StepOver するとカーネルハンドラに入ることを書く (#11)
  - [ ] Reverse Continue はブレークポイントで止まらず先頭まで戻ることを書く (#5)
- [x] `runtime_call!` (`kseg.asm:19`) の仕様を書く. カーネルモード専用で, `$v0` で機能を選ぶ (#3)
- [x] `doc/instructions.md`
  - [x] `jal` の `$ra` の説明を PC+8 から PC+4 に直す. 遅延スロットがないため (#19, `instructions.md:124`)
  - [x] 実装状況を更新する (#9)
- [ ] CHANGELOG
  - [ ] 「Continue / Reverse Continue」に, Reverse Continue がブレークポイントで止まらないことを書く (#5)
  - [ ] 「Multi-file program support」の範囲を書く (#8)
- [x] 空の docfx ページ (`docs/getting-started.md`, `docs/introduction.md`) を埋めるか削除する (#15)
- [ ] `doc/おおまかな設計.puml` を現状に合わせる (未実装の `MemoryRange` など) (#9)
- [ ] 時間遡行 (Undo) の設計を文書化する. 命令ごとの Undo スタック, 履歴, 例外時の扱い (#20)

## 新機能 (優先度未定)

- [ ] Pause (#1). 無限ループで VS Code が応答しなくなったときに抜ける唯一の手段
  - [ ] `Continue` をワーカースレッドで実行し, 実行中も DAP 要求 (`threads`, `pause` など) に応答する (`Application.cs:135-144`, `DebugAdapter.cs:255`)
  - [ ] 停止要求のフラグを Step のループで確認する
  - [ ] `SupportsPause` と `HandlePauseRequest` を実装し, reason `pause` の `stopped` イベントを送る
  - [ ] 実行中に来た Step / StepBack などの要求を排他制御する
  - [ ] Reverse Continue も中断できるようにする
  - [ ] テスト: 無限ループ → Pause → 停止位置とレジスタを取得できる
- [ ] Reverse Continue をブレークポイントで止める (#5, `Application.cs:151-156`)
  - [ ] `Back` 後の PC がブレークポイントなら止める. 判定を `PlusPimDbg` 側に用意する
  - [ ] 例外フィルタに該当する例外の発生地点でも止めるか決める
  - [ ] `stopped` イベントの reason を `breakpoint` にする
  - [ ] テスト
- [ ] 擬似命令の展開先をインレイヒントで表示する (#11)
  - [ ] 展開結果を拡張機能に渡す方法を決める (カスタム DAP 要求 / CLI / LSP)
  - [ ] `InlayHintsProvider` で `la` → `lui` + `ori` などを表示する
- [ ] syscall のカーネルハンドラを StepOver で飛ばす (#11)
  - [ ] EPC+4 でユーザーモードに戻るまで実行する
  - [ ] 既定の動作にするか, 設定で切り替えるか決める
- [ ] 既定のカーネルハンドラを同梱する (#3)
  - [ ] `kseg.asm` 相当を同梱する (拡張機能のファイル, または PlusPim の埋め込みリソース)
  - [ ] `.ktext` を含むファイルが指定されていなければ自動で読み込む
  - [ ] 同梱ハンドラの中をステップ時にどう見せるか決める
- [ ] `.globl` によるファイル間のラベル解決 (#8)
  - [ ] `.globl` 指令を解析してシンボルテーブルに記録する (現在は `.` で始まる行として無視される)
  - [ ] ローカル → グローバルの順に解決する (`ParsedPrograms.cs:148-157`)
  - [ ] グローバルシンボルの重複をエラーにする
  - [ ] テスト: ファイルをまたぐ `jal`
- [ ] アセンブリ構文 (#9)
  - [ ] 命令と同じ行のラベル `loop: addi ...` (`ParsedProgram.cs:182`)
  - [ ] オフセットを省略したメモリオペランド `sw $t0, ($sp)` (#7, `OperandParser.cs:32`)
- [ ] 命令 (#9)
  - [ ] 分岐擬似命令: `b`, `beqz`, `bnez`, `blt`, `bgt`, `ble`, `bge`
  - [ ] 分岐命令: `bgez`, `bltz`, `blez`, `bgtz`
  - [ ] `mul`
  - [ ] `jalr`
  - [ ] `not`, `neg`
- [ ] syscall: `print_char` (11) (#9)

## 未分類 (優先度未定)

### コード品質

- [ ] デッドコードを削除する (#15)
  - [ ] `Program.cs:148-150` の `throw` より後のコード (CS0162)
  - [ ] `Application._isDebug` とランタイムモードの経路
  - [ ] `InstructionIndex.FromAddress` (両オーバーロード)
  - [ ] `Address.FromInstructionIndex(…, bool)`
  - [ ] `TextSegmentBuilder.CurrentInstructionIndex` / `CurrentAddr`
  - [ ] `TextSegment.BaseAddress`, `DataSegment.BaseAddress`
  - [ ] `ParsedProgram.GetInstruction`
  - [ ] `Immediate.Parse`
  - [ ] テストからしか使われていない `PlusPimDbg.GetRegisters`, `ParsedProgram.InstructionCount` の扱いを決める
- [ ] VS Code テンプレートの残骸を削除する (#15)
  - [x] `vsc-extension-quickstart.md`
  - [x] `src/test/suite/extension.test.ts:11-13` の失敗しえないテスト
  - [ ] 除外リストの `.yarnrc` (`scripts/vsix.mjs`, `.vscodeignore`)
  - [x] `extension.ts:8-9` のコメント
- [ ] 個人設定 `settings.VisualStudio.json` をリポジトリから外す (#15)
- [ ] warnings-as-errors を有効にする (#15)
- [ ] 重複の共通化 (#16)
  - [ ] `Stack<uint>` + `WriteRd` + `Undo` の重複 (`RType3Reg`, `RTypeShiftImm`, `RTypeShiftVar`, `IType`)
  - [ ] 「Undoスタックの整合性のために現在値でWriteRtを呼ぶ」回避策 (`ITypeInstruction.cs:34`, `RType3RegInstruction.cs:33`). 履歴整合性の一元化とあわせて検討
- [ ] コードを言い換えるだけのコメントを削除する (`PlusPimDbg.cs`) (#16)
- [ ] 命名を統一する (#16)
  - [ ] `Address.InValid` と `Label.Invalid`
  - [ ] 名前空間の大文字小文字 (`…Instruction.instructions`, `Program.records`)
  - [ ] `lineIndex` が 0 始まり (`TextSegmentBuilder`) と 1 始まり (命令のコンストラクタ) で混在
  - [ ] 末尾のアンダースコア (`_debugger_`, `inst_`, `maxLength_`)
- [x] 壊れた doc コメントを直す (`ParsedPrograms.cs:9`, `ParsedProgram.cs:45`) (#16)

### テストと CI

- [x] CI (#17, `.github/workflows/ci.yml`)
  - [x] `dotnet build -warnaserror` を追加する
  - [x] `dotnet test` を追加する
  - [x] 拡張機能の `pnpm run compile` と `pnpm run lint` を追加する
  - [x] main への push でも実行する
- [ ] テストの追加 (#17)
  - [ ] `Application`: StepOver, StepOut, Continue, ReverseContinue
  - [ ] `DebugAdapter`: DAP の要求と応答
  - [ ] 複数ファイル
  - [ ] ブレークポイント
  - [ ] カーネルハンドラを経由する流れ (syscall → ハンドラ → `eret`)
  - [ ] `testfolder` の例題を実行し, 出力を照合する

### 独自性・アピール

- [ ] 他のツール (MARS, RARS) にない機能を検討する (#21)
  - [ ] 履歴を検索して「このレジスタ / メモリを最後に書いたのはどの命令か」を答える
  - [ ] レジスタの変化のタイムライン
- [ ] AI の活用方針を決める. スメルの除去, または AI を使った開発フローの文書化 (#22)

## 低優先度

- [ ] 命令実行のパフォーマンス改善 (#13)
  - [ ] Debug ログが無効なときにログ文字列を組み立てない (`ITypeInstruction.cs:40` など)
  - [ ] 計測用のベンチマークを用意する

## 検討中

- [ ] Emacs, Vim, Neovim, Zed への拡張機能提供
- [ ] メモリビュー. DAP `readMemory`, または `.data` と `$sp` 周辺を表示する variables スコープ (#9)

# PlusPim Todo

## バグ修正等

### 実行系

#### MIPS準拠

- [ ] `$sp`, `$gp` に適切な初期値を設定する

#### 履歴整合性

#### ランタイム系

- [ ] char関連システムコール
  - [ ] print_char が生のバイトを書かない
    - `Console.Write((char)(a0 & 0xFF))` は U+00XX を書くため UTF-8 出力では 0x80 以上のバイトが 2バイトになり, UTF-8 文字列を1バイトずつ print_char で出すと文字化けする. 対応案: 状態を持つ UTF-8 デコーダ経由で生バイトを出力する, または仕様を Latin-1 (MARS 互換) と明記する. 0x80 以上のバイトのテストも追加する.
- [ ] 入力バッファ (PendingInput) が MachineState / デバッガのスナップショットから見えない
  - RuntimeContext に明示的な PendingInput プロパティを持たせ, テストのスナップショットに含める. PendingInput 自体は同期されていない (ステップ実行が単一スレッドなので許容) ことをコメントに書く.

- [ ] initでDAPに準拠しない問題

### 前処理系

- [x] パースに失敗した行があるとラベルアドレスがずれる (#7)
  - [x] pass 1 はニーモニックが既知なら命令として数え (`ParsedProgram.cs:172`, `InstructionRegistry.GetInstructionCount`), pass 2 は失敗行を捨てる (`TextSegmentBuilder.cs:25-30`). 数える処理と生成する処理を一致させる
  - [x] パースに失敗した行は無視されログに出力されることを明記する か オプション切り替え
    - PlusPimで処理できないマクロ等をスキップするため
  - [ ] テスト
- [ ] `.kdata` がセグメント指令として認識されない (`ParsedProgram.cs:71` 付近)
  - [ ] `doc/instructions.md` は `.kdata` を実装済みとしているため，実装するか未実装に直す
  - [ ] 実装する場合は `.kdata` の開始アドレスを決め (MARS は `0x90000000`)，カーネルデータのセグメントを追加する
  - [ ] テスト: `.kdata` の `kx:` `.word 1` でラベルのアドレスとメモリが正しい
- [ ] `.ascii`/`.asciiz` が UTF-16 の下位1バイトだけを書く (`DataSegmentBuilder.cs:244`, `248`)
  - `"あ"` が `0x42` になる．print_string は UTF-8 として出力するため不整合
  - [ ] 文字列を UTF-8 のバイト列にして書く．エスケープは ASCII のまま処理する
  - [ ] テスト: `.asciiz "あ"` が `E3 81 82 00`
- [ ] 文字列の直後が `\\"` のとき，行末のコメントが除去されない (`ParsedProgram.cs:149`)
  - `.asciiz "a\\" # c "q"` で文字列が `a\\" # c "q` になる．引用符の直前の `\` の個数の偶奇で判定する
  - [ ] テスト
- [ ] マクロ定義の本体が定義の位置に命令として配置され，`main` などのアドレスがずれる (`ParsedProgram.cs:171`, `TextSegmentBuilder.cs:19`)
  - `.macro` 行は `.` で始まるため無視されるが，本体の行は通常の命令として数えられる
  - [ ] マクロの実装まで，`.macro` から `.end_macro` の本体を読み飛ばす (上のパース失敗の項目の方針と合わせる)
  - [ ] テスト: 定義の後の `main:` が `T+0`
- [ ] `la` が `$` を含むラベルを参照できない (`LaInstructionParser.cs:23`)
  - 分岐とジャンプは `(\w|\$)+` を受け付けるが，`la` は `\w+` のみ
  - [ ] ラベルのパターンを共通化する
  - [ ] テスト: `la $t0, $ret`

### VS Code 拡張

- [ ] セッション終了時にプログラムの出力がターミナルごと消える (#12, `extension.ts:17-23`, `87-90`)
  - [ ] ターミナルを残して終了待ちにする
- ポートが既定で 4711 固定のため, 使用中だと起動できない (#18, `extension.ts:52`)
  - 空きポートを動的に選ぶ
- 拡張機能のポート確認と本接続を 50ms のタイミングで区別している (#18, `Program.cs:128-137`)
  - より堅牢な方法へ
- 起動に時間がかかる
  - プロファイリング

- [ ] Debug ビルドが VSIX に入る (#18)
  - [ ] Debug バイナリを優先するのは開発モード (`ExtensionMode.Development`) のときだけにする (`extension.ts:67-70`)
- [ ] 表示名の変更: PlusPim for VS Code

### ドキュメンテーション

- [ ] README
  - [ ] PlusPim for VS Codeの案内
  - [ ] syscall を使うにはカーネルハンドラ (`.ktext`) が必要なことを書き, `kseg.asm` を `program` に加えた launch.json の例を載せる
  - [ ] サンプルのカーネルハンドラ (`testfolder/kseg.asm`) の場所と内容を説明する
  - [ ] 未実装の一覧 (`README.md:29-31`) を更新する
  - [ ] ステップ実行はソース行単位ではなく機械命令単位であることを書く. 擬似命令 (`la`, `li` など) は複数命令に展開されるため, 1行に複数回のステップが必要
  - [ ] syscall を StepIn / StepOver するとカーネルハンドラに入ることを書く

- [ ] `doc/おおまかな設計.puml` を現状に合わせる
- [ ] CHANGELOGを作成

## 新機能

### 実行系

- [ ] Pause:  無限ループで VS Code が応答しなくなったときに抜ける唯一の手段 
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

- [ ] 既定のカーネルハンドラを同梱する (#3)
  - [ ] `kseg.asm` 相当を同梱する (拡張機能のファイル, または PlusPim の埋め込みリソース)
  - [ ] `.ktext` を含むファイルが指定されていなければ自動で読み込む
  - [ ] 同梱ハンドラの中をステップ時にどう見せるか決める

- [ ] メモリビュー機能
  - [ ] DAP `readMemory`
  - [ ] `.data` と `$sp` 周辺を表示

### 前処理系

- [ ] `.globl` によるファイル間のラベル解決
  - [ ] `.globl` 指令を解析してシンボルテーブルに記録する (現在は `.` で始まる行として無視される)
  - [ ] ローカル → グローバルの順に解決する
  - [ ] グローバルシンボルの重複をエラーにする
  - [ ] テスト: ファイルをまたぐ `jal`

- [ ] 命令と同じ行のラベル `loop: addi ...` (`ParsedProgram.cs:182`)

## コード品質

### テストと CI

- [ ] テストの追加 (#17)
  - [ ] `Application`: StepOver, StepOut, Continue, ReverseContinue
  - [ ] `DebugAdapter`: DAP の要求と応答
  - [ ] 複数ファイル
  - [ ] ブレークポイント
  - [x] カーネルハンドラを経由する流れ (syscall → ハンドラ → `eret`) (`ExceptionTimeTravelTests`)
  - [ ] `testfolder` の例題を実行し, 出力を照合する


## 低優先度

- [ ] 命令実行のパフォーマンス改善 (#13)
  - [ ] Debug ログが無効なときにログ文字列を組み立てない (`ITypeInstruction.cs:40` など)
  - [ ] 計測用のベンチマークを用意する

## 検討中

- [ ] Emacs, Vim, Neovim, Zed への拡張機能提供
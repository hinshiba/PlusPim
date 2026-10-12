# PlusPim Todo

## バグ修正等

### 実行系

#### MIPS準拠

#### 履歴整合性

#### ランタイム系

### 前処理系

- [ ] `.ascii`/`.asciiz` が UTF-16 の下位1バイトだけを書く (`DataSegmentBuilder.cs:244`, `248`)
  - `"あ"` が `0x42` になる．print_string は UTF-8 として出力するため不整合
  - [ ] 文字列を UTF-8 のバイト列にして書く．エスケープは ASCII のまま処理する
  - [ ] テスト: `.asciiz "あ"` が `E3 81 82 00`
- [ ] 文字列の直後が `\\"` のとき，行末のコメントが除去されない (`ParsedProgram.cs:149`)
  - `.asciiz "a\\" # c "q"` で文字列が `a\\" # c "q` になる．引用符の直前の `\` の個数の偶奇で判定する
  - [ ] テスト

### VS Code 拡張

- [ ] プラットフォームとアーキテクチャの判定を厳密にする
  - [ ] 現状は `win32` 以外をすべて `linux-x64` とみなす. macOS や arm64 で原因の分からない `ENOENT` になる
  - [ ] `process.platform` と `process.arch` の組から rid を引くテーブルを作り, 未対応環境では明確なエラーを出す
  - [ ] `scripts/vsix.ts` の `ridMap` と重複しているので, 共通化する (拡張本体は `src`, スクリプトは `scripts` にあり `rootDir` が別なので, 置き場所に注意)
  - [ ] OSx対応も追加
- [ ] `activationEvents: ["onDebug"]` を絞る
  - [ ] `onDebug` は他の拡張のデバッグ開始でも activate される. `onDebugResolve:pluspim` と `onDebugAdapterProtocolTracker:pluspim` で足りるか, VS Code 1.74 以降の暗黙の activation event で省略できるかを実機で確認する
- [ ] ログ出力を `console.log` から `LogOutputChannel` (`createOutputChannel(name, { log: true })`) に移す (`extension.ts:8`)
- [ ] バイナリが見つからないときのエラー通知が二重 (`extension.ts:99-102`)
  - [ ] `showErrorMessage` の直後に `throw` している. VS Code は throw されたエラーを自分で通知するので, どちらかにする．
- [ ] `ensureExecutable` の同期 I/O (`fs.accessSync`, `fs.chmodSync`) を `fs.promises` にそろえ, エラーを値で返す方式と throw が混在している点を整理する

- 起動に時間がかかる
  - ready to run
  - プロファイリング

- 構成を分割する

### ドキュメンテーション

- [ ] README
  - [ ] PlusPim for VS Codeの案内
  - [ ] syscall を使うにはカーネルハンドラ (`.ktext`) が必要なことを書き, `kseg.asm` を `program` に加えた launch.json の例を載せる
  - [ ] サンプルのカーネルハンドラ (`testfolder/kseg.asm`) の場所と内容を説明する
  - [ ] 未実装の一覧 (`README.md:29-31`) を更新する
  - [ ] ステップ実行はソース行単位ではなく機械命令単位であることを書く. 擬似命令 (`la`, `li` など) は複数命令に展開されるため, 1行に複数回のステップが必要
  - [ ] syscall を StepIn / StepOver するとカーネルハンドラに入ることを書く

- [ ] `doc/おおまかな設計.puml` を現状に合わせる
  - [ ] d2言語に移行

## 新機能

### 実行系

### 前処理系

## コード品質

### テストと CI

- [ ] テストの追加
  - [ ] アセンブラ
    - [ ] パース
    - [ ] 疑似命令展開
    - [ ] マクロ
    - [ ] アドレス配置
  - [ ] 結合テスト
    - [ ] テスト用アセンブリディレクトリの自動実行

- [ ] xUnit.net v3への移行

## 低優先度

- [ ] 命令実行のパフォーマンス改善 (#13)
  - [ ] Debug ログが無効なときにログ文字列を組み立てない (`ITypeInstruction.cs:40` など)
  - [ ] 計測用のベンチマークを用意する

- [ ] Emacs, Vim, Neovim, Zed への拡張機能提供
# Changelog

PlusPim変更履歴

拡張機能については，それぞれのディレクトリの変更履歴を参照してください．

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.6.0] - 2026-11-22

### Added

- サブコマンドとオプションの一覧を表示する `--help`, `-h`, `-?` を追加
- バージョンとコミットハッシュを表示する `--version`, `-V` を追加
- `--verbose`, `--strict`, `--force-terminal-utf8`, `--attach`をサブコマンドの前後どちらにも書けるように
- コマンドライン引数が誤っているときに`Run 'pluspim --help' for usage.`を表示
- 命令`jalr`に対応
- 疑似命令`clear`, `neg`, `negu`, `not`, `b`, `beqz`, `bnez`, `blt`, `bgt`, `ble`, `bge`, `bltu`, `bgtu`, `bleu`, `bgeu`, `sgt`, `sgtu`, `sge`, `sgeu`, `sle`, `sleu`, `seq`, `sne`, `mul`, `rem`に対応
- 数値リテラルで2進数(`0b101`)，桁区切りの`_`と`+`符号に対応
- `.text`, `.data`, `.ktext`の引数で配置アドレスを指定できるように
- 4の倍数でないテキストの配置，セグメント範囲外への配置，セクションの重なりをアセンブルエラーとして検出
- アセンブルの診断に警告コード`PPW0001`〜`PPW0014`とエラーコード`PPE0001`〜`PPE0006`を付け，`<path>:<line>: warning PPW0001: …`の書式で表示
- `main`が`.globl`で宣言されていないとき，同じファイルが重複して指定されたとき，複数のファイルで同名のシンボルが`.globl`されたときの警告を追加
- 起動構成に`noDebug`を追加
- ステップ実行の単位を変更する`granularity`起動オプションの追加
- メモリビューのスコープに`$gp`を追加
- Linux，macOS向けのインストーラ`install.sh`とWindows向けのインストーラ`install.ps1`を追加
- GitHub Releaseに8つのプラットフォーム向けの単一実行ファイルと`SHA256SUMS`を添付

### Changed

- `-d`, `--debug`を廃止し，`debug`サブコマンドへ移行
- `System.CommandLine`を2.0.14に更新
- `--connect`と`--listen`を廃止し，`debug connect <ip:port>`と`debug listen <ip:port>`サブコマンドへ移行
- 接続先はホスト名ではなくIPアドレスを取るように（IPv6は`[::1]:port`の形式）
- 実行ファイル名を小文字の`pluspim`に変更
- ステップ実行の既定の単位を機械命令からソースの行に変更
- ニーモニック，指令，レジスタ名は小文字のみ受け付けるように
- `.half`と`.word`は自動で整列しないように
- `.byte`, `.half`, `.word`，16bitの即値，`li`の範囲外の値はアセンブルエラーに
- `.byte`, `.half`, `.word`に整数でない値を渡すと警告を出して捨てるように
- `.space 0`を受け付けるように
- `la`においても未定義のラベルは警告に
- テキストのアドレスの読み書きをランタイムエラー`TextAccess`に
- データセグメントなど命令の無いアドレスの実行をランタイムエラー`NonInstructionExecution`に
- `--strict`はすべての警告をエラーとして扱うように
- 閉じていない文字列や，文字列の後に余分なトークンがある行は警告を出して捨てるように
- `mfc0`と`mtc0`はCP0レジスタ`$8`, `$12`, `$13`, `$14`のみ受け付け，それ以外はアセンブル時に行を捨てるように
- `div`で`0x80000000`を`-1`で割る演算はランタイムエラーにせず，LOを`0x80000000`，HIを`0`にして続行
- `$ra`の初期値を`0xFFFFFFFC`にし，このアドレスへのジャンプを`main`からの復帰による終了として扱うように
- 例外ハンドラをスタックトレースのフレームとして表示し，`eret`で取り除くように
- 実行履歴の先頭に達したときの停止理由を`entry`から`step`に変更
- スコープ名を`General`, `Special`, `CP0`, `Memory`に，変数名を`$zero`, `pc`, `hi`, `lo`, `BadVAddr`などに，スレッド名を`main`に変更
- ランタイムエラーは例外フィルタによらず常に停止し，`breakMode`を`always`に
- 二重例外をランタイムエラーの一種とし，`exceptionInfo`のIDを`DoubleException`に
- アセンブルの警告は`launch`の成功後に`stderr`の出力として送るように
- `--verbose`のログのうち警告以上を`stderr`，それ以外を`console`の出力として送るように
- `initialize`で1始まりの行と列，パス形式のソース以外を拒否
- 内部エラーのときに`[PlusPim internal error]`を出力して終了するように
- ソースコードを複数のプロジェクトに分割
- 全体の大幅なリファクタリング
- DAPを自前の実装に置き換え
- ビルド成果物の出力先を`artifacts/`に変更
- .NET 11（C# 15）に移行
- テストをxUnit v3とMicrosoft.Testing.Platformに移行
- ドキュメントを`doc/`から`docs/`に移動
- このファイルで英文と和文に空白を入れないように

### Removed

- 例外フィルタ`double`（Double Exceptions）を削除
- ランタイムエラー`DivisionOverflow`と`UnsupportedCP0Register`を削除

## [0.5.0] - 2026-10-13

### Added

- 変更履歴の追跡のためにこのファイルを追加
- 命令`bgez`, `bgtz`, `blez`, `bltz`, `lwl`, `lwr`, `swl`, `swr`に対応
- UTF-8のバイト列にNUL終端を付けて配置する`.utf-8z`指令を追加
- `.globl`と`.global`によるシンボルのスコープに対応
- ラベルと同じ行に書かれた命令を解析できるように
- 行を解析できないときに終了する`--strict`オプションを追加
- 実行中だけターミナルをUTF-8に切り替える`--force-terminal-utf8`オプションを追加（Windowsのみ）
- デバッグモードの接続方式として`--connect <host:port>`と`--listen <host:port>`を追加
- 実行を非同期にし，一時停止（Pause）に対応
- メモリビューに対応
- `$sp`と`$gp`に初期値を設定
- 起動構成に`stopOnEntry`を追加
- 疑似命令の展開結果を返すDAPのカスタム要求を追加
- 実行時エラーのメッセージに`RuntimeError:`の接頭辞を付与
- macOS向けバイナリ（`osx-x64`, `osx-arm64`）を`dotnet publish`で出力できるように

### Changed

- `.ascii`と`.asciiz`はASCII範囲外の文字があるとアセンブルエラーに（UTF-8の文字列は`.utf-8z`を使ってください）
- `div`と`divu`の0除算，および`div`の`0x80000000`を`-1`で割る演算はランタイムエラーに
- ランタイムコールのIOはバイト単位で入出力をするように
- Reverse Continueがブレークポイントで停止するように

### Removed

- `--port`と`--stdio`オプションを削除（デバッグモードでは`--connect`か`--listen`が必須です）

### Fixed

- 行の解析に失敗したときにラベルのアドレスがずれる問題を修正
- オフセットを省略したメモリオペランドを受け付けない問題を修正
- `li`で16進数と`0x80000000`以上の値を受け付けない問題を修正
- 範囲外のレジスタを解析時に拒否

## [0.4.0] - 2026-08-31

未追跡です．これより過去はリリースを確認してください．

## [0.3.2] - 2026-04-06

## [0.2.0] - 2026-03-13


[Unreleased]: https://github.com/hinshiba/PlusPim/compare/v0.6.0...HEAD
[0.6.0]: https://github.com/hinshiba/PlusPim/releases/tag/v0.6.0
[0.5.0]: https://github.com/hinshiba/PlusPim/releases/tag/v0.5.0
[0.4.0]: https://github.com/hinshiba/PlusPim/releases/tag/v0.4.0
[0.3.2]: https://github.com/hinshiba/PlusPim/releases/tag/v0.3.2
[0.2.0]: https://github.com/hinshiba/PlusPim/releases/tag/v0.2.0

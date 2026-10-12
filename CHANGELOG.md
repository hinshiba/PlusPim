# Changelog

PlusPim 変更履歴

拡張機能については，それぞれのディレクトリの変更履歴を参照してください．

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.5.0] - 2026-10-13

### Added

- 変更履歴の追跡のためにこのファイルを追加
- 命令 `bgez`, `bgtz`, `blez`, `bltz`, `lwl`, `lwr`, `swl`, `swr` に対応
- UTF-8 のバイト列に NUL 終端を付けて配置する `.utf-8z` 指令を追加
- `.globl` と `.global` によるシンボルのスコープに対応
- ラベルと同じ行に書かれた命令を解析できるように
- 行を解析できないときに終了する `--strict` オプションを追加
- 実行中だけターミナルを UTF-8 に切り替える `--force-terminal-utf8` オプションを追加 (Windows のみ)
- デバッグモードの接続方式として `--connect <host:port>` と `--listen <host:port>` を追加
- 実行を非同期にし，一時停止 (Pause) 二対応
- メモリビューに対応
- `$sp` と `$gp` に初期値を設定
- 起動構成に `stopOnEntry` を追加
- 疑似命令の展開結果を返す DAP のカスタム要求を追加
- 実行時エラーのメッセージに `RuntimeError:` の接頭辞を付与
- macOS 向けバイナリ (`osx-x64`, `osx-arm64`) を `dotnet publish` で出力できるように

### Changed

- `.ascii` と `.asciiz` は ASCII 範囲外の文字があるとアセンブルエラーに (UTF-8 の文字列は `.utf-8z` を使ってください)
- `div` と `divu` の 0 除算，および `div` の 0x80000000 を -1 で割る演算はランタイムエラーに
- ランタイムコールのIOはバイト単位で入出力をするように
- Reverse Continue がブレークポイントで停止するように

### Removed

- `--port` と `--stdio` オプションを削除 (デバッグモードでは `--connect` か `--listen` が必須です)

### Fixed

- 行の解析に失敗したときにラベルのアドレスがずれる問題を修正
- オフセットを省略したメモリオペランドを受け付けない問題を修正
- `li` で 16 進数と 0x80000000 以上の値を受け付けない問題を修正
- 範囲外のレジスタを解析時に拒否

## [0.4.0] - 2026-08-31

未追跡です．これより過去はリリースを確認してください．

## [0.3.2] - 2026-04-06

## [0.2.0] - 2026-03-13


[Unreleased]: https://github.com/hinshiba/PlusPim/compare/v0.5.0...HEAD
[0.5.0]: https://github.com/hinshiba/PlusPim/releases/tag/v0.5.0
[0.4.0]: https://github.com/hinshiba/PlusPim/releases/tag/v0.4.0
[0.3.2]: https://github.com/hinshiba/PlusPim/releases/tag/v0.3.2
[0.2.0]: https://github.com/hinshiba/PlusPim/releases/tag/v0.2.0

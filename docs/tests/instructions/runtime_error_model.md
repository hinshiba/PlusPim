# ランタイムエラーの振る舞い

## 追加の検証

- `execute` でランタイムエラーが発生し，C# の例外は発生しない
- 例外は発生しない
- `RuntimeError` 以外の状態を変更しない
  - レジスタ，`HI`/`LO`，PC，メモリ，CP0，`LastException`，`IsTerminated`，標準入出力
  - カーネルモードでも二重例外にならず，`IsTerminated` は `false` のまま
- PC を設定しない
- Undo 用の情報を積まない(実行が履歴に残らない)
- `undo` で `RuntimeError` を含め実行前の状態に戻る
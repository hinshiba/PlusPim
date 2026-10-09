# 結合 レベルテスト仕様

テスト用アセンブリのプログラムをカーネルハンドラと合わせて最後まで実行し，標準出力を期待値と比較する．

標準入出力の扱いは [ランタイムコールの振る舞いモデル](../instructions/runtime_call_model.md) に従う．

アセンブリをテスト中に文字列で書く `IntegrationTests.cs` の個別のテストは対象外とする．

空のチェックリストの項目は現時点では実現できない検証である．
`要決定` は仕様が決まっていない項目である．
どちらも決まるまでテストしない．

## 対象

`PlusPimTests/Programs/` に置くファイル
- `<name>.asm`: テスト対象のプログラム．1件のテストケースになる
- `<name>.out`: 期待する標準出力
- `<name>.in`: 標準入力(任意)
- `kseg.asm`: カーネルハンドラ．テストケースにはしない

プログラムごとの期待する出力は `.out` に書く．

### testfolder のサンプル

`vscode_ext/pluspim/testfolder` のサンプルである．
入力は空で，`main` の `jr $ra` で終了する．

- `array.asm`
  - `.word` の配列
  - `lw`
  - `sll`
  - `slt`
- `bubble.asm`
  - `sw` による配列の書き換え
  - 二重ループ
- `fact.asm`
  - `.align`
  - `$` を含むラベル
  - `mult`/`mflo`
  - `$fp`
- `fib.asm`
  - 再帰呼び出し
  - スタックの退避と復元
- `gcd.asm`
  - `div`/`mfhi`/`mflo`
  - `mult`
  - 入れ子の `jal`
- `strlen.asm`
  - `lb`/`sb`
  - `.space`
  - `slti`
- `sum.asm`
  - ループ
  - `move`
  - `li`

### 追加するプログラム

ランタイムコールの入出力と `exit` を通す．

- `syscall_exit.asm`
  - `print_string` の後に `exit` を呼ぶ
  - `exit` の後の命令は実行されない
  - 終了はカーネルモードのまま行われる
- `echo.asm`(`echo.in` あり)
  - 入力を読んで同じ内容を出力する
  - `read_int` の負の値
  - `print_int`
  - `print_char`
  - `read_string` が改行を含めて読む
  - `print_string`
  - `read_char`

## 追加のパラメータ

- 入力: なし / あり
- 終了の方法
  - `main` の `jr $ra`
  - `exit`

## 追加の検証

### 正常系

- 上限までに `Terminated` が返る
- `GetRuntimeError()` と `GetLastException()` が `null` である
- 標準出力が `.out` と完全に一致する
- 読み込み時に `Warning` 以上のログが0件である
- 出力は `$sp` と `$gp` の初期値に依存しない
- [ ] 入力をすべて消費したことの確認
- 終了後に `Back` を繰り返して履歴を空にすると，読み込み直後の状態(`TakeSnapshot`)と一致する
  - 出力は取り消されない([ランタイム](../../runtime.md#stepback-の挙動))
- 同じプログラムを2回読み込んで実行すると，出力と `Step` の回数が一致する

### 異常系

- 上限に達したら，プログラム名と最後の PC を含むメッセージで失敗する
  - `j` で自分自身へ戻るだけのプログラムで，上限を小さくして確認する
- `.asm` に対応する `.out` がなければ，スキップではなく失敗する
- カーネルハンドラなしで `syscall` を実行すると，出力は空のまま二重例外(`RI`)で終了する
  - `array.asm` 単独で確認する

## 要決定

- [ ] `testfolder` のサンプルを `Programs/` にコピーして二重管理するか，`Programs/` を正として `testfolder` から参照するか
- [ ] 正常終了以外を期待するプログラム(ランタイムエラー，上限到達)の期待値の書き方
- [ ] 複数ファイルからなるプログラムの置き方
- [ ] 終了時のレジスタやメモリも比較するか
- [ ] 実時間のタイムアウトを併用するか
- [ ] CI で `dotnet test` を実行するか

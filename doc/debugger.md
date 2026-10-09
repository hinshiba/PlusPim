# デバッガドキュメント

PlusPim のデバッグアダプタ (DAP) の挙動について示す．

## 起動と接続

`PlusPim -d <ファイル...>` はデバッガモードで起動し，DAP の通信路として TCP (既定) か標準入出力 (`--stdio`) を使う．

TCP では `127.0.0.1` の `--port` (既定値 4711) で接続を待ち，1つだけ接続を受け付ける．
受け付けた後は待ち受けを閉じるため，2つ目以降の接続は拒否される．

- `--port 0` なら OS が空いているポートを選ぶ．VS Code 拡張機能はこれを使う
- 待ち受けを始めたら，標準出力に `PLUSPIM_DAP_LISTENING 127.0.0.1:<ポート>` と LF の1行を書く．接続の前に標準出力に書くのはこの行だけである
- 指定したポートが使用中なら，標準エラー出力に `Port N is already in use. Use --port 0 to pick a free port.` を書き，終了コード 2 で終了する
- リダイレクトされた標準入力，標準出力，標準エラー出力は UTF-8 で読み書きする．コンソールに直接つながっているものはコンソールのコードページのままにする

VS Code 拡張機能は PlusPim を子プロセスとして起動し，標準出力の上の行からポートを読んで接続する．
それ以外の標準出力と標準エラー出力は端末に表示し，端末への入力を行単位で標準入力に送る．
上の行が 15 秒以内に届かないか，その前に PlusPim が終了したら起動を失敗させる．

## セッションの開始

DAP の順序に従い，次の順に処理する．

1. `initialize` に応答する．`supportsConfigurationDoneRequest` を `true` で返す
2. `launch` でプログラムを読み込み，応答してから `initialized` イベントを送る
3. クライアントが `setBreakpoints`，`setExceptionBreakpoints` などの構成要求を送る
4. `configurationDone` に応答してから実行を始める

`launch` の構成の `stopOnEntry` (既定値 `true`) が `true` なら，`configurationDone` の応答の後に停止理由 `entry` の `stopped` を送る．
`false` なら `configurationDone` の応答の後に Continue と同じく実行を始める．

アセンブルに失敗した場合は `launch` を失敗させ，その応答のメッセージに `ファイル名:行番号` 付きのエラーを含める．
このとき `initialized` は送らない．

## 実行と一時停止

Continue，StepOver (`next`)，StepIn，StepOut，StepBack，Reverse Continue は，応答を先に送ってからワーカースレッドで実行し，停止したら `stopped` を送る．
実行中も `threads`，`setBreakpoints`，`stackTrace`，`pause` などの要求に応答する．
実行中に設定したブレークポイントは，実行中のループにも反映される．

実行は同時に1つまでとし，実行中に届いた実行の要求は待たせずにエラーにする．

`pause` は実行を止め，停止理由 `pause` の `stopped` を送る．
停止中の `pause` は何もせずに成功する．

| 停止の原因                 | `stopped` の reason |
| -------------------------- | ------------------- |
| ステップの完了             | `step`              |
| ブレークポイント           | `breakpoint`        |
| 例外，ランタイムエラー     | `exception`         |
| `pause`                    | `pause`             |
| 巻き戻しで履歴の先頭に達した | `entry`             |

履歴の先頭に達した場合は，デバッグコンソールに `Reached the beginning of the execution history.` を出力する．

### 逆方向の実行

StepBack は1ステップだけ戻し，停止理由は `step` とする．

Reverse Continue はブレークポイントのある命令の実行前の状態に戻るか，履歴の先頭に達するまで巻き戻す．
例外フィルタに該当する例外では止まらない．例外の発生地点に戻るには，その行にブレークポイントを置くか StepBack を使う．

例外ハンドラへの遷移を戻した直後は，例外を起こした命令を実行済みで例外が保留された状態なので，その命令にブレークポイントがあっても止まらない．
さらに1ステップ戻った，その命令の実行前の状態で止まる．

### 入力を待っている間の制限

read_int，read_string，read_char は入力の1行が届くまで標準入力の読み取りで待つ．
この間は実行を止められないため，`pause` は入力が届いた後に効く．
また，この間はプログラムの状態を読む要求 (`stackTrace`，`variables`，`setBreakpoints` など) も入力が届くまで待たされる．

## メモリビュー

DAP の `readMemory` に対応する (`supportsReadMemoryRequest`)．
VS Code では `memoryReference` を持つ変数の View Binary Data からメモリビューを開ける．表示には Hex Editor 拡張機能が必要である．

`memoryReference` を持つ変数は次の通りである．メモリ参照は `0x%08X` の形式とする．

- Registers スコープの各汎用レジスタ．値をアドレスとする
- Memory スコープの `.data`．データセグメントの先頭 `0x10000000` で，値にバイト数を示す
- Memory スコープの `stack ($sp)`．そのフレームの `$sp` の値

スタックフレームの `instructionPointerReference` は PC である．

`readMemory` は次のように読む．

- `memoryReference` は `0x` から始まる16進数か10進数とし，`offset` (負でもよい) を加える
- `count` は 64 KiB までに切り詰める
- アドレス空間 `[0, 2^32)` と重なる部分だけを読む．`address` は実際に返す最初のバイトのアドレスで，0 より前の部分は `address` を進めて表す
- `unreadableBytes` は `0xFFFFFFFF` を超える末尾の部分のバイト数である
- 書き込まれていないメモリは実行時と同じく 0 である

`readMemory` は実行中にも応答する．メモリの書き込み (`writeMemory`) には対応しない．

## 疑似命令の展開先

カスタム要求 `pluspimPseudoExpansions` で，ファイルの疑似命令の行と展開先の機械命令を返す．
拡張機能のインレイヒントに使う．

- `initialized` イベントの後から有効である．それより前は `Program is not loaded.` で失敗する
- 引数は `{ "source": { "path": "<絶対パス>" } }` で，パスは `setBreakpoints` と同じである
- 応答の本体は次の形である．`line` は1始まり，`address` は `0x%08X` の形式である

```json
{ "lines": [
  { "line": 6, "mnemonic": "la",
    "instructions": [ { "address": "0x00400000", "text": "lui $t0, 0x1000" },
                      { "address": "0x00400004", "text": "ori $t0, $t0, 0x0000" } ] },
  { "line": 8, "mnemonic": "move", "instructions": [ { "address": "0x00400008", "text": "addu $t2, $t1, $zero" } ] }
] }
```

- 解析できた疑似命令の行をすべて含む．1命令に展開される行 (`move`，`nop`，小さな値の `li`) も含む
- 解析できなかった行は含まない
- 読み込んでいないファイルは `lines: []` を返す
- `text` は即値を解決した命令の表記である．表記に未対応の命令は `?` とする
- セッション中にプログラムは変わらないので，クライアントはファイルごとに結果を保持してよい

## セッションの終了

`disconnect` は実行中なら実行を止め，応答の送信が終わってからセッションを終える．

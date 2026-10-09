# デバッガドキュメント

PlusPim のデバッグアダプタ (DAP) の挙動について示す．

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

## セッションの終了

`disconnect` には応答を書いてからセッションを終える．

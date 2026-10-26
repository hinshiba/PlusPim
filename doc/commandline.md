# コマンドライン

PlusPim のコマンドラインの使い方を説明します．

```
PlusPim [-h | --help] [-V | --version]
PlusPim <debug | runtime> [options] <file>...
```

## サブコマンド

動作モードをサブコマンドで選択できます．

### `debug`

DAP クライアントからの操作を受け付けるデバッガモードで起動します．

```
PlusPim debug (--connect <host:port> | --listen <host:port>) [options] <file>...
```

`--connect` と `--listen` のどちらか一方を必ず指定してください．
それぞれの動作については[DAP クライアントとの接続](connection.md)を参照してください．

```
PlusPim debug --listen 127.0.0.1:0 main.asm sub.asm
```

### `runtime`

デバッガの機能を用いず，プログラムを最後まで実行します．

```
PlusPim runtime [options] <file>...
```

現在は未実装です．
標準エラー出力へメッセージを出力し，終了コード 1 で終了します．

## オプション一覧

- `--help`, `-h`,`-?`: PlusPim のバージョンおよびハッシュと，サブコマンドおよびオプションの一覧を表示します．
- `--version`, `-V`: PlusPim のバージョンおよびハッシュを表示します

`debug`，`runtime`サブコマンドで有効なオプション
- `--verbose`, `-v`: ログを標準エラー出力へ出力します
- `--strict`: 解析できない行や未対応のアセンブラ指令があるとき，警告ではなくエラーにします
- `--connect <host:port>`: 待ち受けている DAP クライアントへ接続します
- `--listen <host:port>`: DAP クライアントからの接続を待ち受けます
- `--force-terminal-utf8`: ターミナルを UTF-8 に切り替えます
- `--attach`: PlusPim 自体のデバッグのために .NET デバッガをアタッチします

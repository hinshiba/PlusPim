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
PlusPim debug (--connect <ip:port> | --listen <ip:port>) [options] <file>...
```

`--connect` と `--listen` のどちらか一方を必ず指定してください．
それぞれの動作については[DAP クライアントとの接続](connection.md)を参照してください．

```
PlusPim debug --listen 127.0.0.1:0 main.asm sub.asm
```

### `runtime`

現在は未実装です．

デバッガの機能を用いず，プログラムを最後まで実行します．

```
PlusPim runtime [options] <file>...
```

## オプション一覧

- `--help`, `-h`, `-?`: PlusPim のバージョンおよびハッシュと，サブコマンドおよびオプションの一覧を表示します．
- `--version`, `-V`: PlusPim のバージョンおよびハッシュを表示します

`debug`，`runtime`サブコマンドで有効なオプション
- `--verbose`, `-v`: ログを標準エラー出力へ出力します
- `--strict`: 解析できない行や未対応のアセンブラ指令があるとき，警告ではなくエラーにします
- `--force-terminal-utf8`: ターミナルを UTF-8 に切り替えます
- `--attach`: PlusPim 自体のデバッグのために .NET デバッガをアタッチします

`debug`サブコマンドでのみ有効なオプション
- `--connect <ip:port>`: 待ち受けている DAP クライアントへ接続します
- `--listen <ip:port>`: DAP クライアントからの接続を待ち受けます

## ファイル

`-` で始まる名前のファイルはオプションと区別できないため，`./-main.asm` のようにパスを付けて指定してください．

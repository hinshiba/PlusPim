# コマンドライン

PlusPim のコマンドラインの使い方を説明します．

```
pluspim [-h | --help] [-V | --version]
pluspim debug <connect | listen> <ip:port> [options]
pluspim runtime [options] <file>...
```

## サブコマンド

動作モードをサブコマンドで選択できます．

### `debug`

DAP クライアントからの操作を受け付けるデバッガモードで起動します．

```
pluspim debug connect <ip:port> [options]
pluspim debug listen <ip:port> [options]
```

接続方法をサブコマンド `connect` か `listen` で必ず指定してください．
`<ip:port>` は `IPv4:port` か `[IPv6]:port` の形式で指定します．

- `connect`: 待ち受けている DAP クライアントへ接続します．ポートは 1〜65535 です
- `listen`: DAP クライアントからの接続を待ち受けます．ポートは 0〜65535 で，0 なら空きポートを使います

それぞれの動作については[DAP クライアントとの接続](connection.md)を参照してください．

```
pluspim debug listen 127.0.0.1:0
```

### `runtime`

現在は未実装です．

デバッガの機能を用いず，プログラムを最後まで実行します．

```
pluspim runtime [options] <file>...
```

## オプション一覧

- `--help`, `-h`, `-?`: PlusPim のバージョンおよびハッシュと，サブコマンドおよびオプションの一覧を表示します．
- `--version`, `-V`: PlusPim のバージョンおよびハッシュを表示します

`debug`，`runtime`サブコマンドで有効なオプション
- `--verbose`, `-v`: ログを標準エラー出力へ出力します
- `--strict`: アセンブラの警告をすべてエラーとして扱います．詳細は[アセンブラ](assembler.md)を参照してください
- `--force-terminal-utf8`: ターミナルを UTF-8 に切り替えます
- `--attach`: PlusPim 自体のデバッグのために .NET デバッガをアタッチします

## ファイル

`runtime` サブコマンドのファイルの指定について，
`-` で始まる名前のファイルはオプションと区別できないため，`./-main.asm` のようにパスを付けて指定してください．

# PlusPim

DAPに対応したMIPSアセンブリ言語デバッガ

[![Ask DeepWiki](https://deepwiki.com/badge.svg)](https://deepwiki.com/hinshiba/PlusPim)

## 概要

PlusPimは，Debug Adapter Protocolに対応したMIPSアセンブリコード用のデバッグツールです．`.NET`で書かれたバックエンドと，フロントエンドのVS Code拡張機能を組み合わせることで，便利なデバッグ体験を提供します．

このプロジェクトは，主に2つの部分から構成されています:
1. **PlusPim**: [デバッグアダプタプロトコル (DAP) ](https://microsoft.github.io/debug-adapter-protocol/)を実装し，かつMIPSのエミュレーションそしてデバッグのロジックを処理する`.NET 11`アプリケーションです．
2. **VS Code拡張機能**: デバッガをVS Codeに統合し，デバッグコマンドのユーザーインターフェースを提供する拡張機能です．

いわゆるタイムトラベルデバッガであり，バックステップ実行をサポートします．

## 機能

- **ステップ実行**: MIPSコードを1行(または1命令)ずつ実行します．
- **バックステップ実行**: MIPSコードを1行前の状態に戻します．
- **一時停止**: 実行中のプログラムを一時停止できます．
- **レジスタ表示**: 実行中に32本すべてのMIPSレジスタの状態を表示します．
- **ブレークポイント**: ブレークポイントを設定可能にします．
- **ラベル解決**: ジャンプや分岐のためのラベルを解析し，解決します．
- **例外のエミュレーション**: カーネルテキストセグメントにジャンプし，ハンドラを実行します．
- **複数ファイルの対応**: 順番に依存しないリンクをサポートします．
- **メモリビュー**: データ領域を見ることができます．
- **疑似命令の展開表示**: 疑似命令が展開された機械語命令を確認できます (VS Code ではインレイヒントとして表示)．

## ドキュメント

| ドキュメント                                                   | 内容                                                                 |
| -------------------------------------------------------------- | -------------------------------------------------------------------- |
| [命令の対応状況](docs/instructions.md)                         | 疑似命令を含む命令の対応状況                                         |
| [アセンブラ指令の対応状況と仕様](docs/assembler_directives.md) | `.data` や `.utf-8z` などのアセンブラ指令の対応状況と仕様            |
| [DAP クライアントとの接続](docs/connection.md)                 | `debug connect` と `debug listen` による接続方式，ターミナルの符号化 |
| [アセンブラ](docs/assembler.md)                                | セグメント，ラベル，解析失敗時の挙動などアセンブラの仕様             |
| [ランタイム](docs/runtime.md)                                  | レジスタの初期値，ランタイムコール (syscall) など PlusPim 固有の仕様 |
| [VS Code 拡張機能](clients/vscode/README.md)                   | 拡張機能の機能，起動構成，既知の制限                                 |
| [変更履歴](CHANGELOG.md)                                       | PlusPim 本体の変更履歴                                               |
| [VS Code 拡張機能の変更履歴](clients/vscode/CHANGELOG.md)      | 拡張機能の変更履歴                                                   |

## 使い方

### VS Code の場合

VS Code 拡張機能 **PlusPim for VS Code** を使うのが最も簡単です．PlusPim の実行ファイルは拡張機能に同梱されているため，.NET を別途インストールする必要はありません．
対応しているのは Windows (x64)，Linux (x64)，macOS (x64，Apple Silicon) (実機未確認)です．

1. **拡張機能をインストールする**:
    [Releases](https://github.com/hinshiba/PlusPim/releases) から使っている環境に合った `pluspim-<platform>-<version>.vsix` をダウンロードし，VS Code のコマンドパレットで `Extensions: Install from VSIX...` を実行してインストールします．

2. **起動構成を作成する**:
    MIPS アセンブリファイル (`.asm`) を開き，**Run and Debug** ビュー (`Ctrl+Shift+D`) を開きます．`launch.json` がない場合は作成を促されるので，**PlusPim Debugger**を選択します．

3. **デバッグセッションを開始する**:
    **F5**キーを押して，現在開いている`.asm`ファイルのデバッグを開始します．その後，デバッグコントロールを使用してコードを実行できます．
    プログラムの標準入出力は VS Code のターミナルにつながります．

起動構成のプロパティ (`program`，`stopOnEntry`，`forceTerminalUtf8` など) は[拡張機能の README](clients/vscode/README.md#launch-configuration) を参照してください．

#### `syscall`を使う場合

PlusPim は`syscall`を直接処理しません．`syscall`は例外としてカーネルテキスト (`.ktext`) の例外ハンドラへ飛び，ハンドラの中の`runtime_call!`が入出力などを処理します．
ハンドラがないと，飛び先に命令がないためランタイムエラー`NonInstructionExecution`で止まります．

[`samples/kseg.asm`](samples/kseg.asm) は，`syscall`なら`runtime_call!`を呼び，それ以外の例外なら例外を起こした命令を飛ばして復帰する最小のハンドラです．
これをワークスペースへコピーし，`program`に自分のファイルと並べて指定します．

```json
{
	"version": "0.2.0",
	"configurations": [
		{
			"type": "pluspim",
			"request": "launch",
			"name": "Launch with kernel handler",
			"program": ["${file}", "${workspaceFolder}/kseg.asm"]
		}
	]
}
```

コマンドラインから起動する場合も同様に，`pluspim debug listen 127.0.0.1:0 main.asm kseg.asm`のようにハンドラのファイルを加えます．
`runtime_call!`が処理する機能の一覧は[ランタイム](docs/runtime.md#ランタイムコール)を参照してください．

### それ以外のエディタの場合

PlusPim は DAP を実装しているため，DAP に対応した任意のエディタやクライアント (Neovim の nvim-dap，Emacs の dap-mode など) から利用できます．
DAP の通信には標準入出力ではなく TCP を使います．プログラムの標準入出力は PlusPim を起動したターミナルにそのままつながります．

1. **PlusPim の実行ファイルを用意する**:
    [開発環境のセットアップ](#開発環境のセットアップ)に従ってビルドするか，Releases にある実行ファイルを取り出します．

2. **PlusPim をデバッグモードで起動する**:
    クライアントが接続しに来る場合は `debug listen` を使います．ポートに 0 を指定すると空きポートが割り当てられ，実際のアドレスが標準エラー出力に通知されます．

    ```sh
    pluspim debug listen 127.0.0.1:0
    # 標準エラー出力: DAP server listening at: 127.0.0.1:<実ポート>
    ```

    クライアントが先に待ち受ける場合は `debug connect 127.0.0.1:<ポート>` を使います．

3. **クライアントから接続する**:
    通知されたアドレスへ接続し，`launch` 要求を送ります．`launch` の引数には次を指定できます．

    - `program` (必須): アセンブリファイルのパスの文字列，またはその配列．
    - `cwd`: `program` の相対パスの基準にする絶対パスのディレクトリ．省略したときは `program` に絶対パスだけを指定できます．
    - `stopOnEntry` (既定値 `true`): 読み込み直後に最初の命令で止まるか．
    - `noDebug` (既定値 `false`): ブレークポイントと例外フィルタを使わずに実行するか．

    ```json
    { "program": ["main.asm", "kseg.asm"], "cwd": "/path/to/project", "stopOnEntry": true }
    ```

接続方式の詳細と検証規則は [DAP クライアントとの接続](docs/connection.md) を参照してください．

#### コマンドライン

サブコマンドとオプションの一覧は [コマンドライン](docs/commandline.md) を参照してください．

## アーキテクチャ

このプロジェクトは，関心の分離を明確にした設計になっており，コンポーネントを疎結合にするために依存性逆転の原則を使用しています．主要なアーキテクチャの流れは以下の通りです:

`エディタUI (VS Code) -> Adapter/EditorController/DAP -> Usecase -> Asm / Dbg -> Image -> Instruction`

- **EditorController**: 各種エディタとの通信を行います．現在はDAPのみ実装されています．
- **Usecase**: 確立したセッションの間でアセンブラやデバッガを呼び出し制御します．
- **Asm**: アセンブリを実行できる内部の形式に変換します．
- **Dbg**: デバッグ対象の実行と履歴を管理します．
- **Instruction**: 命令の定義を配置しています．

## 開発環境のセットアップ

### 前提条件

- [.NET 11 SDK](https://dotnet.microsoft.com/download)
  - `global.json` で指定した `11.0.100-rc.3` 以降のパッチ
- [Bun](https://bun.sh/) 1.3 以降 (VS Code 拡張機能を開発する場合)
- VS Code 1.108 以降 (VS Code 拡張機能を開発する場合)

### リポジトリの構成

| パス              | 内容                                                        |
| ----------------- | ----------------------------------------------------------- |
| `src/`            | PlusPim 本体 (.NET)         |
| `tests/`          | PlusPim のテスト |
| `clients/vscode/` | VS Code 拡張機能 (TypeScript)                               |
| `samples/`        | 動作確認用の MIPS アセンブリのサンプル                      |
| `docs/`           | ドキュメント                                                |
| `install/`        | インストールスクリプト                                      |

### PlusPim のビルドとテスト

リポジトリのルートディレクトリで実行します:

```sh
git clone https://github.com/hinshiba/PlusPim.git
cd PlusPim
dotnet build PlusPim.slnx
dotnet test --solution PlusPim.slnx
```

テストは xUnit v3 で書かれ，`global.json` の指定により Microsoft.Testing.Platform で実行します．
そのためソリューションは `--solution` で，1つのテストプロジェクトは `--project` で指定します:

```sh
dotnet test --project tests/PlusPim.Instructions.Tests
```

テストの仕様は [docs/tests](docs/tests) にあります．

ミューテーションテストは [Stryker.NET](https://github.com/stryker-mutator/stryker-net) で行います．
設定は各テストプロジェクトの `stryker-config.json` にあり，そのディレクトリで実行します:

```sh
cd tests/PlusPim.Asm.Tests
dotnet run --file ../stryker.cs
```

Stryker.NET 5.1.0 の Roslyn は `union` を解釈できないため，[tests/stryker.cs](tests/stryker.cs) が Roslyn を SDK のものに差し替えたコピーを `artifacts/stryker/` に作って実行します．
`--` の後の引数は Stryker に渡され，レポートは `StrykerOutput/` に出力されます．

単体の実行ファイルを作る場合は `dotnet publish` を使います (`<rid>` は `win-x64`，`linux-x64`，`osx-x64`，`osx-arm64` など):

```sh
dotnet publish src/PlusPim/PlusPim.csproj -c Release -r <rid> --self-contained -o ./out
```

### VS Code 拡張機能の開発

`clients/vscode` で実行します:

```sh
bun install
bun run compile
bun run dotnet:debug:win
```

`dotnet:debug:win` は PlusPim の Debug ビルドを `clients/vscode/bin/debug` に出力します．拡張機能を開発モードで起動したときは，このバイナリが使われます．
Linux では `dotnet publish ../../src/PlusPim/PlusPim.csproj -c Debug -r linux-x64 --self-contained -o ./bin/debug` を実行してください．

その後，`clients/vscode` を VS Code で開き，**Run and Debug** ビューから **"Run Extension"** の起動構成を選んで**F5**キーを押すと，拡張機能が読み込まれた拡張機能開発ホストが起動します．動作確認には `samples/` のファイルが使えます．

リントと整形は `bun run check`，VSIX の作成は `bun run package:win32-x64` などで行います．コマンドの一覧は[拡張機能の README](clients/vscode/README.md#development) を参照してください．

## プロジェクトについて

このプロジェクトの初版は講義: `オブジェクト指向言語`の課題として作成されました．
現在は独立して開発されています．

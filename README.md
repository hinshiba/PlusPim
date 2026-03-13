# PlusPim

DAPに対応したMIPSアセンブリ言語デバッガ

## 概要

PlusPimは，Debug Adapter Protocolに対応したMIPSアセンブリコード用のデバッグツールです．`.NET`で書かれたバックエンドと，フロントエンドのVS Code拡張機能を組み合わせることで，便利なデバッグ体験を提供します．

このプロジェクトは，主に3つの部分から構成されています:
1. **PlusPim**: [デバッグアダプタプロトコル (DAP) ](https://microsoft.github.io/debug-adapter-protocol/)を実装し，かつMIPSのエミュレーションそしてデバッグのロジックを処理する`.NET 10`アプリケーションです．
2. **VS Code拡張機能**: デバッガをVS Codeに統合し，デバッグコマンドのユーザーインターフェースを提供する拡張機能です．

いわゆるタイムトラベルデバッガであり，バックステップ実行をサポートします．

## 機能

- **ステップ実行**: MIPSコードを1行ずつ実行します．
- **バックステップ実行**: MIPSコードを1行前の状態にします．
- **レジスタ表示**: 実行中に32本すべてのMIPSレジスタの状態を表示します．
- **命令サポート**: MIPS命令の実行をサポートします．
- **ラベル解決**: ジャンプや分岐のためのラベルを解析し，解決します．

いくつかの命令と機能はまだ実装されていません．

- ステップオーバー
- ブレークポイント
- 例外のエミュレーション
- 複数ファイルの実行
- ランタイムモード


### 実装予定の機能

- I形式のMIPS命令
- メモリアクセス命令 (`lw`, `sw`) の完全サポート．
- `.data`セクションと`syscall`(例:`print_string`)のサポート．
- 動的なブレークポイント管理．

## アーキテクチャ

このプロジェクトは，関心の分離を明確にした設計になっており，コンポーネントを疎結合にするために依存性逆転の原則を使用しています．主要なアーキテクチャの流れは以下の通りです:

`エディタUI (VS Code) -> DebugAdapter -> IApplication <- Application -> IDebugger <- PlusPimDbg`

- **EditorController (`DebugAdapter`)**: DAPを実装し，VS Codeクライアントと通信します．DAPリクエストを`IApplication`インターフェースへの呼び出しに変換します．
- **Application (`IApplication`)**: アプリケーションのコアロジックを定義し，エディタ向けコンポーネントとデバッガバックエンドの間の橋渡しをします．
- **Debugger (`IDebugger`)**: デバッグエンジンのインターフェースで，異なるデバッガバックエンドを交換可能にします．
- **PlusPimDbg**: `IDebugger`インターフェースの現在の実装で，MIPS命令パーサーと実行エンジンを含みます．


## セットアップ

### 前提条件

- [.NET 10 SDK](https://dotnet.microsoft.com/download)

### ビルド手順

1. **リポジトリをクローン**

```sh
git clone <repository-url>
cd <repository-folder>
```

1. **PlusPimのビルド**

リポジトリのルートディレクトリで実行します:
```sh
dotnet build PlusPim.slnx
```

## 使い方

1. **VS Codeでプロジェクトを開く**:
    ルートフォルダ (`OOP10`) をVisual Studio Codeで開きます．

1. **拡張機能を実行する**:
    - 「実行とデバッグ」ビューを開きます (`Ctrl+Shift+D`) ．
    - ドロップダウンメニューから**"Run Extension"**の起動構成を選択します．
    - **F5**キーを押して，PlusPim拡張機能が読み込まれた新しいVS Codeウィンドウ("拡張機能開発ホスト")を起動します．

1. **デバッグセッションを開始する**:
    - 新しい「拡張機能開発ホスト」ウィンドウで，MIPSアセンブリファイル(例:`.asm`拡張子のファイル)を開きます．
    - 再度「実行とデバッグ」ビューを開きます．
    - `launch.json`ファイルがない場合，VS Codeに作成を促されます．「PlusPim Debugger」を選択してください．これにより`.vscode/launch.json`ファイルが作成されます．
    - デフォルトの構成は以下のようになります:

```json
{
    "version": "0.2.0",
    "configurations": [
        {
        "type": "pluspim",
        "request": "launch",
        "name": "Minimal Launch",
        "program": "${file}"
        }
    ]
}
```

**F5**キーを押して，現在開いている`.asm`ファイルのデバッグを開始します．その後，デバッグコントロールを使用してコードを実行できます．

## プロジェクトについて

このプロジェクトの初版は講義: `オブジェクト指向言語`の課題として作成されました．
チームメンバーを次に示します．

- hinshiba: leader
- Kiichi
- Akichika
- takato

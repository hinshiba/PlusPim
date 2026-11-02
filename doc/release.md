# リリースと配布物

PlusPim の Release に添付するファイルの仕様と，リリースの手順を定めます．
v0.5.0 より後のリリースはこれに従うこととします．

## タグとバージョン

- タグは `v<version>` とする
  - 例
    - `v0.6.0`
    - `v0.6.0-rc.1`
- `<version>` は `PlusPim/PlusPim.csproj` の `<Version>` と一致させる
  - リリース CI において検証する
- `<version>` に `-` を含むものはプレリリースとして作成する

プレリリースは `releases/latest` の対象にならないので，インストーラで入れるには `PLUSPIM_VERSION` を指定します．

## 添付ファイル

| ファイル                            | 内容                              |
| ----------------------------------- | --------------------------------- |
| `pluspim-<version>-<target>.zip`    | Windows 向けのアーカイブ          |
| `pluspim-<version>-<target>.tar.gz` | Linux と macOS 向けのアーカイブ   |
| `install.sh`                        | Linux と macOS 向けのインストーラ |
| `install.ps1`                       | Windows 向けのインストーラ        |
| `SHA256SUMS`                        | 上記すべてのファイルの SHA-256    |

### target

`<target>` は次の8つです．.NET の RID とは glibc の Linux だけ名前が異なります．

| target              | .NET RID           | 形式      |
| ------------------- | ------------------ | --------- |
| `win-x64`           | `win-x64`          | `.zip`    |
| `win-arm64`         | `win-arm64`        | `.zip`    |
| `linux-glibc-x64`   | `linux-x64`        | `.tar.gz` |
| `linux-glibc-arm64` | `linux-arm64`      | `.tar.gz` |
| `linux-musl-x64`    | `linux-musl-x64`   | `.tar.gz` |
| `linux-musl-arm64`  | `linux-musl-arm64` | `.tar.gz` |
| `osx-x64`           | `osx-x64`          | `.tar.gz` |
| `osx-arm64`         | `osx-arm64`        | `.tar.gz` |

winget のマニフェスト等のため Windows 向けはzipとします．

### アーカイブの構成

アーカイブはトップレベルのディレクトリを持たず，次のファイルを直接含みます．

- `pluspim` (Windows では `pluspim.exe`)
- `LICENSE`
- `README.md`

`pluspim` は .NET ランタイムを含む単一ファイルの実行ファイルで，ほかのファイルなしで動きます．
次の設定で `dotnet publish` します．

```sh
dotnet publish PlusPim/PlusPim.csproj -c Release -r <rid> --self-contained \
  -p:PublishSingleFile=true \
  -p:PublishReadyToRun=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:GenerateDocumentationFile=false
```

- `InvariantGlobalization` が有効なので，ICU は不要です
- SDK は `global.json` でで固定します
- NuGet の依存は `packages.lock.json` で固定します
- CI では `Directory.Build.props` を用いて，lock file と食い違う依存は取り込みません

### `SHA256SUMS`

GNU coreutils の `sha256sum --text` の形式です．

```
<sha256>  pluspim-0.6.0-linux-glibc-x64.tar.gz
```

Linux では `sha256sum -c --ignore-missing SHA256SUMS` で検証できます．
インストーラは，取得したアーカイブの名前と完全に一致する行がない場合や，ハッシュが一致しない場合は，展開する前に失敗します．

#!/bin/sh
# 配布用のpluspimをpublishする: scripts/build/publish.sh <rid> <out>
# <out>はリポジトリのルートからの相対パス
# linux-x64はAOTでビルドホストのglibcにリンクされるので，glibc 2.28のコンテナでビルドして利用者の下限を保つ
set -eu

GLIBC_IMAGE=docker.io/library/almalinux:8@sha256:768bedc30fe0a536adb0a65d2eddb7caad364e514fbddd0ae9e026975f590342

rid=$1
out=$2
cd "$(dirname "$0")/.."
case "$rid" in
  linux-x64)
    dotnet_root=$(dirname "$(readlink -f "$(command -v dotnet)")")
    docker run --rm -v "$dotnet_root:/opt/dotnet:ro" -v "$PWD:/w" -w /w \
      -e DOTNET_CLI_TELEMETRY_OPTOUT=1 -e DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 \
      "$GLIBC_IMAGE" sh -c '
        set -eu
        dnf install -y -q clang zlib-devel
        /opt/dotnet/dotnet publish src/PlusPim/PlusPim.csproj -c Release -r linux-x64 -o "$1"
        chown -R "$2" "$1" artifacts
      ' sh "$out" "$(id -u):$(id -g)"
    ;;
  *)
    dotnet publish src/PlusPim/PlusPim.csproj -c Release -r "$rid" -o "$out"
    ;;
esac

#!/bin/sh
# Installs PlusPim for the current user on Linux and macOS.
#
# Environment variables:
#   PLUSPIM_VERSION      Version to install (default: latest release)
#   PLUSPIM_TARGET       Release target such as linux-musl-x64 (default: detected)
#   PLUSPIM_INSTALL_DIR  Install directory (default: ~/.local/bin)
#
# See doc/release.md for the release asset contract.

# Everything runs inside main so that a truncated download does nothing.
set -euo pipefail

REPO="https://github.com/hinshiba/PlusPim"

say() { printf 'pluspim-install: %s\n' "$*" >&2; }
die() { say "error: $*"; exit 1; }
has() { command -v "$1" > /dev/null 2>&1; }

check_deps() {
    missing=""
    for c in uname mktemp tar awk; do
        has "$c" || missing="$missing $c"
    done
    has curl || has wget || missing="$missing curl|wget"
    has sha256sum || has shasum || missing="$missing sha256sum|shasum"
    [ -z "$missing" ] || die "missing commands:$missing"
}

# Restrict redirects to https as well.
curl_https() { curl -fsSL --proto '=https' --proto-redir '=https' --tlsv1.2 "$@"; }

# fetch <url> <dest>
fetch() {
    if has curl; then
        curl_https -o "$2" "$1"
    else
        wget -q -O "$2" "$1"
    fi
}

# The redirect target of /releases/latest avoids the API rate limit.
resolve_version() {
    if has curl; then
        url="$(curl_https -I -o /dev/null -w '%{url_effective}' "$REPO/releases/latest")"
    else
        url="$(wget -S --spider "$REPO/releases/latest" 2>&1 | sed -n 's/^ *[Ll]ocation: *//p' | tail -n 1 | tr -d '\r')"
    fi
    tag="${url##*/}"
    case "$tag" in
        v[0-9]*) printf '%s\n' "${tag#v}" ;;
        *) die "cannot resolve the latest release from '$url'" ;;
    esac
}

is_musl() {
    for f in /lib/ld-musl-*.so.1; do
        [ -e "$f" ] && return 0
    done
    # Alpine ldd rejects --version, so check for the loader file first.
    ldd --version 2>&1 | grep -qi musl
}

detect_target() {
    arch="$(uname -m)"
    case "$arch" in
        x86_64 | amd64) arch=x64 ;;
        aarch64 | arm64) arch=arm64 ;;
        *) die "unsupported architecture: $arch" ;;
    esac
    os="$(uname -s)"
    case "$os" in
        Linux)
            if is_musl; then
                printf 'linux-musl-%s\n' "$arch"
            else
                printf 'linux-glibc-%s\n' "$arch"
            fi
            ;;
        Darwin)
            # uname -m reports x86_64 in a shell running under Rosetta 2.
            if [ "$(sysctl -n hw.optional.arm64 2> /dev/null || true)" = 1 ]; then
                arch=arm64
            fi
            printf 'osx-%s\n' "$arch"
            ;;
        *) die "unsupported OS: $os (use install.ps1 on Windows)" ;;
    esac
}

sha256() {
    if has sha256sum; then
        sha256sum "$1"
    else
        shasum -a 256 "$1"
    fi | awk '{ print $1 }'
}

main() {
    check_deps
    target="${PLUSPIM_TARGET:-$(detect_target)}"
    version="${PLUSPIM_VERSION:-$(resolve_version)}"
    version="${version#v}"
    # Reject values outside the contract because they end up in URLs and paths.
    case "$version" in
        "" | [!0-9]* | *[!0-9A-Za-z.+-]*) die "invalid version: $version" ;;
    esac
    case "$target" in
        linux-glibc-x64 | linux-glibc-arm64 | linux-musl-x64 | linux-musl-arm64 | osx-x64 | osx-arm64) ;;
        *) die "invalid target: $target" ;;
    esac
    dir="${PLUSPIM_INSTALL_DIR:-$HOME/.local/bin}"
    name="pluspim-$version-$target.tar.gz"
    base="$REPO/releases/download/v$version"

    tmp="$(mktemp -d)"
    staged=""
    trap 'rm -rf "$tmp"; [ -z "$staged" ] || rm -f "$staged"' EXIT
    trap 'exit 1' HUP INT TERM

    say "downloading $name"
    fetch "$base/$name" "$tmp/$name" || die "cannot download $base/$name"
    fetch "$base/SHA256SUMS" "$tmp/SHA256SUMS" || die "cannot download $base/SHA256SUMS"

    expected="$(awk -v n="$name" '$2 == n { print $1 }' "$tmp/SHA256SUMS")"
    [ -n "$expected" ] || die "$name is not listed in SHA256SUMS"
    [ "$(sha256 "$tmp/$name")" = "$expected" ] || die "checksum mismatch: $name"

    mkdir "$tmp/x"
    tar -xzf "$tmp/$name" -C "$tmp/x"
    [ -f "$tmp/x/pluspim" ] && [ ! -L "$tmp/x/pluspim" ] || die "$name does not contain a regular file 'pluspim'"

    # Stage next to the target so that an interrupted install never leaves a broken binary.
    mkdir -p "$dir"
    staged="$(mktemp "$dir/.pluspim.XXXXXX")"
    cp "$tmp/x/pluspim" "$staged"
    chmod 755 "$staged"
    mv -f "$staged" "$dir/pluspim"
    staged=""

    "$dir/pluspim" --version > /dev/null 2>&1 ||
        die "$dir/pluspim does not run; set PLUSPIM_TARGET if '$target' is wrong"
    say "installed $("$dir/pluspim" --version) to $dir/pluspim"

    case ":$PATH:" in
        *":$dir:"*) ;;
        *) say "$dir is not in PATH; add 'export PATH=\"$dir:\$PATH\"' to your shell profile" ;;
    esac
}

main "$@"

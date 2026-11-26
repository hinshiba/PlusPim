# サンプル

動作確認用のMIPSアセンブリ．
同じプログラムを[遅延スロット](../docs/runtime.md#遅延スロット)の有無で2つのディレクトリに分けている．

| ディレクトリ      | 起動のオプション                              |
| ----------------- | --------------------------------------------- |
| `delay-slots/`    | なし                                          |
| `no-delay-slots/` | `--no-delayed-branches`，`--no-delayed-loads` |

`syscall`を使うので，同じディレクトリの`kseg.asm`を一緒に読み込む．
このディレクトリをVS Codeで開くと，`.vscode/launch.json`の`Delay Slots`と`No Delay Slots`で開いているファイルを起動できる．

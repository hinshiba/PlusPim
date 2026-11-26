# SPIMのテスト

[SPIM](https://sourceforge.net/projects/spimsimulator/)の回帰テスト`Tests/`のうち，MIPS-Iの命令を検査するファイル．
`tests/PlusPim.Conformance.Tests`が実行する．

- 取得元: `https://sourceforge.net/p/spimsimulator/code/HEAD/tree/Tests/` のリビジョンr764
- ライセンス: BSD-3-Clause．各ファイルの先頭と[LICENSE](LICENSE)を参照

内容は変更しない．
除外する行は`tests/PlusPim.Conformance.Tests/SpimSuite.cs`に行番号で書くので，ファイルを差し替えると行番号がずれる．
`SHA256SUMS`は`lint/ci/third-party.sh`が検査する．

| ファイル        | 内容                                  | SPIMの起動のオプション                    |
| --------------- | ------------------------------------- | ----------------------------------------- |
| `tt.core.s`     | エンディアンによらない命令と疑似命令  | なし                                      |
| `tt.le.s`       | リトルエンディアンに依存する命令      | なし                                      |
| `tt.bare.s`     | 遅延スロットと疑似命令                | `-delayed_branches`，`-delayed_load`      |
| `tt.alu.bare.s` | 疑似命令を使わない演算と分岐          | `-bare`                                   |

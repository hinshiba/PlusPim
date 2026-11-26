# 外部のテストとの適合

他の実装が作ったテストを`tests/PlusPim.Conformance.Tests`で実行し，PlusPimの実行がR3000やSPIMと一致するかを確かめる．

| テスト                                                         | ライセンス   | 置き場所                         |
| -------------------------------------------------------------- | ------------ | -------------------------------- |
| [SPIM](https://sourceforge.net/projects/spimsimulator/)の`Tests/` | BSD-3-Clause | `third_party/spim/`              |
| [SingleStepTests/r3000](https://github.com/SingleStepTests/r3000) | MIT          | `artifacts/sst/r3000`(取得する) |

## 実行

```
sh scripts/test/fetch-sst.sh
dotnet test --project tests/PlusPim.Conformance.Tests
```

`scripts/test/fetch-sst.sh`は固定したコミットから取得し，`scripts/test/sst-r3000.sha256`で検証する．
データがなければ`SstTests`はスキップする．CIは取得してから実行する．
`PLUSPIM_SST_DIR`で別の置き場所を指定できる．

## SPIM

SPIMの方言は`SpimDialect`で同じ意味のPlusPimの命令へ書き換える．

- 空白で区切ったオペランドをカンマで区切る
- 即値をとる`add`や`bne`などは，SPIMと同じ命令数のI形式か`$at`を使う列へ展開する．`tt.alu.bare.s`は番地を直書きするので，命令数を変えられない
- ラベルを使うメモリオペランドと16ビットに収まらないオフセットは，`$at`を基底にする
- `.half`と`.word`の前に`.align`を補う．SPIMはその位置で揃えるが，PlusPimは揃えない

書き換えられないブロックは`SpimSuite`に行番号と理由で除外する．
主な理由はFPU，MIPS32の命令，TLB，PlusPimにない疑似命令である．

`fail`へ移ると，その直前の2命令を元のファイルの行で報告する．

## SingleStepTests

PSXのR3000Aをaresで1命令ずつ実行した記録である．
各ケースの命令語を`R3000Decoder`でPlusPimの命令へ変換し，初期状態を`Debuggee`へ注入して1ステップ進める．

- PlusPimはテキストセグメントにしか命令を置けないので，命令の番地の上位4ビットを落とす．テキストセグメントに入らないケースは飛ばす
- 移した番地から決まる戻りアドレス，PC，EPC，PC相対の分岐先は，比べる前に同じだけ移す
- 例外は，最終状態のPCがPSXの例外ベクタ`0x80000080`かどうかで判定し，ExcCode，EPC，BDビットを比べる
- PSXに固有の`TAR`と，SSTにない`BadVAddr`は比べない

## 既知の差

PlusPimの仕様による差は除外する．

- 0除算，テキストへのロードとストア，遅延スロットの分岐はランタイムエラーになる
- 非整列のジャンプ先は，R3000と同じくジャンプ先の取り出しで`AdEL`になる．EPC + 4へ戻るハンドラでは非整列のまま進むので，SPIMのテストは終わらない

SPIMとR3000で異なる点は，R3000に合わせる．

- ロード遅延スロットの命令がロード先へ書くと，SPIMはロードの値を残す．R3000は命令の値を残し，PlusPimも同じである

次はR3000と異なるPlusPimの挙動で，修正していない．
修正したら除外を外す．

| 差                                                                                                     | 検出したテスト        |
| ------------------------------------------------------------------------------------------------------ | --------------------- |
| ユーザーモードでの`0x80000000`以上へのロードとストアが`AdEL`，`AdES`でなく`TextAccess`か成功になる   | SPIM `tt.core.s`，`tt.le.s` |
| 完了していないロードと同じレジスタへのロードで，先のロードを書き込む．R3000は取り消す                 | SingleStepTests 7命令 |

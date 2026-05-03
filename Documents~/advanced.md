# 上級者向けトピック

応用的な使い方とトラブルシューティング、FAQ をまとめます。

## 目次

- [タイルスナップ](#タイルスナップ)
- [複数ストロークの境界線](#複数ストロークの境界線)
- [Speed Reduction Dot Line と対面通行](#speed-reduction-dot-line-と対面通行)
- [トラブルシューティング](#トラブルシューティング)
- [FAQ](#faq)

---

## タイルスナップ

Dashed Line / Rumble Strips / Speed Reduction Dot Line などの周期パターンは、タイル長と周期が整合していないとタイル境界で切れ目が出ます。

各セクションに用意されている **Snap & center pattern** ボタンを押すと、Spacing と Start Offset がタイル長に合うよう自動補正されます。整合していない状態のときは、ボタン上部に黄色の HelpBox が表示されます。

対象セクション:

- Boundary Lines > LineStyle (Dashed)
- Boundary Lines > LineStyle (Diamond)
- Lanes > Rumble Strips
- Lanes > Speed Reduction Dot Line

---

## 複数ストロークの境界線

各 Boundary Line は複数のストローク (個別の線) で構成できます。中央二重線などはこの方式で作成します。

操作手順:

1. 対象の境界線 Foldout を展開
2. **+ Add Stroke** ボタンで追加
3. 追加されたストロークの `LineStyle` を編集 (`Type`, `Color`, `Width` 等)
4. 隣接ストローク間の **spacing** (中心-中心間隔) を調整

代表的な構成例:

| 構成                     | ストローク                        | spacing   |
| ------------------------ | --------------------------------- | --------- |
| 通常の単線               | 1 本                              | —         |
| 中央二重線 (黄実線 × 2)  | 2 本 (両方 Solid Yellow)          | 約 0.15 m |
| 二重線 (白実線 + 黄実線) | 2 本 (Solid White + Solid Yellow) | 約 0.15 m |

---

## Speed Reduction Dot Line と対面通行

`Side = Both` または対面通行の道路では、ドットの slant は車線端と進行方向の組み合わせで自動的に反転されます。

両車線に同じ正の `Slant (m)` 値を設定すれば、両車線とも視覚的に正しいドットラインが生成されます (Backward 車線の slant 反転は自動で行われます)。

```
Lane 1: Direction = Forward,  Slant = +0.3
Lane 2: Direction = Backward, Slant = +0.3
```

---

## トラブルシューティング

### Q. Generate ボタンを押しても何も起きない

ウィンドウ最上部の `Preset Asset` が `None` になっていないか確認してください。`RoadConfigAsset` を選択するか、Quick Apply を押してから再度 Generate してください。

### Q. 「Output folder must be inside the project」と表示される

`Output Folder` がプロジェクト外になっています。`Assets/...` または `Packages/...` で始まるパスに変更してください。

### Q. 上書き確認ダイアログが毎回出る

同じ `Output Folder` × `Name Prefix` の組み合わせで再生成すると毎回確認が出ます。バリエーションを生成したい場合は `Name Prefix` を変えるか、`Output Folder` を分けてください。

### Q. 生成された Material のテクスチャが Pink (マゼンタ) になる

レンダーパイプラインと Material のシェーダが一致していません。`Output > Pipeline` を `AutoDetect` または手動で正しいパイプラインに設定し、再生成してください。

### Q. Dashed Line の dash がタイル境界で切れている

該当 stroke の **Snap & center pattern** ボタンを押してください。または `Tile Length` を周期 (Dash Length + Dash Gap) の整数倍に手動で揃えてください。

### Q. Rumble Strips が車線境界線と重なる

`Lanes > Rumble Strips > Edge Inset (m)` を増やしてください。

### Q. Speed Reduction Dot Line の傾きが想定と逆になる

車線の `Direction` (Forward / Backward) を確認してください。

### Q. プレビュー画像が更新されない

フッターの **Refresh Preview** ボタンを押してください。

---

## FAQ

### Q. URP / Built-in 以外のレンダーパイプラインに対応していますか?

現時点では Built-in と URP のみサポートしています。HDRP は未対応です (テクスチャ自体は標準 PNG なので、HDRP 用 Material を手動で構成すれば利用は可能)。

### Q. 同じ設定から確実に同じテクスチャを生成したい

`Output > Seed` を固定してください。同じシードなら決定的に同じテクスチャが生成されます。

### Q. 生成サイズはどこまで上げられますか?

`Resolution` で 4096 まで指定可能です。生成時間とメモリ消費は解像度の 2 乗で増加します。

### Q. 既存の `RoadConfigAsset` から派生プリセットを作りたい

元の Preset Asset を読み込んだ状態で **Save Current as New Preset** をクリックすると、現在のパラメータが新しいアセットとして保存されます。

### Q. テクスチャはタイリング可能ですか?

はい。U 軸方向・V 軸方向ともシームレスにタイリング可能なよう設計されています。

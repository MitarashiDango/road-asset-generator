# 路面標示の追加実装ガイド

## 概要

路面標示を追加するには、既存のプリミティブと `MarkingPattern` を組み合わせるか、
`IMarkingShape` を直接実装します。多くの場合、新しいプリミティブを作成して
`MarkingPattern` に渡すだけで実現可能です。

## アーキテクチャ全体像

```
IShapePrimitive (図形定義)
  ↓
MarkingPattern (配置制御)  ← このレイヤー
  ↓
IMarkingShape
  ↓ (CompositeShapes で合成可能)
LineStroke → RoadTextureBaker
  ↑
RoadConfig / LineStyle / LaneConfig (設定値)
```

## パターン別の実装方法

### パターン A: 既存プリミティブの組み合わせで実現可能な場合

新しいコードを追加する必要はありません。`MarkingPattern` のパラメータだけで表現可能です。

```csharp
// 例: 楕円の破線
var ellipseDashed = new MarkingPattern(
    EllipsePrimitive.Instance,
    sizePx: 40f,
    gapPx: 60f,
    offsetPx: 0f);

// 例: 三角形の Y-shear パターン
var triangleSheared = new MarkingPattern(
    TrianglePrimitive.Instance,
    sizePx: 30f,
    gapPx: 50f,
    offsetPx: 0f,
    yShearPx: 20f);  // 斜め配置
```

### パターン B: 新しい図形が必要な場合

1. `IShapePrimitive` を実装します（→ [図形プリミティブの追加実装ガイド](dev-adding-shapes.md)を参照）。
2. `MarkingPattern` と組み合わせて使用します。

### パターン C: 合成シェイプで複合標示を作る場合

`UnionShape`、`IntersectionShape`、`SubtractShape` を使用して複数の標示を合成可能です。

```csharp
// 例: 矩形破線から中央の楕円を切り抜いた標示
var base_ = new MarkingPattern(RectanglePrimitive.Instance, 80f, 40f, 0f);
var hole = new MarkingPattern(EllipsePrimitive.Instance, 80f, 40f, 0f);
var cutout = new SubtractShape(base_, hole);

// 例: 異なるパターンの重ね合わせ
var layer1 = new MarkingPattern(RectanglePrimitive.Instance, 100f, 100f, 0f);
var layer2 = new MarkingPattern(TrianglePrimitive.Instance, 50f, 150f, 50f);
var combined = new UnionShape(layer1, layer2);
```

### パターン D: `IMarkingShape` を直接実装する場合

`MarkingPattern` では表現できない特殊なロジック（例: V 軸繰り返しを使わない、
独自の走査最適化が必要など）の場合は、`IMarkingShape` を直接実装します。

既存例: `SolidShape`（V 軸方向に途切れなく続く実線。周期繰り返しが不要なため直接実装）。

## エディタ UI との連携（LineType 経由の標示追加）

エディタの線種ドロップダウンから選べる新しい標示を追加するには、
設定値 → ファクトリ → エディタ UI の 3 箇所を変更します。

### 1. `LineType` 列挙体に値を追加

`Editor/RoadConfig.cs`:

```csharp
public enum LineType { None, Solid, Dashed, Diamond, /* 追加 → */ Wave }
```

### 2. `LineStyle` に必要なパラメータを追加

`Editor/RoadConfig.cs` の `LineStyle` クラスにパラメータを追加し、
`Clone()` メソッドにもコピー処理を追加します。

```csharp
public class LineStyle
{
    // ... 既存フィールド ...

    // Wave 専用パラメータ
    [Min(0.05f)] public float waveAmplitudeMeters = 0.1f;
    [Min(0.05f)] public float waveLengthMeters = 2f;

    public LineStyle Clone()
    {
        return new LineStyle
        {
            // ... 既存フィールド ...
            waveAmplitudeMeters = waveAmplitudeMeters,
            waveLengthMeters = waveLengthMeters,
        };
    }
}
```

### 3. `StrokeResolver.AddStrokeAt` にファクトリ分岐を追加

`Editor/Baking/StrokeResolver.cs` の `AddStrokeAt` メソッド内の `switch` に新しい `case` を追加します。

```csharp
case LineType.Wave:
    shape = new MarkingPattern(
        new WavePrimitive(style.waveAmplitudeMeters * pxPerMx / halfW),
        style.waveLengthMeters * pxPerMy,
        0f,    // gapPx (連続波なのでギャップなし)
        style.dashOffsetMeters * pxPerMy);
    break;
```

ポイント:
- メートル単位のパラメータを `pxPerMx` / `pxPerMy` でピクセルに変換します。
- プリミティブのコンストラクタには**正規化された値**を渡します（例: `amplitudePx / halfW`）。
- `MarkingPattern` のコンストラクタには**ピクセル単位の値**を渡します。

### 4. エディタ UI にパラメータ入力欄を追加

`Editor/UI/RoadAssetGeneratorWindow.cs` の線種描画セクションに、
`LineType` に応じた条件分岐で入力欄を追加します。

## `MarkingPattern` のパラメータ設計ガイド

### コンストラクタ引数

| 引数 | 単位 | 説明 |
|---|---|---|
| `primitive` | - | 配置する図形プリミティブ |
| `sizePx` | px | 1 マークの V 方向サイズ |
| `gapPx` | px | マーク間の V 方向ギャップ。0 で連続 |
| `offsetPx` | px | V 方向の開始オフセット |
| `yShearPx` | px | Y-shear 量。U 位置に応じて V 周期の位相をシフト |

### Y-shear の効果

Y-shear は繰り返しパターン全体の配置を傾ける変換です。
個々のマーク形状は変化しません（形状のシアーは `ParallelogramPrimitive` を使用）。

```
yShearPx = 0              yShearPx > 0
+---+---+---+             +---+---+---+
|[M]|[M]|[M]|             |  [|M]  |  |
|   |   |   |             | [M|]  [|M]|
|[M]|[M]|[M]|             |[M]| [M|] |
+---+---+---+             +---+---+---+
```

### 処理フロー

`MarkingPattern.TestPixel` の内部処理:

1. **U 正規化**: `uNorm = (x - xCenter) / halfWidthPx`
2. **Y-shear** (周期計算前): `yEff = y - uNorm * 0.5f * yShearPx`
3. **V 周期計算**: `p = (yEff - offset) % period`、ギャップ判定
4. **V 正規化**: `v = p / sizePx`
5. **プリミティブ判定**: `primitive.Contains(uNorm, v, out duNorm)`
6. **du 逆変換**: `du = duNorm * halfWidthPx`
7. **V 端判定**: 1 マーク内の先頭と末尾の 1 ピクセルを V 端として扱う

## 合成シェイプの使い分け

| クラス | 動作 | 用途例 |
|---|---|---|
| `UnionShape` | いずれかに含まれればヒット | 複数パターンの重ね合わせ |
| `IntersectionShape` | すべてに含まれる場合のみヒット | マスク領域との交差 |
| `SubtractShape` | ベースに含まれマスクに含まれない場合 | 切り抜き・くり抜き |

合成シェイプは `IMarkingShape` レベルで動作するため、
`MarkingPattern` 同士だけでなく `SolidShape` や他の合成シェイプとも組み合わせ可能です。

## 注意点

- `MarkingPattern` には**ピクセル単位**の値を渡し、プリミティブには**正規化された値**を渡します。
  単位を間違えると描画結果が大きく崩れます。
- `gapPx = 0` かつ `sizePx > 0` の場合、マークが隙間なく連続します（実質的に `SolidShape` + 図形判定）。
- `sizePx + gapPx` が非常に小さい値（0.5 px 未満）の場合、描画されません（ゼロ除算防止）。
- 合成シェイプ内では `CanSkipRow` の最適化が制限されます。
  `UnionShape` はすべての子が skip 可能な場合のみスキップし、
  `IntersectionShape` はいずれかの子がスキップ可能ならスキップします。

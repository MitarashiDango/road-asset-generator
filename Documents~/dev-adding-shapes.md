# 図形プリミティブの追加実装ガイド

## 概要

`IShapePrimitive` を実装することで、新しい幾何形状を追加できます。
プリミティブは正規化座標空間でのみ動作し、路面標示の配置やテクスチャベイクの詳細を知る必要はありません。

## アーキテクチャ上の位置づけ

```
IShapePrimitive (図形定義)   ← このレイヤー
  ↓
MarkingPattern (配置制御)
  ↓
IMarkingShape → RoadTextureBaker
```

プリミティブは「この点が図形の内部かどうか」だけを判定します。
V 軸方向の繰り返し・間隔・Y-shear などの配置は `MarkingPattern` が担当するため、
プリミティブ側で考慮する必要はありません。

## 座標系

- **u 軸**: 道路の横方向。中心が `0`、標準幅端が `+1` / `-1`。
- **v 軸**: 道路の縦方向（1 マーク内の位置）。`0` がマーク先頭、`1` がマーク末尾。

```
u = -1       u = 0       u = +1
  |            |            |
  +---+--------+--------+---+  v = 0 (マーク先頭)
  |            |            |
  |         [図形領域]       |
  |            |            |
  +---+--------+--------+---+  v = 1 (マーク末尾)
```

## 実装手順

### 1. クラスを作成する

`Editor/Shapes/Primitives/` に新しいファイルを作成します。

```csharp
using UnityEngine;

namespace MitarashiDango.RoadAssetGenerator
{
    public sealed class StarPrimitive : IShapePrimitive
    {
        public static readonly StarPrimitive Instance = new StarPrimitive();

        public bool Contains(float u, float v, out float duNorm)
        {
            duNorm = u;
            // ここに図形の内外判定ロジックを実装
            // 例: 星形の判定
            return /* 判定結果 */;
        }

        public float MaxUExtent => 1f;
        public bool HasDiagonalEdges => true;
    }
}
```

### 2. `IShapePrimitive` の各メンバーを実装する

#### `Contains(float u, float v, out float duNorm)`

正規化座標 `(u, v)` が図形の内部にある場合 `true` を返します。

- `duNorm`: 中心からの U 方向の符号付き距離を出力します。Baker の縁ぼかし処理で使用されます。
  - 通常は `duNorm = u` で十分です。
  - 平行四辺形のように中心がシフトする場合は、シフト後の値を出力します。

#### `MaxUExtent`

形状が u 軸方向に占める最大範囲です。

- 形状が `|u| <= 1` に収まる場合は `1f` を返します（矩形・楕円・三角形など）。
- 形状が `|u| > 1` に及ぶ場合は、最大到達範囲を返します。
  - 例: `ParallelogramPrimitive` は `1f + Mathf.Abs(_shearNorm) * 0.5f` を返します。
- この値を正しく返さないと、ピクセル走査範囲が不足し、描画が切れます。

#### `HasDiagonalEdges`

- 図形の境界に斜辺がある場合は `true` を返します（楕円・三角形・平行四辺形など）。
- 水平・垂直の辺のみの場合は `false` を返します（矩形など）。
- Baker はこの値に基づいて縁ソフトニングの方式を切り替えます。
  - `true`: `|du|` ベースの距離フェードを使用（斜辺のジャギーを軽減）
  - `false`: 固定 X 境界のフェードを使用（直線系で十分）

### 3. ステートレスかどうかを判断する

- **パラメータなし**（矩形・楕円・三角形）: シングルトンにできます。
  `public static readonly XxxPrimitive Instance = new XxxPrimitive();` を定義します。
- **パラメータあり**（平行四辺形のシアー量など）: 都度インスタンスを生成します。

## 既存プリミティブの実装例

| クラス | 判定式 | MaxUExtent | HasDiagonalEdges |
|---|---|---|---|
| `RectanglePrimitive` | `|u| <= 1` | 1.0 | false |
| `EllipsePrimitive` | `u^2 + (2(v-0.5))^2 <= 1` | 1.0 | true |
| `TrianglePrimitive` | `|u| <= 1 - v` | 1.0 | true |
| `ParallelogramPrimitive` | `|u - shear*(v-0.5)| <= 1` | 1.0 + \|shear\|*0.5 | true |

## 使い方

作成したプリミティブは `MarkingPattern` と組み合わせて使います。

```csharp
// 星形マークを 2m 間隔で繰り返す路面標示
var shape = new MarkingPattern(
    StarPrimitive.Instance,
    sizePx: 50f,      // 1 マークの高さ
    gapPx: 100f,       // マーク間の隙間
    offsetPx: 0f);     // 開始位置

// 合成シェイプとの組み合わせ
var unionShape = new UnionShape(
    new MarkingPattern(RectanglePrimitive.Instance, 50f, 100f, 0f),
    new MarkingPattern(StarPrimitive.Instance, 30f, 120f, 25f));
```

## 注意点

- プリミティブは `MarkingPattern` を介さず単独では `IMarkingShape` として使用できません。
  V 軸繰り返しが不要な場合は `SolidShape` を使用するか、`gapPx = 0` の `MarkingPattern` を使用してください。
- `Contains` はピクセルごとに呼ばれるため、パフォーマンスに配慮してください。
  三角関数や平方根の使用は最小限に留め、可能なら二乗比較を使います。
- `duNorm` は Baker が `duNorm * halfWidthPx` でピクセル空間に逆変換します。
  中心 (u=0) で `duNorm = 0`、端 (u=+/-1) で `duNorm = +/-1` が基本です。

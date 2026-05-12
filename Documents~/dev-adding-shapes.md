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
| `TextureMaskPrimitive` | グレースケールテクスチャのサンプリング値 >= threshold | テクスチャから自動算出 | true |
| `PolygonPrimitive` | Winding Number ≠ 0 (多角形の内外判定) | 全頂点の \|u\| の最大値 | true |

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

---

## テクスチャマスクで図形を定義する

数式でプリミティブを実装する代わりに、グレースケールテクスチャで図形を定義できます。
`TextureMaskPrimitive` は他のプリミティブ（Rectangle, Ellipse 等）と同列の `IShapePrimitive` 実装であり、
`MarkingPattern` や合成シェイプ（`UnionShape` 等）と自由に組み合わせて使用できます。

### テクスチャの仕様

| 項目 | 内容 |
|---|---|
| 形式 | 任意サイズの PNG（推奨 64×64 〜 128×128） |
| 色 | 白 (1.0) = 内部、黒 (0.0) = 外部 |
| 座標系 | テクスチャ左端が u = -1、右端が u = +1、下端が v = 0、上端が v = 1 |

テクスチャの各ピクセルのグレースケール値が **threshold**（デフォルト 0.5）以上であれば「図形の内部」と判定されます。
テクスチャの Read/Write 設定は問いません。読み取り不可テクスチャも `FromTexture()` が内部で自動対応します。

### 補間アルゴリズムの選び方

`TextureMaskSampling` enum で補間方式を指定します。

| 方式 | 特徴 | 適したケース |
|---|---|---|
| **Bilinear** | 4 テクセルの線形補間。高速 | 十分な解像度のテクスチャ、直線が多い図形 |
| **Bicubic** | 16 テクセルの Catmull-Rom 補間。滑らか | 低解像度テクスチャ、曲線が多い図形 |

### 使い方

`TextureMaskPrimitive.FromTexture()` ファクトリメソッドで `Texture2D` から直接生成できます。

```csharp
// テクスチャマスクを MarkingPattern と組み合わせる例
var primitive = TextureMaskPrimitive.FromTexture(myTexture, threshold: 0.5f, TextureMaskSampling.Bilinear);
var shape = new MarkingPattern(primitive, sizePx: 60f, gapPx: 40f, offsetPx: 0f);
```

`float[]` を直接渡すコンストラクタも利用可能です。

```csharp
// 事前に抽出済みのピクセルデータを渡す場合
var primitive = new TextureMaskPrimitive(pixels, width, height, threshold: 0.5f, TextureMaskSampling.Bicubic);
```

---

## 多角形ポリゴンで図形を定義する

`PolygonPrimitive` を使用すると、多角形の頂点データで図形を定義できます。
テクスチャマスクとは異なり、パラメータによる頂点の変形が可能で、穴（ホール）のある複合図形もサポートします。

### データ構造

`PolygonData` は以下の要素で構成されます:

| 要素 | 説明 |
|---|---|
| `PolygonRing` | 閉じた多角形リング。CCW (反時計回り) = 外周、CW (時計回り) = 穴 |
| `PolygonVertex` | 名前付き頂点。position は正規化座標 u[-1,+1], v[0,1] |
| `PolygonEdge` | 辺タイプ。現在は Linear のみ。将来ベジェ等に拡張可能 |
| `VertexGroup` | BlendShape ライクな頂点グループ。weight × delta の加算で頂点を変形 |

### 座標系

テクスチャマスクと同じ正規化座標系を使用します:

- **u 軸**: 道路の横方向。中心が `0`、標準幅端が `+1` / `-1`。
- **v 軸**: 道路の縦方向。`0` がマーク先頭、`1` がマーク末尾。

### 巻き方向の規約

- **CCW (反時計回り)**: 外周ポリゴンを定義します。
- **CW (時計回り)**: 穴（ホール）を定義します。

Winding Number アルゴリズムにより、外周の内部かつ穴の外部にある点のみが「図形の内部」と判定されます。

### 頂点グループ（BlendShape ライク）

各頂点グループは名前とデルタ（オフセット）のリストを持ちます。
`MarkingShape` クラスから weight を指定して適用することで、図形の形状をパラメトリックに制御できます。

```
最終位置 = 基本位置 + Σ(weight_i × delta_i)
```

頂点はすべて文字列名で参照されるため、頂点の挿入・削除に対して安定です。

### 使い方

```csharp
// PolygonData をコードで定義する例
var data = new PolygonData();
var ring = new PolygonRing { label = "Arrow" };
ring.vertices.Add(new PolygonVertex("tip", new Vector2(0f, 0f)));
ring.vertices.Add(new PolygonVertex("right", new Vector2(1f, 0.5f)));
ring.vertices.Add(new PolygonVertex("notch", new Vector2(0f, 0.3f)));
ring.vertices.Add(new PolygonVertex("left", new Vector2(-1f, 0.5f)));
ring.EnsureEdgeCount();
data.rings.Add(ring);

// PolygonPrimitive を MarkingPattern と組み合わせる
var primitive = PolygonPrimitive.FromData(data);
var shape = new MarkingPattern(primitive, sizePx: 60f, gapPx: 40f, offsetPx: 0f);
```

```csharp
// PolygonDataAsset から生成し、頂点グループを適用する例
var weights = new Dictionary<string, float> { { "Wide", 0.5f } };
var primitive = PolygonPrimitive.FromData(asset.data, weights);
```

### PolygonDataAsset の編集

`PolygonDataAsset` は ScriptableObject としてプロジェクトに保存できます。

- **作成**: Project ウィンドウで右クリック → Create → Road Asset Generator → Polygon Shape
- **編集**: メニュー Tools → Road Asset Generator → Polygon Editor でビジュアルエディタを開く

エディタ機能:
- キャンバス上で頂点をドラッグして移動
- 右クリックで頂点の追加
- Delete キーで選択頂点の削除
- プリセット形状（三角形、矩形、穴）の追加
- 頂点グループの編集とプレビュー

---

## 方式の比較と選び方

図形プリミティブの定義には 3 つの方式があります。いずれの方式でも、`IMarkingShape` の実装（または `MarkingPattern` との組み合わせ）は別途必要です。

| 観点 | コード実装 (`IShapePrimitive`) | テクスチャマスク (`TextureMaskPrimitive`) | 多角形ポリゴン (`PolygonPrimitive`) |
|---|---|---|---|
| 精度 | 数式どおりの正確な形状 | テクスチャ解像度に依存 | 頂点で定義された正確な形状 |
| パフォーマンス | 通常はより高速 | バイリニア: 同等、バイキュービック: やや遅い | 頂点数に依存（少数なら高速） |
| 図形定義のコスト | C# クラスの実装が必要 | PNG を用意するだけ | 頂点データの定義（エディタ利用可） |
| パラメータ化 | シアー量等を引数で受け取れる | テクスチャ固定（変更には別テクスチャが必要） | 頂点グループで変形可能 |
| 穴のサポート | 個別実装が必要 | 不可 | CW リングで自然にサポート |
| 曲線 | 数式で自由に表現 | テクスチャで表現 | 将来の辺タイプ拡張で対応予定 |

- **コード実装**: 楕円や平行四辺形など、数式で簡潔に表せる図形や、高頻度で使用する基本図形に適している。
- **テクスチャマスク**: ロゴや複雑な曲線など、数式やポリゴンでの表現が難しい特殊形状に適している。
- **多角形ポリゴン**: パラメータで変形する必要がある図形や、穴のある複合図形に適している。コードを書かずにビジュアルエディタで定義できる。

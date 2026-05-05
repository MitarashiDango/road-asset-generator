# RoadTextureBaker アーキテクチャ

## 概要

`RoadTextureBaker.Bake(RoadConfig)` は、設定から 4 種類のテクスチャ
（Albedo / Normal / MetallicSmoothness / AO）を生成します。
内部処理は責務ごとに 4 層に分離されています。

## レイヤー構成

```
Layer 1: BakeContext + Resolvers (設定 → 中間表現)
   ↓
Layer 2: Iterators (走査ユーティリティ)
   ↓
Layer 3: Builders (各出力マップ)
   ↓
Layer 4: RoadTextureBaker (オーケストレーター)
```

## ディレクトリ構成

```
Editor/
├── RoadTextureBaker.cs            # 公開 API (Bake) + GeneratedTextures
├── Baking/                        # 内部インフラ
│   ├── BakeContext.cs             # 共有コンテキスト
│   ├── LineStroke.cs              # 1 本のストロークの中間表現
│   ├── StrokeResolver.cs          # 設定 → List<LineStroke>
│   ├── LaneRangeResolver.cs       # LaneRange + ピクセル範囲計算
│   ├── RumbleStripResolver.cs     # RumbleStripParams + 計算
│   ├── StrokePixelIterator.cs     # ストロークピクセルの走査
│   ├── RumblePixelIterator.cs     # rumble strip ピクセルの走査
│   └── TextureUtils.cs            # MakeLinear / ToColor32
└── Builders/                      # 各出力マップ
    ├── AlbedoBuilder.cs           # Albedo 構築 (内部スタンプ含む)
    ├── PaintHeightStamper.cs      # heightMap への塗装高さ加算
    ├── NormalBuilder.cs           # heightMap → normal
    ├── MetallicSmoothnessBuilder.cs
    └── AOBuilder.cs
```

## Bake() 処理フロー

```csharp
public static GeneratedTextures Bake(RoadConfig config)
{
    var ctx = BakeContext.Create(config);                 // 1. 共有コンテキスト作成

    var strokes = StrokeResolver.Resolve(in ctx);         // 2. ストローク解決
    var laneRanges = LaneRangeResolver.Resolve(in ctx);   // 3. レーン範囲計算

    var heightMap = RoadNoise.StyleNoise(...);            // 4. ベース高さマップ生成

    var albedoPixels = AlbedoBuilder.Build(...);          // 5. Albedo 構築
    PaintHeightStamper.Apply(heightMap, ...);             // 6. 高さマップに塗装高さ加算
    var normalPixels = NormalBuilder.Build(heightMap, ...); // 7. Normal 構築
    var msPixels = MetallicSmoothnessBuilder.Build(...);  // 8. MS 構築
    var aoPixels = AOBuilder.Build(...);                  // 9. AO 構築

    return new GeneratedTextures { ... };
}
```

### 重要な順序制約

`PaintHeightStamper` は `AlbedoBuilder` の**後**、`NormalBuilder` の**前**に呼ぶ必要があります。
これは:
- Albedo はベース高さマップを使ってアスファルトをシェーディングするため、塗装の凸を含めない
- Normal は塗装の凸を法線に反映させるため、塗装高さを加算した後の heightMap を使う

## 各レイヤーの役割

### Layer 1: BakeContext + Resolvers

**`BakeContext`** (readonly struct): すべての処理で共有される不変データ
- `RoadConfig config`
- `int W, H` (テクスチャ解像度)
- `float pxPerMx, pxPerMy` (メートル/ピクセル変換係数)
- `int seed`

**Resolvers**: `RoadConfig` から純粋な中間表現を計算
- `StrokeResolver`: `LineConfig` / `LaneConfig` → `List<LineStroke>`
- `LaneRangeResolver`: 各レーンのピクセル範囲 `LaneRange[]`
- `RumbleStripResolver`: `LaneConfig` → `RumbleStripParams`

### Layer 2: Iterators

**`StrokePixelIterator`**: `LineStroke` の有効ピクセルを走査
```csharp
StrokePixelIterator.ForEach(in stroke, W, H, (x, y, idx, du, xStart, xEnd) => {
    // 各有効ピクセルに対する処理
});
```

**`RumblePixelIterator`**: `RumbleStripParams` の有効ピクセルを走査
```csharp
RumblePixelIterator.ForEach(in sp, W, H, (x, y, idx, alpha) => {
    // 各有効ピクセルに対する処理
});
```

これらはステートレスなユーティリティで、Albedo / MS / PaintHeight など複数の Builder から再利用されます。

### Layer 3: Builders

各出力マップを構築します。`Color32[]` を返す純関数（ただし `PaintHeightStamper` は heightMap を変更）。

| クラス | 出力 | 主な依存 |
|---|---|---|
| `AlbedoBuilder` | `Color32[]` | strokes, laneRanges, heightMap |
| `PaintHeightStamper` | (heightMap を変更) | strokes, laneRanges |
| `NormalBuilder` | `Color32[]` | heightMap |
| `MetallicSmoothnessBuilder` | `Color32[]` | strokes, laneRanges |
| `AOBuilder` | `Color32[]` | seed のみ |

`AlbedoBuilder` は内部で `StampRumbleStrips` / `StampStrokes` / `StampRepairPatches` を呼び出します。
これらは Albedo にしか影響しないため、`AlbedoBuilder` のプライベートメソッドとして実装されています。

### Layer 4: RoadTextureBaker

公開 API のみを提供する薄いオーケストレーター。
各 Builder を順序通りに呼び出すだけで、ロジックは持ちません。

## 拡張ポイント

### 新しい出力マップを追加する

1. `Builders/` に新しい Builder クラスを追加
   ```csharp
   internal static class XxxBuilder
   {
       public static Color32[] Build(in BakeContext ctx, /* 必要な引数 */) { ... }
   }
   ```
2. `RoadConfig.OutputConfig` に `generateXxx` フラグを追加
3. `GeneratedTextures` に `Texture2D xxx` を追加
4. `RoadTextureBaker.Bake` に呼び出しを追加

### 新しい中間表現を追加する

`Baking/` に Resolver を追加し、`Bake()` で計算結果を必要な Builder に渡します。

### 新しいスタンプ処理を追加する

- Albedo に影響する場合: `AlbedoBuilder` のプライベートメソッドとして追加
- 高さマップに影響する場合: `PaintHeightStamper` を拡張、または新しい Stamper クラスを追加
- MS に影響する場合: `MetallicSmoothnessBuilder` を拡張

## 設計判断

### なぜ static クラスなのか

- 既存コードベースの一貫性
- 状態を持たないため、インスタンス化する必要がない
- C# の拡張性は `internal` 可視性で十分得られる

### なぜ `in BakeContext` で渡すのか

`BakeContext` は readonly struct のため、参照渡しでコピーを避ける。
パフォーマンス最適化と意図の明示。

### なぜ `PaintHeightStamper` だけ「Stamper」と呼ぶのか

他の Builder は `Color32[]` を返す純関数だが、`PaintHeightStamper` は
`float[] heightMap` を**変更**する点で異なる。命名で意図を区別している。

### なぜサブ名前空間を使わないのか

`Shapes/` の前例に倣い、すべて `MitarashiDango.RoadAssetGenerator` 名前空間に統一。
ファイルはフォルダで分類し、可視性は `internal` で公開 API を絞る。

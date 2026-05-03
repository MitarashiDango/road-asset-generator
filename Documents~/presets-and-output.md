# プリセットと出力

組み込みプリセット、自作プリセットの保存・読込、生成されるアセットの仕様について説明します。

## 目次

- [Quick Apply プリセット](#quick-apply-プリセット)
- [自作プリセットの保存・読込](#自作プリセットの保存読込)
- [Built-in Presets メニュー](#built-in-presets-メニュー)
- [生成されるアセット](#生成されるアセット)
- [マテリアルへのバインディング](#マテリアルへのバインディング)

---

## Quick Apply プリセット

ウィンドウ上部の **Quick Apply** から、6 種の代表的な道路構成をワンクリックで適用できます。**現在の設定は上書きされる**ため、必要に応じて事前にプリセットアセットとして保存しておいてください。

名前付き Preset Asset がロードされている状態で Quick Apply を押すと、上書き確認ダイアログが表示されます (一時アセット使用時はダイアログなし)。

### 1. 追越禁止 (`PresetMountainRoad_NoOvertaking`)

- 想定用途: 山道・追越禁止区間
- 構成: 片側 1 車線 × 2 (対面通行)、中央線 = 黄実線
- 路側帯: 0.75 m

### 2. 追越可 (`PresetMountainRoad_PassingOK`)

- 想定用途: 山道・追越可区間
- 構成: 片側 1 車線 × 2 (対面通行)、中央線 = 白破線
- 路側帯: 0.75 m

### 3. 4 車線 (`PresetFourLane`)

- 想定用途: 都市部の 4 車線道路
- 構成: 片側 2 車線 × 2 (対面通行)、中央 = 黄実線の二重線、車線間 = 白破線

### 4. 1.5 車線 (`PresetNarrowLane15`)

- 想定用途: 都道府県道の 1 車線弱 (全幅 4.5 m 程度)
- 構成: 1 車線 + 0.25 m 路側帯

### 5. 1 車線 (`PresetSingleLane`)

- 想定用途: 一方通行・農道・狭い住宅地路
- 構成: 1 車線 (3.0 m) + 0.5 m 路側帯、両側に白実線

### 6. 車線なし (`PresetNoLaneMarkings`)

- 想定用途: 生活道路・私道
- 構成: 1 車線 (4.0 m) + 0.25 m 路側帯、区画線なし

---

## 自作プリセットの保存・読込

### Save Current as New Preset

ウィンドウ上部の **Save Current as New Preset** ボタンで、現在の設定を `RoadConfigAsset` として保存できます。

操作手順:

1. ウィンドウ上部のパラメータを編集
2. **Save Current as New Preset** をクリック
3. 保存ダイアログで保存先 (プロジェクト内) とファイル名を指定

### Preset Asset の読込

ウィンドウ最上部の **Preset Asset** フィールドに `RoadConfigAsset` をドラッグ&ドロップ、またはピッカーから選択すると、その設定がウィンドウに反映されます。

### Reset to Default

**Reset to Default** ボタンで、現在編集中のアセットを「追越禁止」プリセットの値にリセットできます (確認ダイアログ表示後に実行)。

### 一時アセット表示

`Preset Asset` が未指定の状態で編集を始めると、ウィンドウ上部に「Using unsaved settings — click 'Save Current as New Preset' to keep them.」と表示されます。

---

## Built-in Presets メニュー

メニュー `Tools > Road Asset Generator > Create Built-in Presets`、またはウィンドウ上部の **Create Built-in Presets** ボタンから、上記 6 種の組み込みプリセットを `RoadConfigAsset` ファイルとして一括生成できます。

生成先:

```
Assets/RoadPresets/
├── Mountain_NoOvertaking.asset
├── Mountain_PassingOK.asset
├── FourLane_DoubleYellow.asset
├── Narrow_15Lane.asset
├── SingleLane.asset
└── NoLaneMarkings.asset
```

これらは **Preset Asset** フィールドにドラッグして即座に適用できます。

---

## 生成されるアセット

### ファイル一覧

`Generate Textures + Material` で以下のファイルが `Output Folder` に生成されます (ファイル名は `Name Prefix` に依存)。

| ファイル | 内容 | sRGB | 用途 |
|---|---|---|---|
| `<prefix>_albedo.png` | ベースカラー | Yes | Material の Base Color / Main Texture |
| `<prefix>_normal.png` | 法線マップ | No (NormalMap) | Material の Normal Map |
| `<prefix>_metallicSmoothness.png` | Metallic (R) + Smoothness (A) | No (Linear) | Material の Metallic Map |
| `<prefix>_ao.png` | Ambient Occlusion | No (Linear) | Material の Occlusion Map |
| `<prefix>_material.mat` | マテリアル | — | 上記マップを参照 |

### テクスチャインポート設定

各 PNG は以下のインポート設定で自動構成されます:

| 項目 | Albedo | Normal | MetallicSmoothness | AO |
|---|---|---|---|---|
| Texture Type | Default | Normal Map | Default | Default |
| sRGB | Yes | — | No (Linear) | No (Linear) |
| Wrap Mode | Repeat | Repeat | Repeat | Repeat |
| Filter Mode | Bilinear | Bilinear | Bilinear | Bilinear |
| Aniso Level | 8 | 8 | 8 | 8 |
| Mipmap | Enabled | Enabled | Enabled | Enabled |

---

## マテリアルへのバインディング

`Generate Textures + Material` を実行すると、現在のレンダーパイプラインに合わせた Material が自動生成されます。

### URP (Universal Render Pipeline / Lit)

| マップ | Material プロパティ |
|---|---|
| Albedo | `_BaseMap` (`_MainTex` も併設) |
| Normal | `_BumpMap` |
| MetallicSmoothness | `_MetallicGlossMap` |
| AO | `_OcclusionMap` |

### Built-in (Standard)

| マップ | Material プロパティ |
|---|---|
| Albedo | `_MainTex` |
| Normal | `_BumpMap` |
| MetallicSmoothness | `_MetallicGlossMap` |
| AO | `_OcclusionMap` |

### Pipeline 設定の振る舞い

`Output > Pipeline` の設定値による挙動:

| 設定値 | 振る舞い |
|---|---|
| AutoDetect | URP が検出されれば URP/Lit、それ以外は Built-in Standard |
| BuiltIn | 強制的に Standard シェーダを使用 |
| URP | 強制的に Universal Render Pipeline/Lit を使用 |

---

## 次に読むもの

- 各パラメータの詳細: [ウィンドウリファレンス](./window-reference.md)
- 応用機能とトラブルシューティング: [上級者向けトピック](./advanced.md)

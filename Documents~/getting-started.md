# はじめに

Road Asset Generator のインストール手順と、最初のテクスチャを生成するまでの操作を説明します。

## 目次

- [インストール](#インストール)
- [最初のテクスチャを生成する](#最初のテクスチャを生成する)

---

## インストール

### Package Manager から (GUI)

Unity Editor の Package Manager から GUI 操作のみでインストールできます。

1. Unity メニューから `Window > Package Manager` を開きます
2. 左上の `+` ボタンをクリックし、`Add package from git URL...` を選択します
3. 入力欄に以下の URL を貼り付けて `Add` をクリックします

   ```
   https://github.com/MitarashiDango/road-asset-generator.git
   ```

   特定バージョンを固定したい場合は末尾に `#<タグ名>` を付与してください (例: `...#v1.0.0`)。

### manifest.json を直接編集

`Packages/manifest.json` の `dependencies` セクションに以下を追加します:

```json
"com.matcha-soft.road-asset-generator": "https://github.com/MitarashiDango/road-asset-generator.git"
```

### 動作要件

- Unity 2022.3 LTS 以降
- レンダーパイプライン: Built-in / URP

---

## 最初のテクスチャを生成する

### Step 1. ウィンドウを開く

Unity メニューから `Tools > Road Asset Generator > Open Window` を選択します。

### Step 2. プリセットを選択する

ウィンドウ上部の **Quick Apply** から、目的に近い構成をクリックします。

### Step 3. 出力先を確認する

**Output** セクションで `Output Folder` と `Name Prefix` を確認します。

- デフォルト: `Assets/RoadTextures` フォルダに `road_*.png` が出力されます
- `...` ボタンで OS のフォルダ選択ダイアログを開けます (プロジェクト内のみ選択可)

### Step 4. 生成する

ウィンドウ下部の **Generate Textures + Material** をクリックします。同名ファイルが存在する場合は上書き確認ダイアログが表示されます。

生成後、`Output Folder` 配下に以下のアセットが作成されます:

```
Assets/RoadTextures/
├── road_albedo.png
├── road_normal.png
├── road_metallicSmoothness.png
├── road_ao.png
└── road_material.mat
```

### Step 5. プレビュー

シーンに Plane を配置し、生成された `road_material.mat` をドラッグ&ドロップで適用します。

---

## 次に読むもの

- 各パラメータの詳細: [ウィンドウリファレンス](./window-reference.md)
- プリセットや生成ファイルの仕様: [プリセットと出力](./presets-and-output.md)

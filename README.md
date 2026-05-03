# Road Asset Generator

## これはなに

道路用テクスチャおよびマテリアルを生成するツール

## 主な機能

- 車線・路側帯・境界線・路面・摩耗を調整してタイリング可能な道路テクスチャを生成
- URP / Built-in 双方のマテリアル自動構成に対応
- 日本仕様の代表的な構成を含む 6 種のクイックプリセット
- 任意の `RoadConfigAsset` を作成することで、自作プリセットとして保存・再利用可能

## インストール

### Package Manager から (GUI)

Unity Editor の Package Manager から GUI 操作のみでインストールできます。

1. Unity メニューから `Window > Package Manager` を開く
2. 左上の `+` ボタンをクリックし、`Add package from git URL...` を選択
3. 入力欄に以下の URL を貼り付けて `Add` をクリック

   ```
   https://github.com/MitarashiDango/road-asset-generator.git
   ```

   特定バージョンを固定したい場合は末尾に `#<タグ名>` を付与してください (例: `...#v1.0.0`)。

### manifest.json を直接編集

`Packages/manifest.json` の `dependencies` に以下を追加します:

```json
"com.matcha-soft.road-asset-generator": "https://github.com/MitarashiDango/road-asset-generator.git"
```

## クイックスタート

1. メニュー `Tools > Road Asset Generator > Open Window` でウィンドウを開く
2. **Quick Apply** から目的に近いプリセット(例: `追越禁止`)をクリック
3. **Output** セクションで `Output Folder` と `Name Prefix` を確認
4. `Generate Textures + Material` をクリック → 4 種の PNG とマテリアルが生成される

詳細な手順は [Documents~/getting-started.md](./Documents~/getting-started.md) を参照してください。

## ドキュメント

- [はじめに / クイックスタート](./Documents~/getting-started.md)
- [ウィンドウリファレンス](./Documents~/window-reference.md)(全パラメータ解説)
- [プリセットと出力](./Documents~/presets-and-output.md)
- [上級者向けトピック](./Documents~/advanced.md)

目次は [Documents~/index.md](./Documents~/index.md) を参照してください。

## 動作要件

- Unity 2022.3 LTS 以降
- レンダーパイプライン: Built-in / URP

## ライセンス

MIT License

詳細は [LICENSE](./LICENSE) を参照してください。

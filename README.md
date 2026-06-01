# Road Asset Generator

## 概要

道路用テクスチャとマテリアルを生成する Unity Editor 拡張

## 主な機能

- 車線・路側帯・境界線・路面・摩耗・路面標示端のなじませを調整し、タイリング可能な道路テクスチャを生成
- URP / Built-in のマテリアル自動構成に対応
- 日本仕様の代表的な構成を含む 6 種のクイックプリセット
- 任意の `RoadConfigAsset` を作成することで、自作プリセットとして保存・再利用可能

## インストール

### Package Manager から (GUI)

Unity Editor の Package Manager から、GUI 操作のみでインストール可能です。

1. Unity メニューから `Window > Package Manager` を開く
2. 左上の `+` ボタンを押下し、`Add package from git URL...` を選択
3. 入力欄に以下の URL を貼り付けて `Add` を選択

   ```
   https://github.com/MitarashiDango/road-asset-generator.git
   ```

   特定バージョンを固定する場合は、末尾に `#<タグ名>` を付与してください (例: `...#v1.2.0`)。

### manifest.json を直接編集

`Packages/manifest.json` の `dependencies` に以下を追加します:

```json
"com.matcha-soft.road-asset-generator": "https://github.com/MitarashiDango/road-asset-generator.git"
```

### VRChat Creator Companion (VCC) 経由

本パッケージは VRChat 専用ではないため上記の Package Manager 経由でのインストールが基本ですが、
VRChat Creator Companion (VCC) を既に利用している環境向けに VPM リポジトリからのインストールにも対応しています。

1. VCC の設定画面でパッケージリポジトリを追加
   - リポジトリ URL: `https://vpm.matcha-soft.com/repos.json`
2. プロジェクトの Manage Project 画面で `Road Asset Generator` を Add

## クイックスタート

1. メニュー `Tools > Road Asset Generator > Open Window` でウィンドウを開く
2. **Quick Apply** から目的に近いプリセット(例: `追越禁止`)を選択
3. **Output** セクションで `Output Folder` と `Name Prefix` を確認
4. `Generate Textures + Material` を実行 → 各種 PNG とマテリアルが生成される

詳細な手順は [Documents~/getting-started.md](./Documents~/getting-started.md) を参照してください。

## ドキュメント

- [はじめに / クイックスタート](./Documents~/getting-started.md)
- [ウィンドウリファレンス](./Documents~/window-reference.md)(全パラメータ解説)
- [プリセットと出力](./Documents~/presets-and-output.md)
- [上級者向けトピック](./Documents~/advanced.md)
- [開発者向け: ベイカー構成](./Documents~/dev-baker-architecture.md)
- [開発者向け: 路面標示の追加](./Documents~/dev-adding-markings.md)
- [開発者向け: 図形プリミティブの追加](./Documents~/dev-adding-shapes.md)

目次は [Documents~/index.md](./Documents~/index.md) を参照してください。

## 動作要件

- Unity 2022.3 LTS 以降
- レンダーパイプライン: Built-in / URP

## ライセンス

MIT License

詳細は [LICENSE](./LICENSE) を参照してください。

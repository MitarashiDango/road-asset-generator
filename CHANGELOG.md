# Changelog

## [Unreleased]

### Added

- RoadNetwork / RoadSegment による道路ネットワーク生成を追加
- Catmull-Rom スプラインから路面メッシュと区画線メッシュを生成する Editor 機能を追加
- RoadSurfaceStyle / RoadSurfaceStyleAsset による Segment 単位の路面スタイルを追加
- SceneView の制御点編集、Shift + クリックでの制御点追加、プレビュー再生成に対応
- 生成 GameObject の Surface / Marking Layer override、Built-in / URP 向け区画線シェーダ、ローカル確認用サンプルを追加
- 路面 MeshCollider の既定生成と Network / Segment 単位の有効化切り替えを追加
- カーブ路面のポリゴン形状露出を抑えるため、路面メッシュの幅方向分割設定を追加

## [1.2.0] - 2026-06-01

### Added

- Weathering に Line Edge Fade を追加し、路面標示の U/V 端のなじませ量を調整できるようにした

### Changed

- 路面標示の端処理を見直し、左右端だけでなく V 軸方向の端にも Line Edge Fade を適用
- 既定の端処理をシャープ寄りに調整

## [1.1.0] - 2026-05-07

### Added

- 減速マーク (山形マーク / V字シェブロン) を追加
- 車線の並び替え機能 (▲▼ ボタン) を追加
- タイヤ跡摩耗のレーン別調整 (Wear Boost) を追加
- タイヤ跡上の路面標示への摩耗反映を追加
- VRChat Creator Companion 経由のインストール方法をドキュメントに追加

### Changed

- 路面標示・区画線の既定値を政令準拠かつタイル整合に調整
- Rumble Strips の日本語表記を実態に即して「減速帯」に変更
- UI tooltip の日本語表記を統一
- パッケージを Editor 専用に統合 (Runtime/ アセンブリを廃止)
- 内部アーキテクチャを再構成 (Shape システム 3 層化 + Baker 4 層化)

### Fixed

- 境界線太さ変更時にタイヤ跡が車線位置に正しく追従しない問題を修正

## [1.0.0] - 2026-05-01

### Added

- 初回リリース

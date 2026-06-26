# 道路ネットワーク生成

道路ネットワーク生成は、Catmull-Rom スプラインで定義した道路区間から、路面メッシュと区画線メッシュを作る Unity Editor 向けの機能です。既存のテクスチャ生成とは独立しているため、生成済みのマテリアルを RoadSegment の Surface Style に割り当てて使えます。

## 作成と編集

1. `GameObject > Road Asset Generator > Road Network` または `Tools > Road Asset Generator > Create Road Network` を実行します。
2. 作成された `RoadSegment` を選択し、SceneView のハンドルで制御点を移動します。
3. Shift + クリックで制御点を末尾に追加します。コライダーにヒットしない場合は、最後の制御点を通る水平面へ投影されます。
4. Inspector の `Profile Template` で `RoadProfileTemplateAsset` を選び、`Apply To First Profile Key` を実行します。
5. `Road Generation` の `Regenerate Road` または RoadNetwork 側の `Regenerate All Roads` で路面と区画線を再生成します。

RoadSegment のプロファイルはテンプレートからコピーされます。テンプレートアセットを後から編集しても、適用済みの道路区間には自動反映されません。

## 生成内容

- `Surfaces` 配下に路面メッシュを生成します。
- `Markings` 配下に区画線メッシュを生成します。
- 区画線は `RoadProfile.boundaryLines` の `RoadLineStroke` を元に、色、線種、幅、破線長、破線間隔を反映します。
- 二重線は 2 本のストロークと `strokeSpacingMeters` から生成します。`strokeSpacingMeters` は、2 本の線の内側エッジ間の隙間として扱います。
- 区画線マテリアルは、Network の `markingMaterial` が指定されていれば複製して使い、ストロークの色を適用します。未指定の場合は、現在の Built-in / URP に合わせて、深度バイアス付きのパッケージ標準シェーダを優先します。見つからない場合だけ、各パイプラインの標準シェーダへフォールバックします。
- 区画線マテリアルの Render Queue は Geometry より後ろに設定されます。パッケージ標準シェーダでは、レンダーパイプライン別の unlit shader で depth bias も使い、路面と重なる区画線が遠距離で路面側に隠れにくくしています。VR の Single Pass Instanced / Multiview に対応しやすいよう、stereo instancing 用の定型 macro も入れています。区画線の頂点は、RoadNetwork Inspector の `Marking Surface Offset Meters` で指定した距離だけ、路面から道路フレームの法線方向へ浮かせます。
- 区画線の生成オブジェクトには Collider を追加しません。MeshRenderer は影を落とさず、影も受けない設定で生成します。

## API と拡張ポイント

- `RoadNetwork`: ネットワーク全体の既定生成設定、fallback material、新規 Segment 用テンプレートを持ちます。
- `RoadSegment`: 制御点、プロファイルキー、Surface Style、生成済みオブジェクト参照を保持します。
- `RoadProfileTemplateAsset`: RoadProfile のテンプレートです。適用時に Segment へコピーされます。
- `RoadSurfaceStyleAsset`: Surface Style のテンプレートです。適用時に Segment へコピーされます。
- `RoadSurfaceMeshBuilder`: RoadSegment から路面メッシュデータを作成します。
- `RoadMarkingMeshBuilder`: RoadProfile の境界線定義から区画線メッシュデータを作成します。
- `RoadNetworkValidator`: プロファイルキー、プロファイル構造、曲率半径などを検証します。

配置標示、交差点、車線遷移は MVP の生成対象外です。追加する場合は RoadSegment のデータ構造に詰め込まず、別の配置データや RoadJunction 系の生成器として拡張する方針です。

## スプラインとフレーム

スプラインの評価には Runtime 側の `CatmullRomSpline` を使います。方式は centripetal Catmull-Rom です。フレームは World Y を基準に横方向を計算します。接線が鉛直に近く、World Y から横方向を作れない場合は、World Z、World X、最後に接線と最も揃っていない軸へ順にフォールバックします。

制御点は RoadSegment ローカル座標です。高低差を持つ制御点も利用できますが、バンク、Collider、Lightmap UV2 は MVP では生成しません。

## 検証と制限

RoadNetwork Inspector の `Validate Road Network` で検証を実行できます。複数プロファイルキー、キー位置の範囲超過、境界線数の不一致、ストローク数の不一致、曲率半径が道路半幅を下回る箇所を警告します。曲率警告は RoadSegment 選択中の SceneView に赤い円で表示されます。

既知の制限:

- 生成処理は先頭の RoadProfileKey のみ使用します。
- ジャンクション、合流、分岐、交差点メッシュは未生成です。
- 配置標示、摩耗表現、線ごとの material override は未実装です。
- 動作確認用シーンはパッケージの配布リソースには含めず、プロジェクト側の `Assets/RoadAssetGeneratorLocalSamples/RoadNetworkMvp/` でローカル確認用として管理します。

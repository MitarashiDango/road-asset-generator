# 道路ネットワーク生成

道路ネットワーク生成は、Catmull-Rom スプラインで定義した道路区間から、路面メッシュと区画線メッシュを作る Unity Editor 向けの機能です。既存のテクスチャ生成とは独立しているため、生成済みのマテリアルを RoadSegment の Surface Style に割り当てて使えます。

## 作成と編集

1. `GameObject > Road Asset Generator > Road Network` または `Tools > Road Asset Generator > Create Road Network` を実行します。
2. 作成された `RoadSegment` を選択し、SceneView のハンドルで制御点を移動します。
3. Shift + クリックで制御点を末尾に追加します。コライダーにヒットしない場合は、最後の制御点を通る水平面へ投影されます。
4. Inspector の `Profile Template` で `RoadProfileTemplateAsset` を選び、`Apply To First Profile Key` を実行します。
5. `Road Generation` の `Regenerate Road` または RoadNetwork 側の `Regenerate All Roads` で路面と区画線を再生成します。

RoadSegment のプロファイルと Surface Style はテンプレートからコピーされます。テンプレートアセットを後から編集しても、適用済みの道路区間には自動反映されません。Network の Material / Texture Length は、Segment 側の Surface Style が未設定の古いシーン向け fallback として扱います。

## 生成内容

- `Surfaces` 配下に路面メッシュを生成します。
- `Markings` 配下に区画線メッシュを生成します。
- 区画線は `RoadProfile.boundaryLines` の `RoadLineStroke` を元に、色、線種、幅、破線長、破線間隔を反映します。
- 二重線は 2 本のストロークと `strokeSpacingMeters` から生成します。`strokeSpacingMeters` は、2 本の線の内側エッジ間の隙間として扱います。
- 区画線マテリアルは、Network の `markingMaterial` が指定されていれば複製して使い、ストロークの色を適用します。未指定の場合は、現在の Built-in / URP に合わせたパッケージ標準の Lit シェーダを優先して使い、シーンのライト、影、GI、Light Probe、Reflection Probe の影響を受けます。URP package がないプロジェクトでも import エラーにならないよう、URP 標準シェーダには互換 fallback も含めています。
- 区画線は RoadNetwork Inspector の `Marking Surface Offset Meters` だけ路面から浮かせ、標準シェーダでは深度バイアスも使います。路面に埋まって見える場合は、この offset を少し大きくしてください。
- 路面メッシュは進行方向の `Max Surface Sample Length Meters` / `Max Surface Sample Angle Degrees` と、幅方向の `Max Surface Column Width Meters` で分割密度を調整できます。`Max Surface Column Width Meters` を小さくすると、カーブ路面で大きなポリゴン形状が模様として目立つ問題を減らしやすくなりますが、頂点数、MeshCollider、再生成コストは増えます。RoadSegment Inspector の `Override Surface Sampling` を使うと、必要な Segment だけ分割密度を上げられます。
- 生成 GameObject の Unity Layer は、RoadNetwork Inspector の `Default Surface Layer` / `Default Marking Layer` を既定値として使います。
  RoadSegment Inspector の `Override Surface Layer` / `Override Marking Layer` を使うと、路面と区画線を個別に Segment 値へ切り替えられます。
  Layer 変更は既存の `Surfaces` / `Markings` 配下にも反映され、再生成後も同じ実効値が使われます。既存の `Markings` 参照を修復した場合も、生成済み区画線 Renderer は現在の影・Probe 設定へ補修されます。
- 路面チャンクには既定で `MeshCollider` を生成します。`Surfaces` root には Collider を追加せず、各 `Surface_000` などの `MeshCollider.sharedMesh` は同じ GameObject の表示用 Mesh と同じ参照になります。
  RoadNetwork Inspector の `Generate Surface Colliders` を切り替えるとネットワーク全体の既定を変更できます。RoadSegment Inspector の `Override Surface Collider Settings` を使うと、特定 Segment だけ `Generate Surface Colliders` を上書きできます。
- 区画線の生成オブジェクトには Collider を追加しません。MeshRenderer は影を落とさず、影を受ける設定で生成します。カスタム `markingMaterial` を指定した場合も Renderer は同じ設定になりますが、ライト、影、GI、Probe への反応はユーザー指定シェーダの実装に依存します。

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

スプラインの評価には Runtime 側の `CatmullRomSpline` を使います。方式は centripetal Catmull-Rom です。フレームは World Y を基準に横方向を計算します。接線が鉛直に近く、World Y から横方向を作れない場合は、World Z、World X、最後に接線と最も揃っていない軸へ順にフォールバックします。路面 UV は U が道路全幅の 0〜1、V が進行方向距離 / `textureLengthMeters` です。幅方向に列分割してもこの UV の意味は変わりません。

制御点は RoadSegment ローカル座標です。高低差を持つ制御点も利用できますが、バンクは MVP では生成しません。路面 Collider は表示用の路面 Mesh と同じ形状で生成されます。区画線メッシュには Unity の secondary UV unwrap で静的ライトマップ用の UV2 を生成しますが、路面メッシュの Lightmap UV2 は未生成です。

## ライティングとベイク

区画線を静的ライトマップに含める場合は、区画線を生成した後に必要な Static / GI 設定をシーン側で行い、Lighting を bake してください。区画線を再生成すると生成メッシュと Renderer が差し替わるため、前回の bake 結果はその生成物には引き継がれません。再生成後は再度 bake してください。

区画線が明るく浮いて見える場合は、Network の `markingMaterial` に unlit など独自シェーダを指定していないか、使用中の Render Pipeline と Probe / Lightmap 設定が合っているかを確認してください。影や GI の結果が不自然な場合は、Static / GI 設定、UV2、最後に再生成した後で bake し直しているかを確認してください。

Console に package 標準の区画線シェーダが見つからない warning が出た場合は、Fallback シェーダで生成されています。この場合、深度バイアスが弱くなる、または失われる可能性があります。パッケージの shader import 状態、使用中の Render Pipeline、必要に応じて同等の Offset を持つカスタム `markingMaterial` を確認してください。

## 検証と制限

RoadNetwork Inspector の `Validate Road Network` で検証を実行できます。複数プロファイルキー、キー位置の範囲超過、境界線数の不一致、ストローク数の不一致、曲率半径が道路半幅を下回る箇所を警告します。曲率警告は RoadSegment 選択中の SceneView に赤い円で表示されます。

既知の制限:

- 生成処理は先頭の RoadProfileKey のみ使用します。
- ジャンクション、合流、分岐、交差点メッシュは未生成です。
- 配置標示、摩耗表現、線ごとの material override は未実装です。
- 動作確認用シーンはパッケージの配布リソースには含めず、プロジェクト側の `Assets/RoadAssetGeneratorLocalSamples/RoadNetworkMvp/` でローカル確認用として管理します。

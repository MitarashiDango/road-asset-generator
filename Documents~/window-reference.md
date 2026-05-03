# ウィンドウリファレンス

Road Asset Generator のエディタウィンドウに表示される全パラメータを、UI の構成順に解説します。

## 目次

- [Output](#output)
- [Shoulders (路側帯)](#shoulders-路側帯)
- [Lanes](#lanes)
  - [Surface Tint (路面色)](#surface-tint-路面色)
  - [Rumble Strips (減速マーク)](#rumble-strips-減速マーク)
  - [Speed Reduction Dot Line (減速ドットライン)](#speed-reduction-dot-line-減速ドットライン)
- [Boundary Lines](#boundary-lines)
- [Asphalt (路面)](#asphalt-路面)
- [Weathering](#weathering)
- [フッター操作](#フッター操作)

各パラメータは「UI ラベル」「型・範囲・デフォルト」「概要」の順に記載します。

---

## Output

生成されるテクスチャ・マテリアルの出力設定です。

### Resolution

- enum (512 / 1024 / 2048 / 4096), default 1024
- 出力テクスチャの解像度 (正方形)。

### Tile Length (m)

- float, ≥1.0, default 10.0
- V 軸方向に 1 タイルがカバーするメートル数。

### Output Folder

- string, default `Assets/RoadTextures`
- 生成ファイルの出力先フォルダ。`Assets/` または `Packages/` 配下のみ選択可。右の `...` ボタンで OS のフォルダ選択ダイアログを開けます。

### Name Prefix

- string, default `road`
- 出力ファイル名のプレフィックス (例: `road_albedo.png`)。

### Seed

- int, default 42
- ノイズ生成の乱数シード。同じシードなら同じテクスチャが生成されます。

### Pipeline

- enum (AutoDetect / BuiltIn / URP), default AutoDetect
- マテリアル生成時のシェーダ選択。AutoDetect は現在のレンダーパイプライン設定から自動判別します。

### 生成トグル (Normal / MetalSmooth / AO / Material)

- bool × 4, default 全 true
- 各マップ・マテリアルの生成有無を切り替えます。

---

## Shoulders (路側帯)

道路両端の路側帯設定です。左右独立に設定できます。

### Width (m) (Left / Right)

- float, ≥0.0, default 0.75
- 路側帯の幅 (メートル)。

### Color Tint (Left / Right)

- float, -0.3 〜 +0.3, default +0.04
- 路側帯の明度オフセット。正の値で明るく、負の値で暗くなります。

---

## Lanes

走行レーンの設定です。`+ Add Lane` ボタンで車線を追加できます。境界線数は車線数 + 1 で自動管理されます。

各車線は以下の 4 ブロックに分かれます:

1. 基本設定 (Label / Width / Direction)
2. [Surface Tint (路面色)](#surface-tint-路面色)
3. [Rumble Strips (減速マーク)](#rumble-strips-減速マーク)
4. [Speed Reduction Dot Line (減速ドットライン)](#speed-reduction-dot-line-減速ドットライン)

### Label

- string
- 車線の識別ラベル (表示用)。

### Width (m)

- float, ≥0.5, default 3.0
- 車線幅 (メートル)。

### Direction

- enum (Forward / Backward), default Forward
- 車線の進行方向。Backward を選ぶと Speed Reduction Dot Line の slant が自動反転されます。

---

### Surface Tint (路面色)

車線の路面色オーバーレイ (カーブ警戒の赤路面など)。

#### Enable

- bool, default false

#### Tint Color

- Color, default 暗赤色
- 車線にブレンドされる色。

#### Tint Strength

- float (Slider), 0.0 〜 1.0, default 0.7
- ブレンド強度。1.0 で完全に Tint Color に置換、0.0 で無効。

---

### Rumble Strips (減速マーク)

車線端に並ぶ V 軸方向の帯状減速マーク。

#### Enable

- bool, default false

#### Stripe Thickness (m)

- float, ≥0.05, default 0.30
- 帯 1 本の太さ。

#### Stripe Spacing (m)

- float, ≥0.1, default 1.0
- 帯と帯の間隔 (gap の長さ)。

#### Stripe Color

- Color, default 白

#### Start Offset (m)

- float, default 0.5
- タイル先頭 (V=0) から最初の帯までのオフセット。

#### Edge Inset (m)

- float, ≥0.0, default 0.20
- 車線端から帯の内側終端までの距離。

#### Paint Height Factor

- float, ≥0.0, default 1.5
- 法線マップへの塗装高さ寄与の倍率。

#### Snap & center pattern

- ボタン操作。Stripe Spacing と Start Offset をタイル長に合わせて自動補正します。

---

### Speed Reduction Dot Line (減速ドットライン)

車線端に沿って斜行した平行四辺形 (ドット) が並ぶ視覚的な減速標示。

#### Enable

- bool, default false

#### Dot Width (m)

- float, ≥0.05, default 0.30
- ドットの U 軸方向の幅。

#### Dot Height (m)

- float, ≥0.05, default 1.5
- ドットの V 軸方向の長さ。

#### Dot Spacing (m)

- float, ≥0.1, default 1.5
- ドット間の間隔 (gap の長さ)。

#### Dot Color

- Color, default 白

#### Slant (m)

- float, default 0.3
- ドットの V 軸方向のシアー量。0 で長方形。

#### Edge Inset (m)

- float, ≥0.0, default 0.30
- 車線端からドットまでの距離。

#### Side

- enum (Left / Right / Both), default Right
- ドットを配置する車線端。

#### Start Offset (m)

- float, default 0.75
- タイル内の V 軸方向の位相オフセット。

#### Paint Height Factor

- float, ≥0.0, default 1.0
- 法線マップへの塗装高さ寄与の倍率。

#### Snap & center pattern

- ボタン操作。Dot Spacing と Start Offset をタイル長に合わせて自動補正します。

---

## Boundary Lines

車線の左右に配置される境界線。境界線数 = 車線数 + 1 を満たすよう自動管理されます。

### 各境界線の操作

| 操作 | 説明 |
|---|---|
| Foldout を展開 | 各 stroke の詳細を表示 |
| ▲ / ▼ ボタン | 境界線そのものの並べ替え (車線間の順序変更)。境界線数は車線数 + 1 で自動管理されるため削除ボタンはなし |
| `+ Add Stroke` | この境界線にストロークを追加 (中央二重線等) |
| ストロークの並べ替え・削除 | 各ストローク行の ▲ / ▼ / ✕ ボタンで操作 |

### Stroke 間の間隔 (spacingsMeters)

複数ストロークを持つ境界線では、隣接ストローク間の中心-中心間隔を別途指定します。

### LineStyle (各ストロークの設定)

#### Type

- enum (None / Solid / Dashed / Diamond), default Solid
  - **None**: 線を描画しない
  - **Solid**: 連続線
  - **Dashed**: 破線
  - **Diamond**: V 軸方向に繰り返される平行四辺形

#### Color

- Color, default 白

#### Width (m)

- float, ≥0.0, default 0.15
- 線の太さ (U 軸方向)。

#### Dash Length (m) / Dash Gap (m) (Type=Dashed のみ)

- **Dash Length**: 1 つの dash の長さ (default 5.0)
- **Dash Gap**: dash 間の gap (default 5.0)

#### Diamond Size (m) / Diamond Spacing (m) / Diamond Slant (m) (Type=Diamond のみ)

- **Diamond Size**: ダイヤの V 軸方向サイズ (default 0.8)
- **Diamond Spacing**: ダイヤ間の間隔 (default 1.5)
- **Diamond Slant**: V 軸方向のシアー量 (default 0.3)

#### Dash / Phase Offset (m) (Type=Dashed / Diamond)

- float, default 2.5
- タイル先頭からの位相オフセット。Dashed と Diamond で共通のフィールドです。

#### Paint Height Factor

- float, ≥0.0, default 1.0
- 法線マップへの塗装高さ寄与の倍率。

---

## Asphalt (路面)

路面の基本色とノイズ設定です。

### Base Color

- Color, default 暗灰色
- アスファルトのベースカラー。

### Noise Style

- enum (Smooth / Standard / Aggregate / Coarse / Worn / Concrete), default Standard
- ノイズプロファイルのプリセット。

### Noise Intensity

- float (Slider), 0.0 〜 2.0, default 1.0
- ノイズの全体強度。

### Bright Specks

- float (Slider), 0.0 〜 0.05, default 0.015
- 明るい斑点の発生量。

### Dark Specks

- float (Slider), 0.0 〜 0.05, default 0.008
- 暗い斑点の発生量。

---

## Weathering

経年劣化・摩耗の表現です。

### Line Edge Wear

- float (Slider), 0.0 〜 1.0, default 0.15
- 線の縁の摩耗。1.0 で完全摩耗。

### Line Fade

- float (Slider), 0.0 〜 1.0, default 0.08
- 線の色のフェード強度。

### Tire Track Wear

- float (Slider), 0.0 〜 1.0, default 0.0
- タイヤ跡による車線中央付近の暗化。

### Paint Height Strength

- float (Slider), 0.0 〜 3.0, default 1.0
- 法線マップへの塗装高さ寄与の全体倍率。0 で平坦、1 で標準。

### Repair Patches

- bool, default false
- 補修パッチの有無。

### Patch Count

- int (SliderInt), 1 〜 8, default 2
- パッチの数 (Repair Patches 有効時のみ)。

### Wet Surface

- bool, default false
- 濡れた路面の表現。

---

## フッター操作

### Refresh Preview

- ウィンドウ上部の albedo プレビュー画像を再生成します。

### Generate Textures + Material

- 4 種のマップとマテリアルを `Output Folder` に生成します。同名ファイルが存在する場合は上書き確認ダイアログが表示されます。

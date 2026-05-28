# ウィンドウリファレンス

Road Asset Generator のエディタウィンドウに表示される全パラメータを、UI の構成順に解説します。

## 目次

- [Output](#output)
- [Shoulders (路側帯)](#shoulders-路側帯)
- [Lanes](#lanes)
  - [Tire Track Wear (タイヤ跡摩耗)](#tire-track-wear-タイヤ跡摩耗)
  - [Surface Tint (路面色)](#surface-tint-路面色)
  - [Rumble Strips (減速帯)](#rumble-strips-減速帯)
  - [Speed Reduction Dot Line (減速ドットライン)](#speed-reduction-dot-line-減速ドットライン)
  - [Deceleration Marks (減速マーク / 山形マーク)](#deceleration-marks-減速マーク--山形マーク)
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
3. [Rumble Strips (減速帯)](#rumble-strips-減速帯)
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

### Tire Track Wear (タイヤ跡摩耗)

レーンごとのタイヤ跡摩耗の追加調整。Weathering > Tire Track Wear (全体設定) に加算される。

#### Wear Boost

- float, 0.0 〜 1.0, default 0.0
- 全体タイヤ跡摩耗強度に加算されるレーン固有の値。実効強度は `clamp(全体値 + Wear Boost, 0, 1)`。
- 例: 全体 0.2 + レーン 0.3 → 実効 0.5。0.0 のままなら全体値のみ適用。

#### Override Appearance

- bool, default false
- ON の場合、このレーンだけタイヤ跡の見た目設定を Weathering の既定値から上書きする。

#### Track Width (m)

- float, min 0.05, default 1.08
- タイヤ跡 1 本の見た目上のおおよその幅。内部では `width / 6` をガウシアン sigma として扱う。

#### Track Spacing (m)

- float, min 0.0, default 1.70
- 左右タイヤ跡の中心間距離。

#### Track Color

- Color, default dark gray
- タイヤ跡のブレンド先色。

#### Track Opacity

- float (Slider), 0.0 〜 1.0, default 0.30
- タイヤ跡色へのブレンド強度。実際のブレンド量はタイヤ跡摩耗強度と掛け合わせられる。

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

### Rumble Strips (減速帯)

車線端に並ぶ V 軸方向の帯状凹凸舗装。走行時の振動で車線逸脱を警告する。

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

### Deceleration Marks (減速マーク / 山形マーク)

急カーブ、急坂、連続カーブ、追突事故多発区間などの減速を要する区間およびその手前に設置される塗装路面標示。
レーン中央に進行方向を指す V 字 (シェブロン) を V 軸方向に周期的に配置する。
レーンの方向 (Forward/Backward) に応じて V 字の頂点の向きが自動反転する。

#### Enable

- bool, default false

#### Mark Color

- Color, default 白

#### Mark Width (m)

- float, ≥0.3, default 2.5
- V 字の開口幅 (U 軸方向の最大幅)。レーン幅 - 2 × Edge Inset を超える場合は自動でクリップされる。

#### Mark Height (m)

- float, ≥0.2, default 1.0
- V 字 1 つ分の V 軸方向の高さ (深さ)。

#### Mark Spacing (m)

- float, ≥0.5, default 5.0
- 隣接する V 字マーク間の V 軸方向ギャップ。

#### Line Thickness (m)

- float, ≥0.05, default 0.2
- V 字を構成する線の太さ (足元の垂直エッジの V 軸方向長さ)。Mark Height に対する比率として正規化される。

#### Edge Inset (m)

- float, ≥0.0, default 0.3
- レーン端から V 字の最も外側 (開口部) までの距離。区画線とマークが重ならないようにするためのマージン。

#### Start Offset (m)

- float, default 0.0
- タイル内の V 軸方向の位相オフセット。

#### Paint Height Factor

- float, ≥0.0, default 1.0
- 法線マップへの塗装高さ寄与の倍率。

#### Snap & center pattern

- ボタン操作。Mark Spacing と Start Offset をタイル長に合わせて自動補正します。

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
- タイヤ跡による車線中央付近の劣化強度。左右タイヤ跡の幅・間隔・色・濃さは下記の Default Tire Track Appearance で指定する。
- レーンごとの追加調整は `Lanes > Tire Track Wear > Wear Boost` で行う (両者の値が加算される)。

### Default Tire Track Appearance

- `Default Tire Track Width (m)`: float, min 0.05, default 1.08。タイヤ跡 1 本の見た目上のおおよその幅。
- `Default Tire Track Spacing (m)`: float, min 0.0, default 1.70。左右タイヤ跡の中心間距離。
- `Default Tire Track Color`: Color, default dark gray。タイヤ跡のブレンド先色。
- `Default Tire Track Opacity`: float (Slider), 0.0 〜 1.0, default 0.30。タイヤ跡色へのブレンド強度。
- レーン側の `Override Appearance` が OFF の場合、これらの既定値が使用される。

### Marking Wear from Tire Tracks

- float (Slider), 0.0 〜 1.0, default 0.5
- タイヤ跡の上にある路面標示 (境界線・減速マーク・ドットライン等) を下地色 (アスファルト) に寄せて摩耗表現する強度。
- 0.0 = 標示はタイヤ跡の影響を受けない (鮮明な状態)、1.0 = タイヤ跡上で標示が完全にフェード。

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

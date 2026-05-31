namespace MitarashiDango.RoadAssetGenerator
{
    /// <summary>
    /// 路面標示の形状をピクセル判定ロジックとして抽象化するインターフェース。
    /// Baker は形状の具象クラスに依存せず、このインターフェースを通じて描画判定を行う。
    /// 合成シェイプ (<see cref="UnionShape"/> 等) を介して複数の形状を組み合わせることができる。
    /// </summary>
    public interface IMarkingShape
    {
        /// <summary>
        /// X-shear 等でストローク幅を超えるピクセル走査が必要な場合の追加マージン (px)。
        /// </summary>
        int GetSlantPad(int halfWidthPx);

        /// <summary>
        /// 行 y にこの形状のピクセルが存在しないことが確実な場合 true を返す。
        /// パフォーマンス最適化用のヒントであり、false を返しても
        /// <see cref="TestPixel"/> が最終判定を行う。
        /// </summary>
        bool CanSkipRow(int y);

        /// <summary>
        /// ピクセル (x, y) がこの形状の内部かどうかを判定する。
        /// 合成シェイプ内で正しく動作するよう、行条件を含む完全な判定を行う。
        /// </summary>
        /// <param name="x">テクスチャ上の X 座標。</param>
        /// <param name="y">テクスチャ上の Y 座標。</param>
        /// <param name="xCenter">ストロークの U 軸中心 (px)。</param>
        /// <param name="halfWidthPx">ストロークの半幅 (px)。</param>
        /// <param name="du">中心からの U 軸方向符号付き距離。縁処理に利用される。</param>
        /// <param name="isVEdge">V 軸方向の端ピクセルである場合 true。</param>
        /// <returns>ピクセルが形状内にある場合 true。</returns>
        bool TestPixel(int x, int y, int xCenter, int halfWidthPx, out float du, out bool isVEdge);

        /// <summary>
        /// 縁ソフトニングに |du| を使うか (true: 斜辺系) 、固定 X 境界を使うか (false: 直線系)。
        /// </summary>
        bool HasDiagonalEdges { get; }
    }
}

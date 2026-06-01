namespace MitarashiDango.RoadAssetGenerator
{
    /// <summary>
    /// 正規化座標空間 u[-1,+1], v[0,1] で定義される純粋な幾何形状。
    /// 用途(路面標示等)には関知せず、図形の内外判定のみを担う。
    /// </summary>
    public interface IShapePrimitive
    {
        /// <summary>
        /// 正規化座標 (u, v) がこの形状の内部にあるかを判定する。
        /// </summary>
        /// <param name="u">U 軸座標。中心が 0、標準幅端が +/-1。</param>
        /// <param name="v">V 軸座標。0 がマーク先頭、1 がマーク末尾。</param>
        /// <param name="duNorm">中心からの正規化 U 距離。摩耗マスクのサンプリングと U 軸端判定に利用される。</param>
        bool Contains(float u, float v, out float duNorm);

        /// <summary>
        /// 形状が u 軸方向に占める最大範囲。通常 1.0。
        /// 平行四辺形等で u 座標が +/-1 を超える場合、拡張分を含めた値を返す。
        /// </summary>
        float MaxUExtent { get; }

        /// <summary>斜辺を持つ形状かどうか。U 軸端判定の方式選択に使用。</summary>
        bool HasDiagonalEdges { get; }
    }
}

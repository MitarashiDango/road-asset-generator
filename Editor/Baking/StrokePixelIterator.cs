namespace MitarashiDango.RoadAssetGenerator
{
    /// <summary>
    /// <see cref="StrokePixelIterator.ForEach"/> のコールバック。
    /// </summary>
    /// <param name="x">テクスチャ X 座標。</param>
    /// <param name="y">テクスチャ Y 座標。</param>
    /// <param name="idx">フラットバッファのインデックス (= y * W + x)。</param>
    /// <param name="duFromCenter">U 軸中心からの符号付き距離 (px)。縁ぼかし処理に利用される。</param>
    /// <param name="xStart">走査範囲の左端。境界判定に利用される。</param>
    /// <param name="xEnd">走査範囲の右端 (exclusive)。境界判定に利用される。</param>
    internal delegate void StrokePixelAction(int x, int y, int idx, float duFromCenter, int xStart, int xEnd);

    /// <summary><see cref="LineStroke"/> の有効ピクセルを走査するイテレータ。</summary>
    internal static class StrokePixelIterator
    {
        public static void ForEach(in LineStroke s, int W, int H, StrokePixelAction action)
        {
            var slantPad = s.shape.GetSlantPad(s.halfWidthPx);
            var xStart = s.xCenter - s.halfWidthPx - slantPad;
            var xEnd = s.xCenter + s.halfWidthPx + slantPad;
            if (xEnd <= xStart)
            {
                xEnd = xStart + 1;
            }

            for (var y = 0; y < H; y++)
            {
                if (s.shape.CanSkipRow(y))
                {
                    continue;
                }
                for (var x = xStart; x < xEnd; x++)
                {
                    if (x < 0 || x >= W)
                    {
                        continue;
                    }
                    if (!s.shape.TestPixel(x, y, s.xCenter, s.halfWidthPx, out var duFromCenter))
                    {
                        continue;
                    }
                    action(x, y, y * W + x, duFromCenter, xStart, xEnd);
                }
            }
        }
    }
}

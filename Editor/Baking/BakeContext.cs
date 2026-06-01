using System;

namespace MitarashiDango.RoadAssetGenerator
{
    /// <summary>
    /// ベイク処理全体で共有される不変コンテキスト。テクスチャ寸法、メートル/ピクセル変換係数、
    /// シードなど、複数の解決処理やマップ生成処理から参照される値をまとめる。
    /// </summary>
    internal readonly struct BakeContext
    {
        public readonly RoadConfig config;
        public readonly int W;
        public readonly int H;
        public readonly float pxPerMx;
        public readonly float pxPerMy;
        public readonly int seed;

        private BakeContext(RoadConfig config, int w, int h, float pxPerMx, float pxPerMy, int seed)
        {
            this.config = config;
            this.W = w;
            this.H = h;
            this.pxPerMx = pxPerMx;
            this.pxPerMy = pxPerMy;
            this.seed = seed;
        }

        public static BakeContext Create(RoadConfig config)
        {
            var totalWidth = config.TotalWidthMeters;
            if (totalWidth <= 0f)
            {
                throw new InvalidOperationException("Total road width must be > 0.");
            }

            var w = (int)config.output.resolution;
            var h = (int)config.output.resolution;
            var pxPerMx = w / totalWidth;
            var pxPerMy = h / config.output.textureLengthMeters;
            return new BakeContext(config, w, h, pxPerMx, pxPerMy, config.output.seed);
        }
    }
}

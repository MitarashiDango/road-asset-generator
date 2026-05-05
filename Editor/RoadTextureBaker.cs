using UnityEngine;

namespace MitarashiDango.RoadAssetGenerator
{
    /// <summary><see cref="RoadTextureBaker.Bake"/> が生成するテクスチャ群。</summary>
    public class GeneratedTextures
    {
        public Texture2D albedo;
        public Texture2D normal;
        // RGB = 0(非メタリック)、A = smoothness。
        public Texture2D metallicSmoothness;
        public Texture2D ao;
    }

    /// <summary>
    /// <see cref="RoadConfig"/> から道路面テクスチャ(albedo / normal / metallic-smoothness / AO)
    /// を手続き的に生成する。Editor に依存しない Runtime API として提供され、ディスクへの保存は
    /// Editor 側のレイヤが担う。
    ///
    /// 内部処理は責務ごとに分離されている:
    /// - <see cref="BakeContext"/>: 共有寸法・係数・シード
    /// - <see cref="StrokeResolver"/> / <see cref="LaneRangeResolver"/>: 設定→中間表現
    /// - <see cref="StrokePixelIterator"/> / <see cref="RumblePixelIterator"/>: ピクセル走査
    /// - <see cref="AlbedoBuilder"/> / <see cref="NormalBuilder"/> / <see cref="MetallicSmoothnessBuilder"/> / <see cref="AOBuilder"/>: 各マップ構築
    /// - <see cref="PaintHeightStamper"/>: 高さマップへの塗装高さ加算
    /// </summary>
    public static class RoadTextureBaker
    {
        /// <summary><paramref name="config"/> に従ってテクスチャ群をベイクする。</summary>
        public static GeneratedTextures Bake(RoadConfig config)
        {
            var ctx = BakeContext.Create(config);

            var strokes = StrokeResolver.Resolve(in ctx);
            var laneRanges = LaneRangeResolver.Resolve(in ctx);

            var heightMap = RoadNoise.StyleNoise(ctx.W, ctx.H, config.asphalt.noiseStyle, 1f, ctx.seed + 100);

            var albedoPixels = AlbedoBuilder.Build(in ctx, strokes, laneRanges, heightMap);
            var albedoTex = TextureUtils.MakeLinear(albedoPixels, ctx.W, ctx.H);

            // 塗装の凸を高さマップに加算する処理は Albedo パスのあとに実行する。アスファルト
            // シェーディングへの影響を避け、法線マップだけが塗装の凸を反映するようにするため。
            PaintHeightStamper.Apply(heightMap, in ctx, strokes, laneRanges);

            Texture2D normalTex = null;
            if (config.output.generateNormal)
            {
                var normalPixels = NormalBuilder.Build(heightMap, ctx.W, ctx.H);
                normalTex = TextureUtils.MakeLinear(normalPixels, ctx.W, ctx.H);
            }

            Texture2D msTex = null;
            if (config.output.generateMetallicSmoothness)
            {
                var msPixels = MetallicSmoothnessBuilder.Build(in ctx, laneRanges, strokes);
                msTex = TextureUtils.MakeLinear(msPixels, ctx.W, ctx.H);
            }

            Texture2D aoTex = null;
            if (config.output.generateAO)
            {
                var aoPixels = AOBuilder.Build(ctx.W, ctx.H, ctx.seed);
                aoTex = TextureUtils.MakeLinear(aoPixels, ctx.W, ctx.H);
            }

            return new GeneratedTextures
            {
                albedo = albedoTex,
                normal = normalTex,
                metallicSmoothness = msTex,
                ao = aoTex,
            };
        }
    }
}

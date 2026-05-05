using UnityEngine;

namespace MitarashiDango.RoadAssetGenerator
{
    /// <summary>V 軸方向に途切れなく続く実線。ステートレスなのでシングルトンとして共有可能。</summary>
    public sealed class SolidShape : IMarkingShape
    {
        public static readonly SolidShape Instance = new SolidShape();

        public int GetSlantPad(int halfWidthPx) => 0;
        public bool CanSkipRow(int y) => false;

        public bool TestPixel(int x, int y, int xCenter, int halfWidthPx, out float du)
        {
            du = x - xCenter;
            return Mathf.Abs(du) <= halfWidthPx;
        }

        public bool HasDiagonalEdges => false;
    }
}

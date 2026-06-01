using UnityEngine;

namespace MitarashiDango.RoadAssetGenerator
{
    /// <summary>V 軸方向に途切れなく続く実線。状態を持たないためシングルトンとして共有する。</summary>
    public sealed class SolidShape : IMarkingShape
    {
        public static readonly SolidShape Instance = new SolidShape();

        public int GetSlantPad(int halfWidthPx) => 0;
        public bool CanSkipRow(int y) => false;

        public bool TestPixel(int x, int y, int xCenter, int halfWidthPx, out float du, out bool isVEdge)
        {
            du = x - xCenter;
            isVEdge = false;
            return Mathf.Abs(du) <= halfWidthPx;
        }

        public bool HasDiagonalEdges => false;
    }
}

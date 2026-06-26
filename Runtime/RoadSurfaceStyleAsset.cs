using UnityEngine;

namespace MitarashiDango.RoadAssetGenerator
{
    /// <summary>道路区間へコピー適用する路面スタイルプリセット。</summary>
    [CreateAssetMenu(fileName = "RoadSurfaceStyle", menuName = "Road Asset Generator/Road Surface Style", order = 121)]
    public class RoadSurfaceStyleAsset : ScriptableObject
    {
        public RoadSurfaceStyle style = RoadSurfaceStyle.CreateDefault();

        public RoadSurfaceStyle CreateStyleCopy()
        {
            return style != null ? style.Clone() : RoadSurfaceStyle.CreateDefault();
        }

        private void Reset()
        {
            style = RoadSurfaceStyle.CreateDefault();
        }
    }
}

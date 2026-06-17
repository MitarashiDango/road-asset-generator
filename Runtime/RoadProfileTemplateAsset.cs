using UnityEngine;

namespace MitarashiDango.RoadAssetGenerator
{
    /// <summary>
    /// 道路プロファイルのテンプレート。道路区間へ適用するときは <see cref="RoadProfile.Clone"/> でコピーする。
    /// </summary>
    [CreateAssetMenu(fileName = "RoadProfileTemplate", menuName = "Road Asset Generator/Road Profile Template", order = 120)]
    public class RoadProfileTemplateAsset : ScriptableObject
    {
        public RoadProfile profile = RoadProfile.CreateDefaultTwoLane();

        private void Reset()
        {
            profile = RoadProfile.CreateDefaultTwoLane();
        }
    }
}

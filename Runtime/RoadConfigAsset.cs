using UnityEngine;

namespace MitarashiDango.RoadAssetGenerator
{
    /// <summary>
    /// <see cref="RoadConfig"/> をプロジェクトアセットとしてラップする ScriptableObject。
    /// Project ウィンドウで道路定義を編集し、シーンを跨いで再利用できる。
    /// </summary>
    [CreateAssetMenu(fileName = "RoadPreset", menuName = "Road Asset Generator/Road Preset", order = 100)]
    public class RoadConfigAsset : ScriptableObject
    {
        public RoadConfig config = new RoadConfig();

        private void Reset()
        {
            config = RoadConfig.PresetMountainRoad_NoOvertaking();
        }
    }
}

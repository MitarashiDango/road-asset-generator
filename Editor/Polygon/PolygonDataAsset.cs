using UnityEngine;

namespace MitarashiDango.RoadAssetGenerator
{
    [CreateAssetMenu(fileName = "PolygonShape", menuName = "Road Asset Generator/Polygon Shape", order = 110)]
    public class PolygonDataAsset : ScriptableObject
    {
        public PolygonData data = new PolygonData();
    }
}

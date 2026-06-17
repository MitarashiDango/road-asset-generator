#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace MitarashiDango.RoadAssetGenerator
{
    /// <summary>道路ネットワーク生成機能の作成メニュー。</summary>
    public static class RoadNetworkMenu
    {
        [MenuItem("GameObject/Road Asset Generator/Road Network", false, 10)]
        [MenuItem("Tools/Road Asset Generator/Create Road Network")]
        public static void CreateRoadNetwork(MenuCommand command)
        {
            var parent = command.context as GameObject;
            var networkObject = new GameObject(UniqueName(parent != null ? parent.transform : null, "RoadNetwork_001"));
            if (parent != null)
            {
                GameObjectUtility.SetParentAndAlign(networkObject, parent);
            }

            var network = networkObject.AddComponent<RoadNetwork>();
            Undo.RegisterCreatedObjectUndo(networkObject, "Create Road Network");
            var segment = CreateSegmentObject(network, "RoadSegment_001");
            RoadSegmentSurfaceGenerator.Regenerate(segment, false);

            Selection.activeGameObject = segment.gameObject;
        }

        [MenuItem("GameObject/Road Asset Generator/Road Segment", false, 11)]
        public static void CreateRoadSegment(MenuCommand command)
        {
            var selected = command.context as GameObject ?? Selection.activeGameObject;
            var network = selected != null ? selected.GetComponentInParent<RoadNetwork>() : null;
            if (network == null)
            {
                network = FindAnySceneNetwork();
            }

            if (network == null)
            {
                EditorUtility.DisplayDialog(
                    "Road Asset Generator",
                    "RoadNetwork が見つかりません。先に Road Network を作成してください。",
                    "OK");
                return;
            }

            var segment = CreateSegmentObject(network, UniqueName(network.transform, "RoadSegment_001"));
            RoadSegmentSurfaceGenerator.Regenerate(segment, false);
            Selection.activeGameObject = segment.gameObject;
        }

        [MenuItem("GameObject/Road Asset Generator/Road Segment", true)]
        public static bool ValidateCreateRoadSegment()
        {
            var selected = Selection.activeGameObject;
            return (selected != null && selected.GetComponentInParent<RoadNetwork>() != null) || FindAnySceneNetwork() != null;
        }

        private static RoadSegment CreateSegmentObject(RoadNetwork network, string name)
        {
            var segmentObject = new GameObject(name);
            segmentObject.transform.SetParent(network.transform, false);
            segmentObject.transform.localPosition = Vector3.zero;
            segmentObject.transform.localRotation = Quaternion.identity;
            segmentObject.transform.localScale = Vector3.one;

            var segment = segmentObject.AddComponent<RoadSegment>();
            segment.RefreshNetworkCache();
            Undo.RegisterCreatedObjectUndo(segmentObject, "Create Road Segment");
            EditorUtility.SetDirty(segment);
            return segment;
        }

        private static string UniqueName(Transform parent, string baseName)
        {
            if (parent != null)
            {
                return GameObjectUtility.GetUniqueNameForSibling(parent, baseName);
            }

            var candidate = baseName;
            var index = 2;
            while (GameObject.Find(candidate) != null)
            {
                candidate = $"RoadNetwork_{index:000}";
                index++;
            }

            return candidate;
        }

        private static RoadNetwork FindAnySceneNetwork()
        {
            var networks = Resources.FindObjectsOfTypeAll<RoadNetwork>();
            foreach (var network in networks)
            {
                if (network != null && !EditorUtility.IsPersistent(network) && network.gameObject.scene.IsValid())
                {
                    return network;
                }
            }

            return null;
        }
    }
}
#endif

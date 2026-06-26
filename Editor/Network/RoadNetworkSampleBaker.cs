#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MitarashiDango.RoadAssetGenerator
{
    /// <summary>Package sample scenes の生成済みプレビューを保存する保守用ユーティリティ。</summary>
    public static class RoadNetworkSampleBaker
    {
        private const string MvpSampleScenePath =
            "Packages/com.matcha-soft.road-asset-generator/Samples~/RoadNetworkMvp/RoadNetworkMvpSample.unity";

        [MenuItem("Tools/Road Asset Generator/Samples/Regenerate Road Network MVP Sample")]
        public static void RegenerateRoadNetworkMvpSample()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            BakeRoadNetworkMvpSample();
        }

        public static void BakeRoadNetworkMvpSample()
        {
            BakeSampleScene(MvpSampleScenePath);
        }

        private static void BakeSampleScene(string scenePath)
        {
            if (!File.Exists(scenePath))
            {
                throw new FileNotFoundException($"Road network sample scene not found: {scenePath}", scenePath);
            }

            var scene = EditorSceneManager.OpenScene(scenePath);
            var segments = new List<RoadSegment>();
            foreach (var root in scene.GetRootGameObjects())
            {
                root.GetComponentsInChildren(true, segments);
            }

            if (segments.Count == 0)
            {
                throw new InvalidOperationException($"Road network sample scene has no RoadSegment: {scenePath}");
            }

            foreach (var segment in segments)
            {
                segment.RefreshNetworkCache();
                RoadSegmentSurfaceGenerator.Regenerate(segment, false);
                EditorUtility.SetDirty(segment);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException($"Failed to save road network sample scene: {scenePath}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[RoadAssetGenerator] Baked road network sample scene: {scenePath}");
        }
    }
}
#endif

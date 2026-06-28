#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MitarashiDango.RoadAssetGenerator
{
    /// <summary>ローカル確認用サンプルシーンの生成済みプレビューを保存する保守用ユーティリティ。</summary>
    public static class RoadNetworkSampleBaker
    {
        private const string MvpSampleScenePath =
            "Assets/RoadAssetGeneratorLocalSamples/RoadNetworkMvp/RoadNetworkMvpSample.unity";

        [MenuItem("Tools/Road Asset Generator/Samples/Regenerate Road Network Local Sample")]
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
            CollectRoadSegments(scene, segments);

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

        private static void CollectRoadSegments(UnityEngine.SceneManagement.Scene scene, List<RoadSegment> segments)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                segments.AddRange(root.GetComponentsInChildren<RoadSegment>(true));
            }

            if (segments.Count > 0)
            {
                return;
            }

            foreach (var segment in Resources.FindObjectsOfTypeAll<RoadSegment>())
            {
                if (segment.gameObject.scene == scene)
                {
                    segments.Add(segment);
                }
            }
        }
    }
}
#endif

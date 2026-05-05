#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MitarashiDango.RoadAssetGenerator
{
    /// <summary>
    /// 組み込みプリセットを <see cref="RoadConfigAsset"/> としてプロジェクトに書き出す
    /// メニューコマンド群。
    /// </summary>
    public static class RoadPresetMenu
    {
        private const string DefaultFolder = "Assets/RoadPresets";

        [MenuItem("Tools/Road Asset Generator/Create Built-in Presets")]
        public static void CreateBuiltInPresets()
        {
            EnsureFolder(DefaultFolder);

            CreatePresetAsset("Mountain_NoOvertaking",  RoadConfig.PresetMountainRoad_NoOvertaking());
            CreatePresetAsset("Mountain_PassingOK",     RoadConfig.PresetMountainRoad_PassingOK());
            CreatePresetAsset("FourLane_DoubleYellow",  RoadConfig.PresetFourLane());
            CreatePresetAsset("Narrow_15Lane",          RoadConfig.PresetNarrowLane15());
            CreatePresetAsset("SingleLane",             RoadConfig.PresetSingleLane());
            CreatePresetAsset("NoLaneMarkings",         RoadConfig.PresetNoLaneMarkings());

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "Road Asset Generator",
                $"Built-in presets created in {DefaultFolder}.",
                "OK");

            var folderObj = AssetDatabase.LoadAssetAtPath<DefaultAsset>(DefaultFolder);
            if (folderObj != null)
            {
                EditorGUIUtility.PingObject(folderObj);
            }
        }

        /// <summary>
        /// 指定フォルダ内に <see cref="RoadConfigAsset"/> を 1 つ作成する。
        /// 同名ファイルが既に存在する場合は一意な名前に自動でリネームされる。
        /// </summary>
        public static RoadConfigAsset CreatePresetAsset(string name, RoadConfig config, string folder = DefaultFolder)
        {
            EnsureFolder(folder);
            var path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{name}.asset");
            var asset = ScriptableObject.CreateInstance<RoadConfigAsset>();
            asset.config = config;
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            return asset;
        }

        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath))
            {
                return;
            }
            var parent = Path.GetDirectoryName(assetPath).Replace('\\', '/');
            var leaf = Path.GetFileName(assetPath);
            if (!AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
#endif

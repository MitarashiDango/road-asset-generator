#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MitarashiDango.RoadAssetGenerator
{
    /// <summary>
    /// ベイクされたテクスチャをディスクに保存し、適切なインポート設定を適用したうえで、
    /// それらを参照するマテリアルを新規作成または更新する。
    /// </summary>
    public static class RoadMaterialFactory
    {
        public class SaveResult
        {
            public string albedoPath;
            public string normalPath;
            public string metallicSmoothnessPath;
            public string aoPath;
            public string materialPath;
        }

        private enum TextureKind { Color, Normal, Linear }

        public static SaveResult SaveAndCreateAssets(GeneratedTextures gen, RoadConfig config)
        {
            var folder = string.IsNullOrEmpty(config.output.outputFolder) ? "Assets" : config.output.outputFolder;
            EnsureFolder(folder);

            var prefix = string.IsNullOrEmpty(config.output.namePrefix) ? "road" : config.output.namePrefix;
            var result = new SaveResult();

            result.albedoPath = SavePng(gen.albedo, $"{folder}/{prefix}_albedo.png");
            ApplyImportSettings(result.albedoPath, TextureKind.Color);

            if (gen.normal != null)
            {
                result.normalPath = SavePng(gen.normal, $"{folder}/{prefix}_normal.png");
                ApplyImportSettings(result.normalPath, TextureKind.Normal);
            }
            if (gen.metallicSmoothness != null)
            {
                result.metallicSmoothnessPath = SavePng(gen.metallicSmoothness, $"{folder}/{prefix}_metallicSmoothness.png");
                ApplyImportSettings(result.metallicSmoothnessPath, TextureKind.Linear);
            }
            if (gen.ao != null)
            {
                result.aoPath = SavePng(gen.ao, $"{folder}/{prefix}_ao.png");
                ApplyImportSettings(result.aoPath, TextureKind.Linear);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (config.output.generateMaterial)
            {
                result.materialPath = $"{folder}/{prefix}_material.mat";
                CreateOrUpdateMaterial(result, config);
            }

            return result;
        }

        /// <summary>
        /// 現在アクティブなレンダーパイプラインを判定する。SRP が動作していない場合は
        /// Built-in を返す。
        /// </summary>
        public static PipelineTarget DetectPipeline()
        {
            var rp = GraphicsSettings.currentRenderPipeline;
            if (rp == null)
            {
                return PipelineTarget.BuiltIn;
            }
            var typeName = rp.GetType().FullName ?? string.Empty;
            if (typeName.Contains("Universal"))
            {
                return PipelineTarget.URP;
            }
            return PipelineTarget.BuiltIn;
        }

        private static void CreateOrUpdateMaterial(SaveResult paths, RoadConfig config)
        {
            var pipeline = config.output.pipelineTarget;
            if (pipeline == PipelineTarget.AutoDetect)
            {
                pipeline = DetectPipeline();
            }

            var shader = pipeline == PipelineTarget.URP
                ? Shader.Find("Universal Render Pipeline/Lit")
                : Shader.Find("Standard");

            if (shader == null)
            {
                Debug.LogWarning($"[RoadAssetGenerator] Shader for {pipeline} not found, falling back.");
                shader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit");
            }

            var mat = AssetDatabase.LoadAssetAtPath<Material>(paths.materialPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, paths.materialPath);
            }
            else
            {
                mat.shader = shader;
            }

            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(paths.albedoPath);
            var normal = string.IsNullOrEmpty(paths.normalPath) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(paths.normalPath);
            var ms = string.IsNullOrEmpty(paths.metallicSmoothnessPath) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(paths.metallicSmoothnessPath);
            var ao = string.IsNullOrEmpty(paths.aoPath) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(paths.aoPath);

            if (pipeline == PipelineTarget.URP)
            {
                ApplyUrpTextures(mat, albedo, normal, ms, ao);
            }
            else
            {
                ApplyBuiltInTextures(mat, albedo, normal, ms, ao);
            }

            mat.mainTextureScale = Vector2.one;
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
        }

        private static void ApplyUrpTextures(Material mat, Texture2D albedo, Texture2D normal, Texture2D ms, Texture2D ao)
        {
            mat.SetTexture("_BaseMap", albedo);
            mat.SetTexture("_MainTex", albedo); // 旧 URP 互換用。
            if (normal != null)
            {
                mat.SetTexture("_BumpMap", normal);
                mat.EnableKeyword("_NORMALMAP");
                mat.SetFloat("_BumpScale", 1.0f);
            }
            if (ms != null)
            {
                mat.SetTexture("_MetallicGlossMap", ms);
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                mat.SetFloat("_Metallic", 0f);
                mat.SetFloat("_Smoothness", 1f);
            }
            if (ao != null)
            {
                mat.SetTexture("_OcclusionMap", ao);
                mat.EnableKeyword("_OCCLUSIONMAP");
                mat.SetFloat("_OcclusionStrength", 1f);
            }
            mat.SetFloat("_WorkflowMode", 1); // 1 = メタリックワークフロー。
        }

        private static void ApplyBuiltInTextures(Material mat, Texture2D albedo, Texture2D normal, Texture2D ms, Texture2D ao)
        {
            mat.SetTexture("_MainTex", albedo);
            if (normal != null)
            {
                mat.SetTexture("_BumpMap", normal);
                mat.EnableKeyword("_NORMALMAP");
            }
            if (ms != null)
            {
                mat.SetTexture("_MetallicGlossMap", ms);
                mat.EnableKeyword("_METALLICGLOSSMAP");
                mat.SetFloat("_Metallic", 0f);
                mat.SetFloat("_Glossiness", 1f);
                mat.SetFloat("_GlossMapScale", 1f);
                mat.SetInt("_SmoothnessTextureChannel", 0); // メタリックマップの A チャンネルからスムースネスを読む。
            }
            if (ao != null)
            {
                mat.SetTexture("_OcclusionMap", ao);
                mat.SetFloat("_OcclusionStrength", 1f);
            }
        }

        private static string SavePng(Texture2D tex, string path)
        {
            var png = tex.EncodeToPNG();
            File.WriteAllBytes(path, png);
            AssetDatabase.ImportAsset(path);
            return path;
        }

        private static void ApplyImportSettings(string path, TextureKind kind)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                return;
            }
            importer.textureType = kind == TextureKind.Normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = (kind == TextureKind.Color);
            importer.alphaIsTransparency = false;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.anisoLevel = 8;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
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

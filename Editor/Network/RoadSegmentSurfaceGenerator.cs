#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace MitarashiDango.RoadAssetGenerator
{
    /// <summary>RoadSegment 配下の路面・区画線生成物を作成・破棄する。</summary>
    public static class RoadSegmentSurfaceGenerator
    {
        private const string SurfacesRootName = "Surfaces";
        private const string MarkingsRootName = "Markings";
        private const string SurfaceObjectNamePrefix = "Surface";
        private const string MarkingObjectNamePrefix = "Marking";
        private const string UndoRegenerateName = "Regenerate Road Geometry";
        private const string UndoClearName = "Clear Road Geometry";
        private const string UndoGeneratedLayerName = "Change Road Generated Layers";
        private const string BuiltInDepthBiasedMarkingShaderName = "MitarashiDango/RoadAssetGenerator/RoadMarkingDepthBiasedBuiltIn";
        private const string UrpDepthBiasedMarkingShaderName = "MitarashiDango/RoadAssetGenerator/RoadMarkingDepthBiasedURP";
        private const float DefaultMarkingMetallic = 0f;
        private const float DefaultMarkingSmoothness = 0.25f;
        private static readonly Dictionary<int, int> GeneratedHierarchyUndoGroups = new Dictionary<int, int>();
        private static readonly HashSet<string> LoggedMarkingShaderFallbacks = new HashSet<string>();

        public static void Regenerate(RoadSegment segment, bool registerUndo)
        {
            Regenerate(segment, registerUndo, true);
        }

        public static void Regenerate(RoadSegment segment, bool registerUndo, bool clearExistingWithUndo)
        {
            if (segment == null)
            {
                return;
            }

            var network = segment.Network;
            var surfaceMeshes = RoadSurfaceMeshBuilder.Build(segment, network);
            var markingMeshes = RoadMarkingMeshBuilder.Build(segment, network);
            Clear(segment, registerUndo && clearExistingWithUndo);

            if (surfaceMeshes.Count == 0 && markingMeshes.Count == 0)
            {
                return;
            }

            if (registerUndo)
            {
                Undo.RecordObject(segment, UndoRegenerateName);
            }

            CreateSurfaceObjects(segment, network, surfaceMeshes, registerUndo);
            CreateMarkingObjects(segment, network, markingMeshes, registerUndo);
            EditorUtility.SetDirty(segment);
        }

        public static void Clear(RoadSegment segment, bool registerUndo)
        {
            if (segment == null)
            {
                return;
            }

            if (registerUndo)
            {
                Undo.RecordObject(segment, UndoClearName);
            }

            ClearRoot(
                segment,
                SurfacesRootName,
                segment.surfacesRoot,
                segment.generatedSurfaceObjects,
                registerUndo,
                false);
            ClearRoot(
                segment,
                MarkingsRootName,
                segment.markingsRoot,
                segment.generatedMarkingObjects,
                registerUndo,
                true);

            segment.surfacesRoot = null;
            segment.markingsRoot = null;
            if (segment.generatedSurfaceObjects == null)
            {
                segment.generatedSurfaceObjects = new List<GameObject>();
            }
            else
            {
                segment.generatedSurfaceObjects.Clear();
            }

            if (segment.generatedMarkingObjects == null)
            {
                segment.generatedMarkingObjects = new List<GameObject>();
            }
            else
            {
                segment.generatedMarkingObjects.Clear();
            }

            EditorUtility.SetDirty(segment);
        }

        public static void RegisterGeneratedHierarchyUndo(RoadSegment segment, string undoName)
        {
            if (segment == null)
            {
                return;
            }

            var surfacesRoot = segment.surfacesRoot != null
                ? segment.surfacesRoot
                : FindDirectChild(segment.transform, SurfacesRootName);
            var markingsRoot = segment.markingsRoot != null
                ? segment.markingsRoot
                : FindDirectChild(segment.transform, MarkingsRootName);
            if (surfacesRoot == null && markingsRoot == null)
            {
                return;
            }

            var segmentId = segment.GetInstanceID();
            var undoGroup = Undo.GetCurrentGroup();
            if (GeneratedHierarchyUndoGroups.TryGetValue(segmentId, out var registeredGroup) &&
                registeredGroup == undoGroup)
            {
                return;
            }

            GeneratedHierarchyUndoGroups[segmentId] = undoGroup;
            if (surfacesRoot != null)
            {
                Undo.RegisterFullObjectHierarchyUndo(surfacesRoot, undoName);
            }
            if (markingsRoot != null)
            {
                Undo.RegisterFullObjectHierarchyUndo(markingsRoot, undoName);
            }
        }

        public static bool EnsureGeneratedSurfaceReferences(RoadSegment segment)
        {
            if (segment == null)
            {
                return false;
            }

            var surfacesOk = EnsureGeneratedReferences(
                segment,
                SurfacesRootName,
                ref segment.surfacesRoot,
                ref segment.generatedSurfaceObjects,
                true);
            var markingsOk = EnsureGeneratedReferences(
                segment,
                MarkingsRootName,
                ref segment.markingsRoot,
                ref segment.generatedMarkingObjects,
                ShouldRequireMarkings(segment));
            if (markingsOk)
            {
                RepairGeneratedMarkingRenderers(segment.markingsRoot, segment.generatedMarkingObjects, false);
            }
            return surfacesOk && markingsOk;
        }

        public static void ApplyGeneratedLayers(RoadSegment segment, bool registerUndo)
        {
            if (segment == null)
            {
                return;
            }

            var network = segment.Network;
            var surfacesRoot = segment.surfacesRoot != null
                ? segment.surfacesRoot
                : FindDirectChild(segment.transform, SurfacesRootName);
            var markingsRoot = segment.markingsRoot != null
                ? segment.markingsRoot
                : FindDirectChild(segment.transform, MarkingsRootName);

            ApplyGeneratedLayer(
                surfacesRoot,
                segment.generatedSurfaceObjects,
                RoadGeneratedLayerSettings.ResolveSurfaceLayer(segment, network),
                registerUndo);
            ApplyGeneratedLayer(
                markingsRoot,
                segment.generatedMarkingObjects,
                RoadGeneratedLayerSettings.ResolveMarkingLayer(segment, network),
                registerUndo);
            RepairGeneratedMarkingRenderers(markingsRoot, segment.generatedMarkingObjects, registerUndo);
        }

        private static void CreateSurfaceObjects(
            RoadSegment segment,
            RoadNetwork network,
            IReadOnlyList<RoadSurfaceMeshData> meshes,
            bool registerUndo)
        {
            if (meshes.Count == 0)
            {
                return;
            }

            var root = CreateRoot(segment, SurfacesRootName, registerUndo);
            segment.surfacesRoot = root;
            var layer = RoadGeneratedLayerSettings.ResolveSurfaceLayer(segment, network);
            root.layer = layer;
            var material = ResolveSurfaceMaterial(segment, network);
            segment.generatedSurfaceObjects = new List<GameObject>(meshes.Count);

            for (var i = 0; i < meshes.Count; i++)
            {
                var meshData = meshes[i];
                var surfaceObject = new GameObject($"{SurfaceObjectNamePrefix}_{i:000}");
                surfaceObject.layer = layer;
                var meshFilter = surfaceObject.AddComponent<MeshFilter>();
                var meshRenderer = surfaceObject.AddComponent<MeshRenderer>();
                meshFilter.sharedMesh = meshData.mesh;
                meshRenderer.sharedMaterial = material;
                RegisterGeneratedObject(surfaceObject, meshData.mesh, null, root, registerUndo);
                segment.generatedSurfaceObjects.Add(surfaceObject);
            }
        }

        private static void CreateMarkingObjects(
            RoadSegment segment,
            RoadNetwork network,
            IReadOnlyList<RoadMarkingMeshData> meshes,
            bool registerUndo)
        {
            if (meshes.Count == 0)
            {
                return;
            }

            var root = CreateRoot(segment, MarkingsRootName, registerUndo);
            segment.markingsRoot = root;
            var layer = RoadGeneratedLayerSettings.ResolveMarkingLayer(segment, network);
            root.layer = layer;
            segment.generatedMarkingObjects = new List<GameObject>(meshes.Count);

            for (var i = 0; i < meshes.Count; i++)
            {
                var meshData = meshes[i];
                var markingObject = new GameObject($"{MarkingObjectNamePrefix}_{i:000}");
                markingObject.layer = layer;
                var meshFilter = markingObject.AddComponent<MeshFilter>();
                var meshRenderer = markingObject.AddComponent<MeshRenderer>();
                var material = CreateMarkingMaterial(meshData, network);
                meshFilter.sharedMesh = meshData.mesh;
                meshRenderer.sharedMaterial = material;
                ConfigureMarkingRenderer(meshRenderer);
                RegisterGeneratedObject(markingObject, meshData.mesh, material, root, registerUndo);
                segment.generatedMarkingObjects.Add(markingObject);
            }
        }

        private static void ConfigureMarkingRenderer(MeshRenderer renderer)
        {
            if (renderer == null)
            {
                return;
            }

            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
        }

        private static void RepairGeneratedMarkingRenderers(
            GameObject root,
            IReadOnlyList<GameObject> generatedObjects,
            bool registerUndo)
        {
            if (root != null)
            {
                var rootRenderers = root.GetComponentsInChildren<MeshRenderer>(true);
                foreach (var renderer in rootRenderers)
                {
                    RepairGeneratedMarkingRenderer(renderer, registerUndo);
                }
                return;
            }

            if (generatedObjects != null && generatedObjects.Count > 0)
            {
                foreach (var generatedObject in generatedObjects)
                {
                    if (generatedObject == null)
                    {
                        continue;
                    }

                    var renderers = generatedObject.GetComponentsInChildren<MeshRenderer>(true);
                    foreach (var renderer in renderers)
                    {
                        RepairGeneratedMarkingRenderer(renderer, registerUndo);
                    }
                }
                return;
            }
        }

        private static void RepairGeneratedMarkingRenderer(MeshRenderer renderer, bool registerUndo)
        {
            if (!NeedsMarkingRendererRepair(renderer))
            {
                return;
            }

            if (registerUndo)
            {
                Undo.RecordObject(renderer, UndoGeneratedLayerName);
            }

            ConfigureMarkingRenderer(renderer);
            EditorUtility.SetDirty(renderer);
        }

        private static bool NeedsMarkingRendererRepair(MeshRenderer renderer)
        {
            return renderer != null &&
                (renderer.shadowCastingMode != ShadowCastingMode.Off ||
                !renderer.receiveShadows ||
                renderer.lightProbeUsage != LightProbeUsage.BlendProbes ||
                renderer.reflectionProbeUsage != ReflectionProbeUsage.BlendProbes);
        }

        private static void RegisterGeneratedObject(
            GameObject generatedObject,
            Mesh mesh,
            Material material,
            GameObject root,
            bool registerUndo)
        {
            if (registerUndo)
            {
                if (mesh != null)
                {
                    Undo.RegisterCreatedObjectUndo(mesh, UndoRegenerateName);
                }
                if (material != null)
                {
                    Undo.RegisterCreatedObjectUndo(material, UndoRegenerateName);
                }
                Undo.RegisterCreatedObjectUndo(generatedObject, UndoRegenerateName);
                Undo.SetTransformParent(generatedObject.transform, root.transform, UndoRegenerateName);
            }
            else
            {
                generatedObject.transform.SetParent(root.transform, false);
            }

            generatedObject.transform.localPosition = Vector3.zero;
            generatedObject.transform.localRotation = Quaternion.identity;
            generatedObject.transform.localScale = Vector3.one;
        }

        private static void ClearRoot(
            RoadSegment segment,
            string rootName,
            GameObject serializedRoot,
            IReadOnlyList<GameObject> generatedObjects,
            bool registerUndo,
            bool destroySceneMaterials)
        {
            var root = serializedRoot != null
                ? serializedRoot
                : FindDirectChild(segment.transform, rootName);

            if (root != null)
            {
                DestroyGeneratedObject(root, registerUndo, destroySceneMaterials);
            }
            else if (generatedObjects != null)
            {
                foreach (var generatedObject in generatedObjects)
                {
                    if (generatedObject != null)
                    {
                        DestroyGeneratedObject(generatedObject, registerUndo, destroySceneMaterials);
                    }
                }
            }
        }

        private static bool EnsureGeneratedReferences(
            RoadSegment segment,
            string rootName,
            ref GameObject serializedRoot,
            ref List<GameObject> generatedObjects,
            bool requireRoot)
        {
            if (HasValidGeneratedReferences(serializedRoot, generatedObjects))
            {
                return true;
            }

            var root = serializedRoot != null
                ? serializedRoot
                : FindDirectChild(segment.transform, rootName);
            if (root == null)
            {
                return !requireRoot;
            }

            var filters = root.GetComponentsInChildren<MeshFilter>(true);
            var objects = new List<GameObject>(filters.Length);
            foreach (var filter in filters)
            {
                if (filter != null && filter.sharedMesh != null)
                {
                    objects.Add(filter.gameObject);
                }
            }

            if (objects.Count == 0)
            {
                return !requireRoot;
            }

            serializedRoot = root;
            generatedObjects = objects;
            EditorUtility.SetDirty(segment);
            return true;
        }

        private static GameObject CreateRoot(RoadSegment segment, string rootName, bool registerUndo)
        {
            var root = new GameObject(rootName);
            if (registerUndo)
            {
                Undo.RegisterCreatedObjectUndo(root, UndoRegenerateName);
                Undo.SetTransformParent(root.transform, segment.transform, UndoRegenerateName);
            }
            else
            {
                root.transform.SetParent(segment.transform, false);
            }

            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            return root;
        }

        private static void ApplyGeneratedLayer(
            GameObject root,
            IReadOnlyList<GameObject> generatedObjects,
            int layer,
            bool registerUndo)
        {
            layer = RoadGeneratedLayerSettings.NormalizeLayer(layer);
            if (root != null)
            {
                SetLayerRecursively(root, layer, registerUndo);
                return;
            }

            if (generatedObjects == null)
            {
                return;
            }

            foreach (var generatedObject in generatedObjects)
            {
                if (generatedObject != null)
                {
                    SetLayer(generatedObject, layer, registerUndo);
                }
            }
        }

        private static void SetLayerRecursively(GameObject root, int layer, bool registerUndo)
        {
            var transforms = root.GetComponentsInChildren<Transform>(true);
            foreach (var child in transforms)
            {
                if (child != null)
                {
                    SetLayer(child.gameObject, layer, registerUndo);
                }
            }
        }

        private static void SetLayer(GameObject gameObject, int layer, bool registerUndo)
        {
            if (gameObject == null || gameObject.layer == layer)
            {
                return;
            }

            if (registerUndo)
            {
                Undo.RecordObject(gameObject, UndoGeneratedLayerName);
            }

            gameObject.layer = layer;
            EditorUtility.SetDirty(gameObject);
        }

        private static Material ResolveSurfaceMaterial(RoadSegment segment, RoadNetwork network)
        {
            var material = RoadSurfaceStyle.ResolveMaterial(segment, network);
            if (material != null)
            {
                return material;
            }

            return AssetDatabase.GetBuiltinExtraResource<Material>("Default-Material.mat");
        }

        private static Material CreateMarkingMaterial(RoadMarkingMeshData meshData, RoadNetwork network)
        {
            var source = network != null ? network.markingMaterial : null;
            var material = source != null
                ? new Material(source)
                : new Material(FindDefaultMarkingShader());
            material.name = $"RoadMarking_{meshData.boundaryIndex:00}_{meshData.strokeIndex:00}";
            if (source == null)
            {
                ConfigureDefaultMarkingMaterial(material);
            }
            ApplyMarkingColor(material, meshData.color);
            var targetQueue = (int)RenderQueue.Geometry + 20;
            if (material.renderQueue < targetQueue)
            {
                material.renderQueue = targetQueue;
            }
            return material;
        }

        private static Shader FindDefaultMarkingShader()
        {
            var pipeline = RoadMaterialFactory.DetectPipeline();
            if (pipeline == PipelineTarget.URP)
            {
                var shader = Shader.Find(UrpDepthBiasedMarkingShaderName);
                if (shader != null)
                {
                    return shader;
                }

                shader = Shader.Find("Universal Render Pipeline/Lit") ??
                    Shader.Find("Universal Render Pipeline/Unlit");
                if (shader != null)
                {
                    return UseFallbackMarkingShader(UrpDepthBiasedMarkingShaderName, shader);
                }

                var builtInFallbackShader = Shader.Find(BuiltInDepthBiasedMarkingShaderName);
                if (builtInFallbackShader != null)
                {
                    return UseFallbackMarkingShader(UrpDepthBiasedMarkingShaderName, builtInFallbackShader);
                }
            }
            else
            {
                var builtInShader = Shader.Find(BuiltInDepthBiasedMarkingShaderName);
                if (builtInShader != null)
                {
                    return builtInShader;
                }
            }

            var fallbackShader = Shader.Find("Standard") ??
                Shader.Find("Sprites/Default") ??
                Shader.Find("Hidden/InternalErrorShader");
            var expectedShaderName = pipeline == PipelineTarget.URP
                ? UrpDepthBiasedMarkingShaderName
                : BuiltInDepthBiasedMarkingShaderName;
            return UseFallbackMarkingShader(expectedShaderName, fallbackShader);
        }

        private static Shader UseFallbackMarkingShader(string expectedShaderName, Shader fallbackShader)
        {
            if (fallbackShader == null)
            {
                return null;
            }

            var key = $"{expectedShaderName}|{fallbackShader.name}";
            if (LoggedMarkingShaderFallbacks.Add(key))
            {
                Debug.LogWarning(
                    $"[RoadAssetGenerator] Default road marking shader '{expectedShaderName}' was not found. " +
                    $"Falling back to '{fallbackShader.name}'. Package depth-bias behavior may be weaker or unavailable; " +
                    "generated markings can z-fight with the road until the package shader imports correctly or a custom material provides equivalent offset.");
            }

            return fallbackShader;
        }

        private static void ConfigureDefaultMarkingMaterial(Material material)
        {
            if (material == null)
            {
                return;
            }

            SetFloatIfPresent(material, "_Metallic", DefaultMarkingMetallic);
            SetFloatIfPresent(material, "_Glossiness", DefaultMarkingSmoothness);
            SetFloatIfPresent(material, "_Smoothness", DefaultMarkingSmoothness);
            SetFloatIfPresent(material, "_WorkflowMode", 1f);
            SetFloatIfPresent(material, "_Surface", 0f);
            SetFloatIfPresent(material, "_Blend", 0f);
            SetFloatIfPresent(material, "_AlphaClip", 0f);
            SetFloatIfPresent(material, "_ZWrite", 1f);
            SetFloatIfPresent(material, "_Cull", (float)CullMode.Back);
            SetFloatIfPresent(material, "_ReceiveShadows", 1f);
        }

        private static void SetFloatIfPresent(Material material, string propertyName, float value)
        {
            if (material.HasProperty(propertyName))
            {
                material.SetFloat(propertyName, value);
            }
        }

        private static void ApplyMarkingColor(Material material, Color color)
        {
            if (material == null)
            {
                return;
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }
        }

        private static GameObject FindDirectChild(Transform parent, string childName)
        {
            if (parent == null)
            {
                return null;
            }

            for (var i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.name == childName)
                {
                    return child.gameObject;
                }
            }

            return null;
        }

        private static bool HasValidGeneratedReferences(GameObject root, IReadOnlyList<GameObject> generatedObjects)
        {
            if (root == null || generatedObjects == null || generatedObjects.Count == 0)
            {
                return false;
            }

            foreach (var generatedObject in generatedObjects)
            {
                if (generatedObject == null)
                {
                    return false;
                }

                var filter = generatedObject.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ShouldRequireMarkings(RoadSegment segment)
        {
            var profile = segment != null ? segment.GetActiveProfile() : null;
            if (profile?.boundaryLines == null)
            {
                return false;
            }

            foreach (var boundaryLine in profile.boundaryLines)
            {
                if (boundaryLine?.strokes == null)
                {
                    continue;
                }

                foreach (var stroke in boundaryLine.strokes)
                {
                    if (stroke != null &&
                        stroke.kind != RoadLineKind.None &&
                        stroke.widthMeters > CatmullRomSpline.Epsilon)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static void DestroyGeneratedObject(GameObject root, bool registerUndo, bool destroySceneMaterials)
        {
            var meshes = CollectSceneMeshes(root);
            var materials = destroySceneMaterials ? CollectSceneMaterials(root) : new List<Material>();
            if (registerUndo)
            {
                Undo.RegisterFullObjectHierarchyUndo(root, UndoClearName);
                Undo.DestroyObjectImmediate(root);
                foreach (var mesh in meshes)
                {
                    Undo.DestroyObjectImmediate(mesh);
                }
                foreach (var material in materials)
                {
                    Undo.DestroyObjectImmediate(material);
                }
            }
            else
            {
                Object.DestroyImmediate(root);
                foreach (var mesh in meshes)
                {
                    Object.DestroyImmediate(mesh);
                }
                foreach (var material in materials)
                {
                    Object.DestroyImmediate(material);
                }
            }
        }

        private static List<Mesh> CollectSceneMeshes(GameObject root)
        {
            var meshes = new List<Mesh>();
            if (root == null)
            {
                return meshes;
            }

            var filters = root.GetComponentsInChildren<MeshFilter>(true);
            foreach (var filter in filters)
            {
                var mesh = filter.sharedMesh;
                if (mesh != null && !AssetDatabase.Contains(mesh) && !meshes.Contains(mesh))
                {
                    meshes.Add(mesh);
                }
            }

            return meshes;
        }

        private static List<Material> CollectSceneMaterials(GameObject root)
        {
            var materials = new List<Material>();
            if (root == null)
            {
                return materials;
            }

            var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var renderer in renderers)
            {
                var sharedMaterials = renderer.sharedMaterials;
                foreach (var material in sharedMaterials)
                {
                    if (material != null && !AssetDatabase.Contains(material) && !materials.Contains(material))
                    {
                        materials.Add(material);
                    }
                }
            }

            return materials;
        }
    }
}
#endif

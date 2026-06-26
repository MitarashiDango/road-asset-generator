#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MitarashiDango.RoadAssetGenerator
{
    /// <summary>RoadSegment 配下の路面生成物を作成・破棄する。</summary>
    public static class RoadSegmentSurfaceGenerator
    {
        private const string SurfacesRootName = "Surfaces";
        private const string SurfaceObjectNamePrefix = "Surface";
        private const string UndoRegenerateName = "Regenerate Road Surfaces";
        private const string UndoClearName = "Clear Road Surfaces";
        private static readonly Dictionary<int, int> GeneratedHierarchyUndoGroups = new Dictionary<int, int>();

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
            var meshes = RoadSurfaceMeshBuilder.Build(segment, network);
            Clear(segment, registerUndo && clearExistingWithUndo);

            if (meshes.Count == 0)
            {
                return;
            }

            if (registerUndo)
            {
                Undo.RecordObject(segment, UndoRegenerateName);
            }

            var root = CreateRoot(segment, registerUndo);
            var material = ResolveSurfaceMaterial(segment, network);
            segment.generatedSurfaceObjects = new List<GameObject>(meshes.Count);

            for (var i = 0; i < meshes.Count; i++)
            {
                var meshData = meshes[i];
                var surfaceObject = new GameObject($"{SurfaceObjectNamePrefix}_{i:000}");
                var meshFilter = surfaceObject.AddComponent<MeshFilter>();
                var meshRenderer = surfaceObject.AddComponent<MeshRenderer>();
                meshFilter.sharedMesh = meshData.mesh;
                meshRenderer.sharedMaterial = material;
                if (registerUndo)
                {
                    Undo.RegisterCreatedObjectUndo(meshData.mesh, UndoRegenerateName);
                    Undo.RegisterCreatedObjectUndo(surfaceObject, UndoRegenerateName);
                    Undo.SetTransformParent(surfaceObject.transform, root.transform, UndoRegenerateName);
                }
                else
                {
                    surfaceObject.transform.SetParent(root.transform, false);
                }

                surfaceObject.transform.localPosition = Vector3.zero;
                surfaceObject.transform.localRotation = Quaternion.identity;
                surfaceObject.transform.localScale = Vector3.one;
                segment.generatedSurfaceObjects.Add(surfaceObject);
            }

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

            var root = segment.surfacesRoot != null
                ? segment.surfacesRoot
                : FindDirectChild(segment.transform, SurfacesRootName);

            if (root != null)
            {
                DestroyGeneratedObject(root, registerUndo);
            }
            else if (segment.generatedSurfaceObjects != null)
            {
                foreach (var generatedObject in segment.generatedSurfaceObjects)
                {
                    if (generatedObject != null)
                    {
                        DestroyGeneratedObject(generatedObject, registerUndo);
                    }
                }
            }

            segment.surfacesRoot = null;
            if (segment.generatedSurfaceObjects == null)
            {
                segment.generatedSurfaceObjects = new List<GameObject>();
            }
            else
            {
                segment.generatedSurfaceObjects.Clear();
            }

            EditorUtility.SetDirty(segment);
        }

        public static void RegisterGeneratedHierarchyUndo(RoadSegment segment, string undoName)
        {
            if (segment == null || segment.surfacesRoot == null)
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
            // Keep this scoped to generated surfaces; registering segment.gameObject snapshots unrelated children.
            Undo.RegisterFullObjectHierarchyUndo(segment.surfacesRoot, undoName);
        }

        public static bool EnsureGeneratedSurfaceReferences(RoadSegment segment)
        {
            if (HasValidGeneratedSurfaceReferences(segment))
            {
                return true;
            }

            var root = segment != null && segment.surfacesRoot != null
                ? segment.surfacesRoot
                : FindDirectChild(segment != null ? segment.transform : null, SurfacesRootName);
            if (root == null)
            {
                return false;
            }

            var filters = root.GetComponentsInChildren<MeshFilter>(true);
            var surfaceObjects = new List<GameObject>(filters.Length);
            foreach (var filter in filters)
            {
                if (filter != null && filter.sharedMesh != null)
                {
                    surfaceObjects.Add(filter.gameObject);
                }
            }

            if (surfaceObjects.Count == 0)
            {
                return false;
            }

            segment.surfacesRoot = root;
            segment.generatedSurfaceObjects = surfaceObjects;
            EditorUtility.SetDirty(segment);
            return true;
        }

        private static GameObject CreateRoot(RoadSegment segment, bool registerUndo)
        {
            var root = new GameObject(SurfacesRootName);
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
            segment.surfacesRoot = root;
            return root;
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

        private static bool HasValidGeneratedSurfaceReferences(RoadSegment segment)
        {
            if (segment == null || segment.surfacesRoot == null || segment.generatedSurfaceObjects == null ||
                segment.generatedSurfaceObjects.Count == 0)
            {
                return false;
            }

            foreach (var surfaceObject in segment.generatedSurfaceObjects)
            {
                if (surfaceObject == null)
                {
                    return false;
                }

                var filter = surfaceObject.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null)
                {
                    return false;
                }
            }

            return true;
        }

        private static void DestroyGeneratedObject(GameObject root, bool registerUndo)
        {
            var meshes = CollectSceneMeshes(root);
            if (registerUndo)
            {
                Undo.RegisterFullObjectHierarchyUndo(root, UndoClearName);
                Undo.DestroyObjectImmediate(root);
                foreach (var mesh in meshes)
                {
                    Undo.DestroyObjectImmediate(mesh);
                }
            }
            else
            {
                Object.DestroyImmediate(root);
                foreach (var mesh in meshes)
                {
                    Object.DestroyImmediate(mesh);
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
    }
}
#endif

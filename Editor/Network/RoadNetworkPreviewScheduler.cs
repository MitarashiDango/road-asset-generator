#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MitarashiDango.RoadAssetGenerator
{
    /// <summary>Inspector / SceneView 編集後の路面プレビュー再生成を遅延実行する。</summary>
    [InitializeOnLoad]
    public static class RoadNetworkPreviewScheduler
    {
        private static readonly HashSet<int> PendingSegmentIds = new HashSet<int>();
        private static readonly Dictionary<int, int> PendingUndoGroupsBySegmentId = new Dictionary<int, int>();
        private static readonly Dictionary<int, int> KnownSegmentStateHashes = new Dictionary<int, int>();
        private static bool scheduled;

        static RoadNetworkPreviewScheduler()
        {
            Undo.undoRedoPerformed += RegenerateChangedSegmentsAfterUndo;
        }

        public static void Schedule(RoadSegment segment)
        {
            Schedule(segment, false);
        }

        public static void Schedule(RoadSegment segment, bool registerUndo)
        {
            if (segment == null)
            {
                return;
            }

            var segmentId = segment.GetInstanceID();
            PendingSegmentIds.Add(segmentId);
            if (registerUndo)
            {
                var currentGroup = Undo.GetCurrentGroup();
                if (!PendingUndoGroupsBySegmentId.TryGetValue(segmentId, out var pendingGroup) ||
                    currentGroup < pendingGroup)
                {
                    PendingUndoGroupsBySegmentId[segmentId] = currentGroup;
                }
            }

            if (scheduled)
            {
                return;
            }

            scheduled = true;
            EditorApplication.delayCall += Flush;
        }

        public static void Schedule(RoadNetwork network)
        {
            Schedule(network, false);
        }

        public static void Schedule(RoadNetwork network, bool registerUndo)
        {
            if (network == null)
            {
                return;
            }

            var segments = new List<RoadSegment>();
            network.CollectSegments(segments);
            foreach (var segment in segments)
            {
                Schedule(segment, registerUndo);
            }
        }

#if UNITY_INCLUDE_TESTS
        public static void FlushForTests()
        {
            if (!scheduled)
            {
                return;
            }

            EditorApplication.delayCall -= Flush;
            Flush();
        }
#endif

        private static void Flush()
        {
            scheduled = false;
            var ids = new List<int>(PendingSegmentIds);
            var undoGroupsBySegmentId = new Dictionary<int, int>(PendingUndoGroupsBySegmentId);
            PendingSegmentIds.Clear();
            PendingUndoGroupsBySegmentId.Clear();

            var shouldCollapseUndo = false;
            var collapseGroup = int.MaxValue;
            foreach (var id in ids)
            {
                var segment = EditorUtility.InstanceIDToObject(id) as RoadSegment;
                if (segment != null)
                {
                    var registerUndo = undoGroupsBySegmentId.TryGetValue(id, out var undoGroup);
                    if (registerUndo)
                    {
                        shouldCollapseUndo = true;
                        collapseGroup = Mathf.Min(collapseGroup, undoGroup);
                    }

                    RoadSegmentSurfaceGenerator.Regenerate(segment, registerUndo);
                    KnownSegmentStateHashes[id] = CalculateSegmentStateHash(segment);
                }
            }

            if (shouldCollapseUndo)
            {
                Undo.CollapseUndoOperations(collapseGroup);
            }

            SceneView.RepaintAll();
        }

        private static void RegenerateChangedSegmentsAfterUndo()
        {
            var segments = Resources.FindObjectsOfTypeAll<RoadSegment>();
            foreach (var segment in segments)
            {
                if (!IsSceneObject(segment))
                {
                    continue;
                }

                var segmentId = segment.GetInstanceID();
                var currentHash = CalculateSegmentStateHash(segment);
                if (!KnownSegmentStateHashes.TryGetValue(segmentId, out var knownHash))
                {
                    KnownSegmentStateHashes[segmentId] = currentHash;
                    continue;
                }

                if (knownHash != currentHash)
                {
                    KnownSegmentStateHashes[segmentId] = currentHash;
                    RoadSegmentSurfaceGenerator.ApplyGeneratedLayers(segment, false);
                    if (NeedsGeneratedSurfaceSync(segment))
                    {
                        Schedule(segment);
                    }
                }
            }
        }

        private static bool IsSceneObject(Object value)
        {
            if (value == null || EditorUtility.IsPersistent(value))
            {
                return false;
            }

            var component = value as Component;
            return component != null && component.gameObject.scene.IsValid();
        }

        private static bool NeedsGeneratedSurfaceSync(RoadSegment segment)
        {
            return !RoadSegmentSurfaceGenerator.EnsureGeneratedSurfaceReferences(segment) ||
                !RoadSegmentSurfaceGenerator.AreGeneratedSurfaceCollidersSynced(segment);
        }

        private static int CalculateSegmentStateHash(RoadSegment segment)
        {
            unchecked
            {
                var hash = 17;
                AddTransformHash(segment.transform, ref hash);
                AddNetworkHash(segment.Network, ref hash);
                AddSurfaceStyleHash(segment, segment.Network, ref hash);
                AddSurfaceSamplingHash(segment, segment.Network, ref hash);
                AddSurfaceColliderHash(segment, segment.Network, ref hash);
                AddGeneratedLayerHash(segment, segment.Network, ref hash);
                AddControlPointHash(segment.controlPoints, ref hash);
                AddProfileKeyHash(segment.profileKeys, ref hash);
                return hash;
            }
        }

        private static void AddTransformHash(Transform transform, ref int hash)
        {
            AddVector3Hash(transform.position, ref hash);
            AddVector3Hash(transform.eulerAngles, ref hash);
            AddVector3Hash(transform.lossyScale, ref hash);
        }

        private static void AddNetworkHash(RoadNetwork network, ref int hash)
        {
            if (network == null)
            {
                hash = hash * 31;
                return;
            }

            AddFloatHash(network.meshSegmentLengthMeters, ref hash);
            AddFloatHash(network.markingVertexOffsetMeters, ref hash);
            hash = hash * 31 + (network.markingMaterial != null ? network.markingMaterial.GetInstanceID() : 0);
            if (network.markingMaterial == null)
            {
                hash = hash * 31 + (RoadMaterialFactory.DetectPipeline() == PipelineTarget.URP ? 1 : 0);
            }
        }

        private static void AddSurfaceStyleHash(RoadSegment segment, RoadNetwork network, ref int hash)
        {
            var usesSegmentStyle = RoadSurfaceStyle.HasSegmentStyle(segment);
            hash = hash * 31 + (usesSegmentStyle ? 1 : 0);
            AddFloatHash(RoadSurfaceStyle.ResolveTextureLengthMeters(segment, network), ref hash);
            var material = RoadSurfaceStyle.ResolveMaterial(segment, network);
            hash = hash * 31 + (material != null ? material.GetInstanceID() : 0);
        }

        private static void AddSurfaceSamplingHash(RoadSegment segment, RoadNetwork network, ref int hash)
        {
            var usesSegmentOverride = segment != null && segment.overrideSurfaceSamplingSettings;
            hash = hash * 31 + (usesSegmentOverride ? 1 : 0);
            if (usesSegmentOverride)
            {
                AddFloatHash(RoadSurfaceSamplingSettings.ResolveMaxSampleLengthMeters(segment, network), ref hash);
                AddFloatHash(RoadSurfaceSamplingSettings.ResolveMaxSampleAngleDegrees(segment, network), ref hash);
                AddFloatHash(RoadSurfaceSamplingSettings.ResolveMaxColumnWidthMeters(segment, network), ref hash);
                return;
            }

            AddFloatHash(RoadSurfaceSamplingSettings.ResolveMaxSampleLengthMeters(null, network), ref hash);
            AddFloatHash(RoadSurfaceSamplingSettings.ResolveMaxSampleAngleDegrees(null, network), ref hash);
            AddFloatHash(RoadSurfaceSamplingSettings.ResolveMaxColumnWidthMeters(null, network), ref hash);
        }

        private static void AddGeneratedLayerHash(RoadSegment segment, RoadNetwork network, ref int hash)
        {
            hash = hash * 31 + RoadGeneratedLayerSettings.ResolveSurfaceLayer(segment, network);
            hash = hash * 31 + RoadGeneratedLayerSettings.ResolveMarkingLayer(segment, network);
        }

        private static void AddSurfaceColliderHash(RoadSegment segment, RoadNetwork network, ref int hash)
        {
            hash = hash * 31 + (RoadSurfaceColliderSettings.ResolveGenerateSurfaceColliders(segment, network) ? 1 : 0);
        }

        private static void AddControlPointHash(SplinePoint[] points, ref int hash)
        {
            hash = hash * 31 + (points?.Length ?? 0);
            if (points == null)
            {
                return;
            }

            foreach (var point in points)
            {
                AddVector3Hash(point?.position ?? Vector3.zero, ref hash);
            }
        }

        private static void AddProfileKeyHash(RoadProfileKey[] keys, ref int hash)
        {
            hash = hash * 31 + (keys?.Length ?? 0);
            if (keys == null)
            {
                return;
            }

            foreach (var key in keys)
            {
                if (key == null)
                {
                    hash = hash * 31;
                    continue;
                }

                AddFloatHash(key.positionMeters, ref hash);
                AddFloatHash(key.transitionLengthMeters, ref hash);
                hash = hash * 31 + (int)key.laneTransitionSide;
                AddProfileHash(key.profile, ref hash);
            }
        }

        private static void AddProfileHash(RoadProfile profile, ref int hash)
        {
            if (profile == null)
            {
                hash = hash * 31;
                return;
            }

            AddFloatHash(profile.leftShoulderWidthMeters, ref hash);
            AddFloatHash(profile.rightShoulderWidthMeters, ref hash);

            hash = hash * 31 + (profile.lanes?.Count ?? 0);
            if (profile.lanes != null)
            {
                foreach (var lane in profile.lanes)
                {
                    if (lane == null)
                    {
                        hash = hash * 31;
                        continue;
                    }

                    AddFloatHash(lane.widthMeters, ref hash);
                    hash = hash * 31 + (int)lane.direction;
                }
            }

            hash = hash * 31 + (profile.boundaryLines?.Count ?? 0);
            if (profile.boundaryLines == null)
            {
                return;
            }

            foreach (var boundaryLine in profile.boundaryLines)
            {
                AddBoundaryLineHash(boundaryLine, ref hash);
            }
        }

        private static void AddBoundaryLineHash(RoadBoundaryLine boundaryLine, ref int hash)
        {
            if (boundaryLine == null)
            {
                hash = hash * 31;
                return;
            }

            AddFloatHash(boundaryLine.strokeSpacingMeters, ref hash);
            hash = hash * 31 + (boundaryLine.strokes?.Count ?? 0);
            if (boundaryLine.strokes == null)
            {
                return;
            }

            foreach (var stroke in boundaryLine.strokes)
            {
                if (stroke == null)
                {
                    hash = hash * 31;
                    continue;
                }

                hash = hash * 31 + (int)stroke.kind;
                AddFloatHash(stroke.widthMeters, ref hash);
                AddColorHash(stroke.color, ref hash);
                AddFloatHash(stroke.dashLengthMeters, ref hash);
                AddFloatHash(stroke.dashGapMeters, ref hash);
                AddMarkingDetailHash(stroke.markingDetail, ref hash);
            }
        }

        private static void AddMarkingDetailHash(RoadLineMarkingDetailSettings detail, ref int hash)
        {
            if (detail == null)
            {
                hash = hash * 31;
                return;
            }

            hash = hash * 31 + (detail.wearMask != null ? detail.wearMask.GetInstanceID() : 0);
            AddFloatHash(detail.wearMaskStrength, ref hash);
            hash = hash * 31 + (int)detail.wearMaskTiling;
            AddFloatHash(detail.wearMaskTileLengthMeters, ref hash);
            hash = hash * 31 + (detail.invertWearMask ? 1 : 0);
            hash = hash * 31 + (detail.lineTexture != null ? detail.lineTexture.GetInstanceID() : 0);
            AddFloatHash(detail.lineTextureStrength, ref hash);
            AddFloatHash(detail.lineTextureTileLengthMeters, ref hash);
            AddFloatHash(detail.lineTextureColorInfluence, ref hash);
            AddFloatHash(detail.smoothness, ref hash);
            AddFloatHash(detail.wornSmoothness, ref hash);
        }

        private static void AddVector3Hash(Vector3 value, ref int hash)
        {
            AddFloatHash(value.x, ref hash);
            AddFloatHash(value.y, ref hash);
            AddFloatHash(value.z, ref hash);
        }

        private static void AddColorHash(Color value, ref int hash)
        {
            AddFloatHash(value.r, ref hash);
            AddFloatHash(value.g, ref hash);
            AddFloatHash(value.b, ref hash);
            AddFloatHash(value.a, ref hash);
        }

        private static void AddFloatHash(float value, ref int hash)
        {
            hash = hash * 31 + Mathf.RoundToInt(value * 10000f);
        }
    }
}
#endif

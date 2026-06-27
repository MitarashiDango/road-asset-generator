#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MitarashiDango.RoadAssetGenerator
{
    /// <summary>区画線メッシュ 1 チャンク分の生成結果。</summary>
    public sealed class RoadMarkingMeshData
    {
        public RoadMarkingMeshData(
            Mesh mesh,
            Color color,
            int boundaryIndex,
            int strokeIndex,
            float startDistanceMeters,
            float endDistanceMeters)
        {
            this.mesh = mesh;
            this.color = color;
            this.boundaryIndex = boundaryIndex;
            this.strokeIndex = strokeIndex;
            this.startDistanceMeters = startDistanceMeters;
            this.endDistanceMeters = endDistanceMeters;
        }

        public Mesh mesh { get; }
        public Color color { get; }
        public int boundaryIndex { get; }
        public int strokeIndex { get; }
        public float startDistanceMeters { get; }
        public float endDistanceMeters { get; }
    }

    /// <summary>RoadProfile の境界線定義から RoadSegment 用の区画線メッシュを構築する。</summary>
    public static class RoadMarkingMeshBuilder
    {
        private const int ArcLengthSamplesPerSegment = 48;
        private const float MinimumSampleLengthMeters = 0.25f;
        private const int MaxAdaptiveDepth = 12;
        private const float DistanceEpsilon = 0.001f;

        public static List<RoadMarkingMeshData> Build(RoadSegment segment, RoadNetwork network)
        {
            var results = new List<RoadMarkingMeshData>();
            if (segment == null || !segment.TryCreateSpline(out var spline))
            {
                return results;
            }

            var profile = segment.GetActiveProfile();
            if (profile == null || profile.TotalWidthMeters <= CatmullRomSpline.Epsilon)
            {
                return results;
            }

            var table = spline.BuildArcLengthTable(ArcLengthSamplesPerSegment);
            if (table.TotalLengthMeters <= CatmullRomSpline.Epsilon)
            {
                return results;
            }

            var boundaryOffsets = CalculateBoundaryOffsets(profile);
            if (profile.boundaryLines == null || boundaryOffsets.Count == 0)
            {
                return results;
            }

            var settings = RoadMarkingBuildSettings.From(network, segment);
            var distances = BuildAdaptiveDistances(table, settings);
            SortAndDedupe(distances);

            var meshIndex = 0;
            var boundaryCount = Mathf.Min(profile.boundaryLines.Count, boundaryOffsets.Count);
            for (var boundaryIndex = 0; boundaryIndex < boundaryCount; boundaryIndex++)
            {
                var boundaryLine = profile.boundaryLines[boundaryIndex];
                if (boundaryLine?.strokes == null)
                {
                    continue;
                }

                var strokeCount = Mathf.Min(2, boundaryLine.strokes.Count);
                for (var strokeIndex = 0; strokeIndex < strokeCount; strokeIndex++)
                {
                    var stroke = boundaryLine.strokes[strokeIndex];
                    if (!IsRenderable(stroke))
                    {
                        continue;
                    }

                    var centerOffset = boundaryOffsets[boundaryIndex] +
                        CalculateStrokeCenterOffset(boundaryLine, strokeIndex, strokeCount);
                    AppendStrokeMeshes(
                        results,
                        table,
                        distances,
                        stroke,
                        centerOffset,
                        boundaryIndex,
                        strokeIndex,
                        settings,
                        ref meshIndex);
                }
            }

            return results;
        }

        private static void AppendStrokeMeshes(
            List<RoadMarkingMeshData> results,
            CatmullRomArcLengthTable table,
            IReadOnlyList<float> distances,
            RoadLineStroke stroke,
            float centerOffsetMeters,
            int boundaryIndex,
            int strokeIndex,
            RoadMarkingBuildSettings settings,
            ref int meshIndex)
        {
            var spans = BuildStrokeSpans(stroke, table.TotalLengthMeters);
            if (spans.Count == 0)
            {
                return;
            }

            var splitLength = Mathf.Max(1f, settings.meshSegmentLengthMeters);
            for (var chunkStart = 0f; chunkStart < table.TotalLengthMeters - CatmullRomSpline.Epsilon; chunkStart += splitLength)
            {
                var chunkEnd = Mathf.Min(chunkStart + splitLength, table.TotalLengthMeters);
                var clippedSpans = ClipSpans(spans, chunkStart, chunkEnd);
                if (clippedSpans.Count == 0)
                {
                    continue;
                }

                AppendStrokeChunkMeshes(
                    results,
                    table,
                    distances,
                    clippedSpans,
                    stroke,
                    centerOffsetMeters,
                    boundaryIndex,
                    strokeIndex,
                    settings,
                    ref meshIndex);
            }
        }

        private static void AppendStrokeChunkMeshes(
            List<RoadMarkingMeshData> results,
            CatmullRomArcLengthTable table,
            IReadOnlyList<float> distances,
            IReadOnlyList<Vector2> spans,
            RoadLineStroke stroke,
            float centerOffsetMeters,
            int boundaryIndex,
            int strokeIndex,
            RoadMarkingBuildSettings settings,
            ref int meshIndex)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var tangents = new List<Vector4>();
            var uvs = new List<Vector2>();
            var colors = new List<Color>();
            var triangles = new List<int>();
            var chunkStart = float.PositiveInfinity;
            var chunkEnd = 0f;

            foreach (var span in spans)
            {
                var rows = BuildRowsForSpan(distances, span.x, span.y);
                if (rows.Count < 2)
                {
                    continue;
                }

                var rowStart = 0;
                while (rowStart < rows.Count - 1)
                {
                    var availableRows = (RoadSurfaceMeshBuilder.MaxVerticesPerMesh - vertices.Count) / 2;
                    if (availableRows < 2)
                    {
                        AddMesh(
                            results,
                            vertices,
                            normals,
                            tangents,
                            uvs,
                            colors,
                            triangles,
                            stroke.color,
                            boundaryIndex,
                            strokeIndex,
                            chunkStart,
                            chunkEnd,
                            ref meshIndex);
                        ClearMeshBuffers(vertices, normals, tangents, uvs, colors, triangles);
                        chunkStart = float.PositiveInfinity;
                        chunkEnd = 0f;
                        continue;
                    }

                    var rowCount = Mathf.Min(availableRows, rows.Count - rowStart);
                    if (rowCount < 2)
                    {
                        break;
                    }

                    var slice = rows.GetRange(rowStart, rowCount);
                    AppendSpan(
                        vertices,
                        normals,
                        tangents,
                        uvs,
                        colors,
                        triangles,
                        table,
                        slice,
                        stroke,
                        centerOffsetMeters,
                        settings);
                    chunkStart = Mathf.Min(chunkStart, slice[0]);
                    chunkEnd = Mathf.Max(chunkEnd, slice[slice.Count - 1]);

                    if (rowStart + rowCount >= rows.Count)
                    {
                        break;
                    }

                    rowStart += rowCount - 1;
                }
            }

            AddMesh(
                results,
                vertices,
                normals,
                tangents,
                uvs,
                colors,
                triangles,
                stroke.color,
                boundaryIndex,
                strokeIndex,
                chunkStart,
                chunkEnd,
                ref meshIndex);
        }

        private static void ClearMeshBuffers(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector4> tangents,
            List<Vector2> uvs,
            List<Color> colors,
            List<int> triangles)
        {
            vertices.Clear();
            normals.Clear();
            tangents.Clear();
            uvs.Clear();
            colors.Clear();
            triangles.Clear();
        }

        private static void AppendSpan(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector4> tangents,
            List<Vector2> uvs,
            List<Color> colors,
            List<int> triangles,
            CatmullRomArcLengthTable table,
            IReadOnlyList<float> rows,
            RoadLineStroke stroke,
            float centerOffsetMeters,
            RoadMarkingBuildSettings settings)
        {
            var baseVertex = vertices.Count;
            var halfWidth = Mathf.Max(0.001f, stroke.widthMeters) * 0.5f;
            for (var row = 0; row < rows.Count; row++)
            {
                var distance = rows[row];
                var sample = table.SampleByDistance(
                    distance,
                    settings.referenceUp,
                    settings.fallbackForward,
                    settings.fallbackRight);
                var frame = sample.frame;
                var normal = frame.up.sqrMagnitude > CatmullRomSpline.Epsilon
                    ? frame.up.normalized
                    : Vector3.up;
                var tangent = new Vector4(frame.right.x, frame.right.y, frame.right.z, -1f);
                var center = frame.position +
                    frame.right * centerOffsetMeters +
                    normal * settings.markingVertexOffsetMeters;

                vertices.Add(center - frame.right * halfWidth);
                vertices.Add(center + frame.right * halfWidth);
                normals.Add(normal);
                normals.Add(normal);
                tangents.Add(tangent);
                tangents.Add(tangent);
                uvs.Add(new Vector2(0f, distance));
                uvs.Add(new Vector2(1f, distance));
                colors.Add(stroke.color);
                colors.Add(stroke.color);
            }

            for (var row = 0; row < rows.Count - 1; row++)
            {
                var currentLeft = baseVertex + row * 2;
                var currentRight = currentLeft + 1;
                var nextLeft = currentLeft + 2;
                var nextRight = nextLeft + 1;

                triangles.Add(currentLeft);
                triangles.Add(nextLeft);
                triangles.Add(currentRight);
                triangles.Add(currentRight);
                triangles.Add(nextLeft);
                triangles.Add(nextRight);
            }
        }

        private static void AddMesh(
            List<RoadMarkingMeshData> results,
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector4> tangents,
            List<Vector2> uvs,
            List<Color> colors,
            List<int> triangles,
            Color color,
            int boundaryIndex,
            int strokeIndex,
            float startDistanceMeters,
            float endDistanceMeters,
            ref int meshIndex)
        {
            if (vertices.Count == 0 || triangles.Count == 0)
            {
                return;
            }

            var mesh = new Mesh
            {
                name = $"RoadMarking_{meshIndex:000}",
                indexFormat = IndexFormat.UInt16,
            };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTangents(tangents);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0, true);
            GenerateLightmapUvs(mesh, uvs, startDistanceMeters, endDistanceMeters);
            mesh.RecalculateBounds();

            results.Add(new RoadMarkingMeshData(
                mesh,
                color,
                boundaryIndex,
                strokeIndex,
                startDistanceMeters,
                endDistanceMeters));
            meshIndex++;
        }

        private static void GenerateLightmapUvs(
            Mesh mesh,
            IReadOnlyList<Vector2> uvs,
            float startDistanceMeters,
            float endDistanceMeters)
        {
            var unwrapParam = new UnwrapParam();
            UnwrapParam.SetDefaults(out unwrapParam);
            if (!Unwrapping.GenerateSecondaryUVSet(mesh, unwrapParam))
            {
                mesh.SetUVs(1, BuildFallbackLightmapUvs(uvs, startDistanceMeters, endDistanceMeters));
            }
        }

        private static List<Vector2> BuildFallbackLightmapUvs(
            IReadOnlyList<Vector2> uvs,
            float startDistanceMeters,
            float endDistanceMeters)
        {
            var lightmapUvs = new List<Vector2>(uvs.Count);
            var start = startDistanceMeters;
            var end = endDistanceMeters;
            if (float.IsInfinity(start) || float.IsInfinity(end) || end <= start)
            {
                start = float.PositiveInfinity;
                end = 0f;
                foreach (var uv in uvs)
                {
                    start = Mathf.Min(start, uv.y);
                    end = Mathf.Max(end, uv.y);
                }
            }

            var length = Mathf.Max(DistanceEpsilon, end - start);
            foreach (var uv in uvs)
            {
                lightmapUvs.Add(new Vector2(
                    Mathf.Clamp01(uv.x),
                    Mathf.Clamp01((uv.y - start) / length)));
            }

            return lightmapUvs;
        }

        private static List<float> BuildAdaptiveDistances(
            CatmullRomArcLengthTable table,
            RoadMarkingBuildSettings settings)
        {
            var distances = new List<float> { 0f };
            var cursor = 0f;
            var maxLength = Mathf.Max(MinimumSampleLengthMeters, settings.maxSampleLengthMeters);

            while (cursor < table.TotalLengthMeters - CatmullRomSpline.Epsilon)
            {
                var next = Mathf.Min(cursor + maxLength, table.TotalLengthMeters);
                AddRefinedDistanceSpan(
                    distances,
                    table,
                    cursor,
                    next,
                    settings.maxSampleAngleDegrees,
                    Mathf.Min(MinimumSampleLengthMeters, maxLength),
                    0);
                cursor = next;
            }

            return distances;
        }

        private static void AddRefinedDistanceSpan(
            List<float> distances,
            CatmullRomArcLengthTable table,
            float startDistance,
            float endDistance,
            float maxAngleDegrees,
            float minLength,
            int depth)
        {
            var span = endDistance - startDistance;
            if (span <= CatmullRomSpline.Epsilon)
            {
                return;
            }

            var start = table.SampleByDistance(startDistance);
            var mid = table.SampleByDistance((startDistance + endDistance) * 0.5f);
            var end = table.SampleByDistance(endDistance);
            var maxAngle = Mathf.Max(
                Vector3.Angle(start.tangent, mid.tangent),
                Vector3.Angle(mid.tangent, end.tangent));

            if (maxAngle > maxAngleDegrees && span > minLength && depth < MaxAdaptiveDepth)
            {
                var midpoint = (startDistance + endDistance) * 0.5f;
                AddRefinedDistanceSpan(distances, table, startDistance, midpoint, maxAngleDegrees, minLength, depth + 1);
                AddRefinedDistanceSpan(distances, table, midpoint, endDistance, maxAngleDegrees, minLength, depth + 1);
                return;
            }

            distances.Add(endDistance);
        }

        private static List<float> BuildRowsForSpan(IReadOnlyList<float> distances, float start, float end)
        {
            var rows = new List<float> { start };
            foreach (var distance in distances)
            {
                if (distance > start + DistanceEpsilon && distance < end - DistanceEpsilon)
                {
                    rows.Add(distance);
                }
            }
            rows.Add(end);
            SortAndDedupe(rows);
            return rows;
        }

        private static List<Vector2> BuildStrokeSpans(RoadLineStroke stroke, float totalLengthMeters)
        {
            var spans = new List<Vector2>();
            if (stroke == null || totalLengthMeters <= CatmullRomSpline.Epsilon)
            {
                return spans;
            }

            if (stroke.kind == RoadLineKind.Solid)
            {
                spans.Add(new Vector2(0f, totalLengthMeters));
                return spans;
            }

            if (stroke.kind != RoadLineKind.Dashed)
            {
                return spans;
            }

            var dashLength = Mathf.Max(0.05f, stroke.dashLengthMeters);
            var dashGap = Mathf.Max(0.05f, stroke.dashGapMeters);
            for (var start = 0f; start < totalLengthMeters - CatmullRomSpline.Epsilon; start += dashLength + dashGap)
            {
                var end = Mathf.Min(start + dashLength, totalLengthMeters);
                if (end - start > DistanceEpsilon)
                {
                    spans.Add(new Vector2(start, end));
                }
            }

            return spans;
        }

        private static List<Vector2> ClipSpans(IReadOnlyList<Vector2> spans, float clipStart, float clipEnd)
        {
            var clipped = new List<Vector2>();
            foreach (var span in spans)
            {
                var start = Mathf.Max(span.x, clipStart);
                var end = Mathf.Min(span.y, clipEnd);
                if (end - start > DistanceEpsilon)
                {
                    clipped.Add(new Vector2(start, end));
                }
            }

            return clipped;
        }

        private static List<float> CalculateBoundaryOffsets(RoadProfile profile)
        {
            var offsets = new List<float>();
            if (profile == null)
            {
                return offsets;
            }

            var halfWidth = profile.TotalWidthMeters * 0.5f;
            var cursor = -halfWidth + Mathf.Max(0f, profile.leftShoulderWidthMeters);
            offsets.Add(cursor);

            if (profile.lanes == null)
            {
                return offsets;
            }

            foreach (var lane in profile.lanes)
            {
                cursor += Mathf.Max(0f, lane?.widthMeters ?? 0f);
                offsets.Add(cursor);
            }

            return offsets;
        }

        private static float CalculateStrokeCenterOffset(RoadBoundaryLine boundaryLine, int strokeIndex, int strokeCount)
        {
            if (boundaryLine?.strokes == null || strokeCount <= 1)
            {
                return 0f;
            }

            var spacing = Mathf.Max(0f, boundaryLine.strokeSpacingMeters);
            if (strokeIndex == 0)
            {
                var width = Mathf.Max(0f, boundaryLine.strokes[0]?.widthMeters ?? 0f);
                return -(spacing * 0.5f + width * 0.5f);
            }

            var rightWidth = Mathf.Max(0f, boundaryLine.strokes[strokeIndex]?.widthMeters ?? 0f);
            return spacing * 0.5f + rightWidth * 0.5f;
        }

        private static bool IsRenderable(RoadLineStroke stroke)
        {
            return stroke != null &&
                stroke.kind != RoadLineKind.None &&
                stroke.widthMeters > CatmullRomSpline.Epsilon;
        }

        private static void SortAndDedupe(List<float> distances)
        {
            distances.Sort();
            for (var i = distances.Count - 1; i > 0; i--)
            {
                if (Mathf.Abs(distances[i] - distances[i - 1]) <= DistanceEpsilon)
                {
                    distances.RemoveAt(i);
                }
            }
        }

        private readonly struct RoadMarkingBuildSettings
        {
            public readonly float meshSegmentLengthMeters;
            public readonly float markingVertexOffsetMeters;
            public readonly float maxSampleLengthMeters;
            public readonly float maxSampleAngleDegrees;
            public readonly Vector3 referenceUp;
            public readonly Vector3 fallbackForward;
            public readonly Vector3 fallbackRight;

            private RoadMarkingBuildSettings(
                float meshSegmentLengthMeters,
                float markingVertexOffsetMeters,
                float maxSampleLengthMeters,
                float maxSampleAngleDegrees,
                Vector3 referenceUp,
                Vector3 fallbackForward,
                Vector3 fallbackRight)
            {
                this.meshSegmentLengthMeters = Mathf.Max(1f, meshSegmentLengthMeters);
                this.markingVertexOffsetMeters = Mathf.Max(0f, markingVertexOffsetMeters);
                this.maxSampleLengthMeters = Mathf.Max(MinimumSampleLengthMeters, maxSampleLengthMeters);
                this.maxSampleAngleDegrees = Mathf.Clamp(maxSampleAngleDegrees, 1f, 45f);
                this.referenceUp = NormalizeOrFallback(referenceUp, Vector3.up);
                this.fallbackForward = NormalizeOrFallback(fallbackForward, Vector3.forward);
                this.fallbackRight = NormalizeOrFallback(fallbackRight, Vector3.right);
            }

            public static RoadMarkingBuildSettings From(RoadNetwork network, RoadSegment segment)
            {
                var referenceUp = Vector3.up;
                var fallbackForward = Vector3.forward;
                var fallbackRight = Vector3.right;
                if (segment != null)
                {
                    referenceUp = segment.transform.InverseTransformDirection(Vector3.up);
                    fallbackForward = segment.transform.InverseTransformDirection(Vector3.forward);
                    fallbackRight = segment.transform.InverseTransformDirection(Vector3.right);
                }

                var meshSegmentLengthMeters = network != null ? network.meshSegmentLengthMeters : 100f;
                var markingVertexOffsetMeters = network != null ? network.markingVertexOffsetMeters : 0.005f;
                var maxSampleLengthMeters = network != null ? network.maxSurfaceSampleLengthMeters : 1f;
                var maxSampleAngleDegrees = network != null ? network.maxSurfaceSampleAngleDegrees : 4f;
                if (segment != null && segment.overrideSurfaceSamplingSettings)
                {
                    maxSampleLengthMeters = segment.maxSurfaceSampleLengthMeters;
                    maxSampleAngleDegrees = segment.maxSurfaceSampleAngleDegrees;
                }

                return new RoadMarkingBuildSettings(
                    meshSegmentLengthMeters,
                    markingVertexOffsetMeters,
                    maxSampleLengthMeters,
                    maxSampleAngleDegrees,
                    referenceUp,
                    fallbackForward,
                    fallbackRight);
            }

            private static Vector3 NormalizeOrFallback(Vector3 value, Vector3 fallback)
            {
                return value.sqrMagnitude > CatmullRomSpline.Epsilon * CatmullRomSpline.Epsilon ? value.normalized : fallback;
            }
        }
    }
}
#endif

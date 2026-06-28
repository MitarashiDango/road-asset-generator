#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MitarashiDango.RoadAssetGenerator
{
    /// <summary>路面メッシュ 1 チャンク分の生成結果。</summary>
    public sealed class RoadSurfaceMeshData
    {
        public RoadSurfaceMeshData(Mesh mesh, float startDistanceMeters, float endDistanceMeters)
        {
            this.mesh = mesh;
            this.startDistanceMeters = startDistanceMeters;
            this.endDistanceMeters = endDistanceMeters;
        }

        public Mesh mesh { get; }
        public float startDistanceMeters { get; }
        public float endDistanceMeters { get; }
    }

    /// <summary>RoadSegment の路面リボンメッシュを構築する。</summary>
    public static class RoadSurfaceMeshBuilder
    {
        public const int MaxVerticesPerMesh = 65534;

        private const int ArcLengthSamplesPerSegment = 48;
        private const float MinimumSampleLengthMeters = RoadSurfaceSamplingSettings.MinimumSampleLengthMeters;
        private const int MaxAdaptiveDepth = 12;
        private const int MaxColumnsPerMesh = (MaxVerticesPerMesh / 2) - 1;

        public static List<RoadSurfaceMeshData> Build(RoadSegment segment, RoadNetwork network)
        {
            var results = new List<RoadSurfaceMeshData>();
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

            var settings = RoadSurfaceBuildSettings.From(network, segment);
            var distances = BuildAdaptiveDistances(table, settings);
            AddSplitBoundaries(distances, table.TotalLengthMeters, settings.meshSegmentLengthMeters);
            SortAndDedupe(distances);
            BuildChunks(results, distances, table, profile.TotalWidthMeters, settings);
            return results;
        }

        private static List<float> BuildAdaptiveDistances(
            CatmullRomArcLengthTable table,
            RoadSurfaceBuildSettings settings)
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

        private static void AddSplitBoundaries(List<float> distances, float totalLengthMeters, float meshSegmentLengthMeters)
        {
            var splitLength = Mathf.Max(1f, meshSegmentLengthMeters);
            for (var distance = splitLength; distance < totalLengthMeters - CatmullRomSpline.Epsilon; distance += splitLength)
            {
                distances.Add(distance);
            }
        }

        private static void SortAndDedupe(List<float> distances)
        {
            distances.Sort();
            for (var i = distances.Count - 1; i > 0; i--)
            {
                if (Mathf.Abs(distances[i] - distances[i - 1]) <= 0.001f)
                {
                    distances.RemoveAt(i);
                }
            }
        }

        private static void BuildChunks(
            List<RoadSurfaceMeshData> results,
            List<float> distances,
            CatmullRomArcLengthTable table,
            float totalWidthMeters,
            RoadSurfaceBuildSettings settings)
        {
            var splitLength = Mathf.Max(1f, settings.meshSegmentLengthMeters);
            var chunkStartDistance = 0f;
            var chunkRows = new List<float>();
            var meshIndex = 0;

            for (var i = 0; i < distances.Count; i++)
            {
                var distance = distances[i];
                if (chunkRows.Count == 0)
                {
                    chunkStartDistance = distance;
                }

                chunkRows.Add(distance);
                var reachedSplit = distance >= chunkStartDistance + splitLength - 0.001f;
                var reachedEnd = i == distances.Count - 1;
                if ((reachedSplit || reachedEnd) && chunkRows.Count >= 2)
                {
                    AppendChunkMeshes(results, chunkRows, table, totalWidthMeters, settings, ref meshIndex);
                    chunkRows.Clear();
                    chunkRows.Add(distance);
                    chunkStartDistance = distance;
                }
            }
        }

        private static void AppendChunkMeshes(
            List<RoadSurfaceMeshData> results,
            List<float> rows,
            CatmullRomArcLengthTable table,
            float totalWidthMeters,
            RoadSurfaceBuildSettings settings,
            ref int meshIndex)
        {
            var columnCount = CalculateColumnCount(totalWidthMeters, settings.maxColumnWidthMeters);
            var verticesPerRow = columnCount + 1;
            var maxRowsPerMesh = Mathf.Max(2, MaxVerticesPerMesh / verticesPerRow);
            var startIndex = 0;
            while (startIndex < rows.Count - 1)
            {
                var rowCount = Mathf.Min(maxRowsPerMesh, rows.Count - startIndex);
                if (rowCount < 2)
                {
                    break;
                }

                var slice = rows.GetRange(startIndex, rowCount);
                var mesh = BuildMesh(slice, table, totalWidthMeters, columnCount, settings);
                mesh.name = $"RoadSurface_{meshIndex:000}";
                results.Add(new RoadSurfaceMeshData(mesh, slice[0], slice[slice.Count - 1]));
                meshIndex++;

                startIndex += rowCount - 1;
            }
        }

        private static Mesh BuildMesh(
            IReadOnlyList<float> distances,
            CatmullRomArcLengthTable table,
            float totalWidthMeters,
            int columnCount,
            RoadSurfaceBuildSettings settings)
        {
            var halfWidth = totalWidthMeters * 0.5f;
            var verticesPerRow = columnCount + 1;
            var vertexCount = distances.Count * verticesPerRow;
            var vertices = new Vector3[vertexCount];
            var normals = new Vector3[vertexCount];
            var tangents = new Vector4[vertexCount];
            var uvs = new Vector2[vertexCount];
            var triangles = new int[(distances.Count - 1) * columnCount * 6];
            var textureLength = Mathf.Max(0.1f, settings.textureLengthMeters);

            for (var row = 0; row < distances.Count; row++)
            {
                var sample = table.SampleByDistance(
                    distances[row],
                    settings.referenceUp,
                    settings.fallbackForward,
                    settings.fallbackRight);
                var frame = sample.frame;
                var normal = Vector3.Cross(frame.tangent, frame.right).normalized;
                if (normal.sqrMagnitude <= CatmullRomSpline.Epsilon)
                {
                    normal = frame.up;
                }

                var rowStart = row * verticesPerRow;
                var tangent = new Vector4(frame.right.x, frame.right.y, frame.right.z, -1f);
                var v = distances[row] / textureLength;
                for (var column = 0; column <= columnCount; column++)
                {
                    var widthT = column / (float)columnCount;
                    var lateral = Mathf.Lerp(-halfWidth, halfWidth, widthT);
                    var index = rowStart + column;
                    vertices[index] = frame.position + frame.right * lateral;
                    normals[index] = normal;
                    tangents[index] = tangent;
                    uvs[index] = new Vector2(widthT, v);
                }
            }

            var tri = 0;
            for (var row = 0; row < distances.Count - 1; row++)
            {
                var currentRowStart = row * verticesPerRow;
                var nextRowStart = currentRowStart + verticesPerRow;
                for (var column = 0; column < columnCount; column++)
                {
                    var currentLeft = currentRowStart + column;
                    var currentRight = currentLeft + 1;
                    var nextLeft = nextRowStart + column;
                    var nextRight = nextLeft + 1;

                    triangles[tri++] = currentLeft;
                    triangles[tri++] = nextLeft;
                    triangles[tri++] = currentRight;
                    triangles[tri++] = currentRight;
                    triangles[tri++] = nextLeft;
                    triangles[tri++] = nextRight;
                }
            }

            var mesh = new Mesh
            {
                indexFormat = IndexFormat.UInt16,
            };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTangents(tangents);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0, true);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static int CalculateColumnCount(float totalWidthMeters, float maxColumnWidthMeters)
        {
            var columnWidth = Mathf.Max(RoadSurfaceSamplingSettings.MinimumColumnWidthMeters, maxColumnWidthMeters);
            var requestedColumns = Mathf.CeilToInt(totalWidthMeters / columnWidth);
            return Mathf.Clamp(requestedColumns, 1, MaxColumnsPerMesh);
        }

        private readonly struct RoadSurfaceBuildSettings
        {
            public readonly float textureLengthMeters;
            public readonly float meshSegmentLengthMeters;
            public readonly float maxSampleLengthMeters;
            public readonly float maxSampleAngleDegrees;
            public readonly float maxColumnWidthMeters;
            public readonly Vector3 referenceUp;
            public readonly Vector3 fallbackForward;
            public readonly Vector3 fallbackRight;

            private RoadSurfaceBuildSettings(
                float textureLengthMeters,
                float meshSegmentLengthMeters,
                float maxSampleLengthMeters,
                float maxSampleAngleDegrees,
                float maxColumnWidthMeters,
                Vector3 referenceUp,
                Vector3 fallbackForward,
                Vector3 fallbackRight)
            {
                this.textureLengthMeters = Mathf.Max(0.1f, textureLengthMeters);
                this.meshSegmentLengthMeters = Mathf.Max(1f, meshSegmentLengthMeters);
                this.maxSampleLengthMeters = Mathf.Max(MinimumSampleLengthMeters, maxSampleLengthMeters);
                this.maxSampleAngleDegrees = Mathf.Clamp(
                    maxSampleAngleDegrees,
                    RoadSurfaceSamplingSettings.MinimumSampleAngleDegrees,
                    RoadSurfaceSamplingSettings.MaximumSampleAngleDegrees);
                this.maxColumnWidthMeters = Mathf.Max(RoadSurfaceSamplingSettings.MinimumColumnWidthMeters, maxColumnWidthMeters);
                this.referenceUp = NormalizeOrFallback(referenceUp, Vector3.up);
                this.fallbackForward = NormalizeOrFallback(fallbackForward, Vector3.forward);
                this.fallbackRight = NormalizeOrFallback(fallbackRight, Vector3.right);
            }

            public static RoadSurfaceBuildSettings From(RoadNetwork network, RoadSegment segment)
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

                var textureLengthMeters = RoadSurfaceStyle.ResolveTextureLengthMeters(segment, network);
                var meshSegmentLengthMeters = network != null ? network.meshSegmentLengthMeters : 100f;
                var maxSampleLengthMeters = RoadSurfaceSamplingSettings.ResolveMaxSampleLengthMeters(segment, network);
                var maxSampleAngleDegrees = RoadSurfaceSamplingSettings.ResolveMaxSampleAngleDegrees(segment, network);
                var maxColumnWidthMeters = RoadSurfaceSamplingSettings.ResolveMaxColumnWidthMeters(segment, network);

                return new RoadSurfaceBuildSettings(
                    textureLengthMeters,
                    meshSegmentLengthMeters,
                    maxSampleLengthMeters,
                    maxSampleAngleDegrees,
                    maxColumnWidthMeters,
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

using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MitarashiDango.RoadAssetGenerator.Tests
{
    public class RoadSurfaceMeshBuilderTests
    {
        [Test]
        public void RoadNetworkDefaultsUseFinerSurfaceSampling()
        {
            var networkObject = new GameObject("RoadNetwork_Default_Sampling_Test");

            try
            {
                var network = networkObject.AddComponent<RoadNetwork>();

                Assert.That(network.maxSurfaceSampleLengthMeters, Is.EqualTo(1f).Within(0.001f));
                Assert.That(network.maxSurfaceSampleAngleDegrees, Is.EqualTo(4f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(networkObject);
            }
        }

        [Test]
        public void StraightSurfaceUsesProfileWidthAndTextureLengthUv()
        {
            var networkObject = new GameObject("RoadNetwork_Surface_Test");
            var segmentObject = new GameObject("RoadSegment_Surface_Test");
            var meshes = new List<RoadSurfaceMeshData>();

            try
            {
                var network = networkObject.AddComponent<RoadNetwork>();
                network.textureLengthMeters = 10f;
                network.meshSegmentLengthMeters = 100f;
                network.maxSurfaceSampleLengthMeters = 5f;

                segmentObject.transform.SetParent(networkObject.transform, false);
                var segment = segmentObject.AddComponent<RoadSegment>();
                segment.controlPoints = new[]
                {
                    new SplinePoint(new Vector3(0f, 0f, 0f)),
                    new SplinePoint(new Vector3(0f, 0f, 20f)),
                };
                segment.profileKeys = new[]
                {
                    new RoadProfileKey { profile = RoadProfile.CreateDefaultTwoLane() },
                };

                meshes = RoadSurfaceMeshBuilder.Build(segment, network);

                Assert.That(meshes, Has.Count.EqualTo(1));
                var mesh = meshes[0].mesh;
                var vertices = mesh.vertices;
                var uvs = mesh.uv;

                Assert.That(vertices[0].x, Is.EqualTo(-3.75f).Within(0.001f));
                Assert.That(vertices[1].x, Is.EqualTo(3.75f).Within(0.001f));
                Assert.That(uvs[0].x, Is.EqualTo(0f).Within(0.001f));
                Assert.That(uvs[0].y, Is.EqualTo(0f).Within(0.001f));
                Assert.That(uvs[1].x, Is.EqualTo(1f).Within(0.001f));
                Assert.That(uvs[1].y, Is.EqualTo(0f).Within(0.001f));
                Assert.That(uvs[uvs.Length - 1].y, Is.EqualTo(2f).Within(0.001f));

                foreach (var normal in mesh.normals)
                {
                    Assert.That(Vector3.Dot(normal, Vector3.up), Is.GreaterThan(0.999f));
                }

                foreach (var tangent in mesh.tangents)
                {
                    var tangentDirection = new Vector3(tangent.x, tangent.y, tangent.z);
                    Assert.That(Vector3.Dot(tangentDirection, Vector3.right), Is.GreaterThan(0.999f));
                    Assert.That(tangent.w, Is.EqualTo(-1f).Within(0.001f));
                }
            }
            finally
            {
                DestroyMeshes(meshes);
                Object.DestroyImmediate(networkObject);
            }
        }

        [Test]
        public void LongSurfaceSplitsAtConfiguredLength()
        {
            var networkObject = new GameObject("RoadNetwork_Split_Test");
            var segmentObject = new GameObject("RoadSegment_Split_Test");
            var meshes = new List<RoadSurfaceMeshData>();

            try
            {
                var network = networkObject.AddComponent<RoadNetwork>();
                network.textureLengthMeters = 10f;
                network.meshSegmentLengthMeters = 10f;
                network.maxSurfaceSampleLengthMeters = 20f;

                segmentObject.transform.SetParent(networkObject.transform, false);
                var segment = segmentObject.AddComponent<RoadSegment>();
                segment.controlPoints = new[]
                {
                    new SplinePoint(new Vector3(0f, 0f, 0f)),
                    new SplinePoint(new Vector3(0f, 0f, 25f)),
                };
                segment.profileKeys = new[]
                {
                    new RoadProfileKey { profile = RoadProfile.CreateDefaultTwoLane() },
                };

                meshes = RoadSurfaceMeshBuilder.Build(segment, network);

                Assert.That(meshes, Has.Count.EqualTo(3));
                Assert.That(meshes[0].startDistanceMeters, Is.EqualTo(0f).Within(0.001f));
                Assert.That(meshes[0].endDistanceMeters, Is.EqualTo(10f).Within(0.001f));
                Assert.That(meshes[1].endDistanceMeters, Is.EqualTo(20f).Within(0.001f));
                Assert.That(meshes[2].endDistanceMeters, Is.EqualTo(25f).Within(0.001f));
                foreach (var meshData in meshes)
                {
                    Assert.That(meshData.mesh.vertexCount, Is.LessThanOrEqualTo(RoadSurfaceMeshBuilder.MaxVerticesPerMesh));
                }
            }
            finally
            {
                DestroyMeshes(meshes);
                Object.DestroyImmediate(networkObject);
            }
        }

        [Test]
        public void DenseSamplesDoNotCreateExtraChunksBeforeNextSplit()
        {
            var networkObject = new GameObject("RoadNetwork_Dense_Split_Test");
            var segmentObject = new GameObject("RoadSegment_Dense_Split_Test");
            var meshes = new List<RoadSurfaceMeshData>();

            try
            {
                var network = networkObject.AddComponent<RoadNetwork>();
                network.textureLengthMeters = 10f;
                network.meshSegmentLengthMeters = 10f;
                network.maxSurfaceSampleLengthMeters = 2f;

                segmentObject.transform.SetParent(networkObject.transform, false);
                var segment = segmentObject.AddComponent<RoadSegment>();
                segment.controlPoints = new[]
                {
                    new SplinePoint(new Vector3(0f, 0f, 0f)),
                    new SplinePoint(new Vector3(0f, 0f, 25f)),
                };
                segment.profileKeys = new[]
                {
                    new RoadProfileKey { profile = RoadProfile.CreateDefaultTwoLane() },
                };

                meshes = RoadSurfaceMeshBuilder.Build(segment, network);

                Assert.That(meshes, Has.Count.EqualTo(3));
                Assert.That(meshes[0].endDistanceMeters, Is.EqualTo(10f).Within(0.001f));
                Assert.That(meshes[1].startDistanceMeters, Is.EqualTo(10f).Within(0.001f));
                Assert.That(meshes[1].endDistanceMeters, Is.EqualTo(20f).Within(0.001f));
                Assert.That(meshes[2].startDistanceMeters, Is.EqualTo(20f).Within(0.001f));
                Assert.That(meshes[2].endDistanceMeters, Is.EqualTo(25f).Within(0.001f));
            }
            finally
            {
                DestroyMeshes(meshes);
                Object.DestroyImmediate(networkObject);
            }
        }

        [Test]
        public void CurvedSurfaceUsesAdaptiveSampling()
        {
            var networkObject = new GameObject("RoadNetwork_Curve_Test");
            var segmentObject = new GameObject("RoadSegment_Curve_Test");
            var meshes = new List<RoadSurfaceMeshData>();

            try
            {
                var network = networkObject.AddComponent<RoadNetwork>();
                network.meshSegmentLengthMeters = 100f;
                network.maxSurfaceSampleLengthMeters = 20f;
                network.maxSurfaceSampleAngleDegrees = 4f;

                segmentObject.transform.SetParent(networkObject.transform, false);
                var segment = segmentObject.AddComponent<RoadSegment>();
                segment.controlPoints = new[]
                {
                    new SplinePoint(new Vector3(0f, 0f, 0f)),
                    new SplinePoint(new Vector3(0f, 0f, 15f)),
                    new SplinePoint(new Vector3(15f, 0f, 30f)),
                    new SplinePoint(new Vector3(30f, 0f, 30f)),
                };
                segment.profileKeys = new[]
                {
                    new RoadProfileKey { profile = RoadProfile.CreateDefaultTwoLane() },
                };

                meshes = RoadSurfaceMeshBuilder.Build(segment, network);

                Assert.That(meshes, Has.Count.EqualTo(1));
                Assert.That(meshes[0].mesh.vertexCount, Is.GreaterThan(6));
            }
            finally
            {
                DestroyMeshes(meshes);
                Object.DestroyImmediate(networkObject);
            }
        }

        [Test]
        public void SegmentSurfaceSamplingOverrideTakesPriorityOverNetworkSettings()
        {
            var networkObject = new GameObject("RoadNetwork_Sampling_Override_Test");
            var segmentObject = new GameObject("RoadSegment_Sampling_Override_Test");
            var coarseMeshes = new List<RoadSurfaceMeshData>();
            var fineMeshes = new List<RoadSurfaceMeshData>();

            try
            {
                var network = networkObject.AddComponent<RoadNetwork>();
                network.meshSegmentLengthMeters = 100f;
                network.maxSurfaceSampleLengthMeters = 20f;
                network.maxSurfaceSampleAngleDegrees = 45f;

                segmentObject.transform.SetParent(networkObject.transform, false);
                var segment = segmentObject.AddComponent<RoadSegment>();
                segment.controlPoints = new[]
                {
                    new SplinePoint(new Vector3(0f, 0f, 0f)),
                    new SplinePoint(new Vector3(0f, 0f, 15f)),
                    new SplinePoint(new Vector3(15f, 0f, 30f)),
                    new SplinePoint(new Vector3(30f, 0f, 30f)),
                };
                segment.profileKeys = new[]
                {
                    new RoadProfileKey { profile = RoadProfile.CreateDefaultTwoLane() },
                };

                coarseMeshes = RoadSurfaceMeshBuilder.Build(segment, network);

                segment.overrideSurfaceSamplingSettings = true;
                segment.maxSurfaceSampleLengthMeters = 1f;
                segment.maxSurfaceSampleAngleDegrees = 4f;
                fineMeshes = RoadSurfaceMeshBuilder.Build(segment, network);

                Assert.That(coarseMeshes, Has.Count.EqualTo(1));
                Assert.That(fineMeshes, Has.Count.EqualTo(1));
                Assert.That(fineMeshes[0].mesh.vertexCount, Is.GreaterThan(coarseMeshes[0].mesh.vertexCount));
            }
            finally
            {
                DestroyMeshes(coarseMeshes);
                DestroyMeshes(fineMeshes);
                Object.DestroyImmediate(networkObject);
            }
        }

        [Test]
        public void RotatedSegmentStillBuildsWidthAndNormalsFromWorldUp()
        {
            var networkObject = new GameObject("RoadNetwork_Rotated_Test");
            var segmentObject = new GameObject("RoadSegment_Rotated_Test");
            var meshes = new List<RoadSurfaceMeshData>();

            try
            {
                var network = networkObject.AddComponent<RoadNetwork>();
                segmentObject.transform.SetParent(networkObject.transform, false);
                segmentObject.transform.rotation = Quaternion.Euler(0f, 0f, 90f);

                var segment = segmentObject.AddComponent<RoadSegment>();
                segment.controlPoints = new[]
                {
                    new SplinePoint(new Vector3(0f, 0f, 0f)),
                    new SplinePoint(new Vector3(0f, 0f, 20f)),
                };
                segment.profileKeys = new[]
                {
                    new RoadProfileKey { profile = RoadProfile.CreateDefaultTwoLane() },
                };

                meshes = RoadSurfaceMeshBuilder.Build(segment, network);

                Assert.That(meshes, Has.Count.EqualTo(1));
                var mesh = meshes[0].mesh;
                var leftWorld = segmentObject.transform.TransformPoint(mesh.vertices[0]);
                var rightWorld = segmentObject.transform.TransformPoint(mesh.vertices[1]);
                var normalWorld = segmentObject.transform.TransformDirection(mesh.normals[0]);

                Assert.That(leftWorld.y, Is.EqualTo(0f).Within(0.001f));
                Assert.That(rightWorld.y, Is.EqualTo(0f).Within(0.001f));
                Assert.That(Vector3.Distance(leftWorld, rightWorld), Is.EqualTo(7.5f).Within(0.001f));
                Assert.That(Vector3.Dot(normalWorld.normalized, Vector3.up), Is.GreaterThan(0.999f));
            }
            finally
            {
                DestroyMeshes(meshes);
                Object.DestroyImmediate(networkObject);
            }
        }

        [Test]
        public void UnrelatedUndoDoesNotRegenerateGeneratedSurfaces()
        {
            var networkObject = new GameObject("RoadNetwork_Unrelated_Undo_Test");
            GameObject unrelatedObject = null;

            try
            {
                var network = networkObject.AddComponent<RoadNetwork>();
                var first = CreateSegment(network, "RoadSegment_First", 0f);
                var second = CreateSegment(network, "RoadSegment_Second", 10f);
                RoadSegmentSurfaceGenerator.Regenerate(first, false);
                RoadSegmentSurfaceGenerator.Regenerate(second, false);

                var firstMesh = GetSurfaceMesh(first);
                var secondMesh = GetSurfaceMesh(second);
                unrelatedObject = new GameObject("RoadNetwork_Unrelated_Undo_Target");
                Undo.RegisterCreatedObjectUndo(unrelatedObject, "Create Unrelated Object");
                Undo.PerformUndo();
                RoadNetworkPreviewScheduler.FlushForTests();

                Assert.That(GetSurfaceMesh(first), Is.SameAs(firstMesh));
                Assert.That(GetSurfaceMesh(second), Is.SameAs(secondMesh));
            }
            finally
            {
                Object.DestroyImmediate(networkObject);
                if (unrelatedObject != null)
                {
                    Object.DestroyImmediate(unrelatedObject);
                }
            }
        }

        [Test]
        public void PreviewRegenerationSurvivesRoadEditUndoRedo()
        {
            var networkObject = new GameObject("RoadNetwork_Road_Undo_Test");

            try
            {
                var network = networkObject.AddComponent<RoadNetwork>();
                var segment = CreateSegment(network, "RoadSegment_Undo", 0f);
                RoadSegmentSurfaceGenerator.Regenerate(segment, false);
                var initialMesh = GetSurfaceMesh(segment);
                AssertSurfaceEndV(segment, 2f);

                Undo.IncrementCurrentGroup();
                Undo.SetCurrentGroupName("Move Road Control Point");
                RoadSegmentSurfaceGenerator.RegisterGeneratedHierarchyUndo(segment, "Move Road Control Point");
                Undo.RecordObject(segment, "Move Road Control Point");
                segment.controlPoints[1].position = new Vector3(0f, 0f, 25f);
                RoadNetworkPreviewScheduler.Schedule(segment, true);
                Undo.IncrementCurrentGroup();
                RoadNetworkPreviewScheduler.FlushForTests();

                Assert.That(segment.controlPoints[1].position.z, Is.EqualTo(25f).Within(0.001f));
                Assert.That(GetSurfaceMesh(segment), Is.Not.SameAs(initialMesh));
                AssertSurfaceEndV(segment, 2.5f);

                Undo.PerformUndo();
                RoadNetworkPreviewScheduler.FlushForTests();
                Assert.That(segment.controlPoints[1].position.z, Is.EqualTo(20f).Within(0.001f));
                AssertSurfaceEndV(segment, 2f);

                Undo.PerformRedo();
                RoadNetworkPreviewScheduler.FlushForTests();
                Assert.That(segment.controlPoints[1].position.z, Is.EqualTo(25f).Within(0.001f));
                AssertSurfaceEndV(segment, 2.5f);
            }
            finally
            {
                Object.DestroyImmediate(networkObject);
            }
        }

        [Test]
        public void PreviewScheduleRegeneratesOnlyRequestedSegment()
        {
            var networkObject = new GameObject("RoadNetwork_Targeted_Preview_Test");

            try
            {
                var network = networkObject.AddComponent<RoadNetwork>();
                var first = CreateSegment(network, "RoadSegment_First", 0f);
                var second = CreateSegment(network, "RoadSegment_Second", 10f);
                RoadSegmentSurfaceGenerator.Regenerate(first, false);
                RoadSegmentSurfaceGenerator.Regenerate(second, false);

                var firstMesh = GetSurfaceMesh(first);
                var secondMesh = GetSurfaceMesh(second);
                RoadNetworkPreviewScheduler.Schedule(first);
                RoadNetworkPreviewScheduler.FlushForTests();

                Assert.That(GetSurfaceMesh(first), Is.Not.SameAs(firstMesh));
                Assert.That(GetSurfaceMesh(second), Is.SameAs(secondMesh));
            }
            finally
            {
                Object.DestroyImmediate(networkObject);
            }
        }

        private static void DestroyMeshes(IEnumerable<RoadSurfaceMeshData> meshes)
        {
            if (meshes == null)
            {
                return;
            }

            foreach (var meshData in meshes)
            {
                if (meshData?.mesh != null)
                {
                    Object.DestroyImmediate(meshData.mesh);
                }
            }
        }

        private static RoadSegment CreateSegment(RoadNetwork network, string name, float xOffset)
        {
            var segmentObject = new GameObject(name);
            segmentObject.transform.SetParent(network.transform, false);
            var segment = segmentObject.AddComponent<RoadSegment>();
            segment.controlPoints = new[]
            {
                new SplinePoint(new Vector3(xOffset, 0f, 0f)),
                new SplinePoint(new Vector3(xOffset, 0f, 20f)),
            };
            segment.profileKeys = new[]
            {
                new RoadProfileKey { profile = RoadProfile.CreateDefaultTwoLane() },
            };
            return segment;
        }

        private static void AssertSurfaceEndV(RoadSegment segment, float expectedV)
        {
            var mesh = GetSurfaceMesh(segment);
            var uvs = mesh.uv;
            Assert.That(uvs, Has.Length.GreaterThan(0));
            Assert.That(uvs[uvs.Length - 1].y, Is.EqualTo(expectedV).Within(0.001f));
        }

        private static Mesh GetSurfaceMesh(RoadSegment segment)
        {
            Assert.That(segment.generatedSurfaceObjects, Is.Not.Null);
            Assert.That(segment.generatedSurfaceObjects, Has.Count.GreaterThan(0));
            var meshFilter = segment.generatedSurfaceObjects[0].GetComponent<MeshFilter>();
            Assert.That(meshFilter, Is.Not.Null);
            return meshFilter.sharedMesh;
        }
    }
}

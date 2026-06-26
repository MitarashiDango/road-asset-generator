using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace MitarashiDango.RoadAssetGenerator.Tests
{
    public class RoadMarkingMeshBuilderTests
    {
        [Test]
        public void StraightDefaultProfileBuildsBoundaryMarkings()
        {
            var networkObject = new GameObject("RoadNetwork_Marking_Test");
            var meshes = new List<RoadMarkingMeshData>();

            try
            {
                var network = networkObject.AddComponent<RoadNetwork>();
                network.meshSegmentLengthMeters = 100f;
                network.maxSurfaceSampleLengthMeters = 5f;
                network.markingVertexOffsetMeters = 0.01f;
                var segment = CreateSegment(network, RoadProfile.CreateDefaultTwoLane(), 20f);

                meshes = RoadMarkingMeshBuilder.Build(segment, network);

                Assert.That(meshes, Has.Count.EqualTo(3));
                var leftVertices = meshes[0].mesh.vertices;
                Assert.That(leftVertices[0].x, Is.EqualTo(-3.075f).Within(0.001f));
                Assert.That(leftVertices[1].x, Is.EqualTo(-2.925f).Within(0.001f));
                Assert.That(leftVertices[0].y, Is.EqualTo(0.01f).Within(0.001f));
                Assert.That(meshes[0].mesh.colors[0], Is.EqualTo(Color.white));
                Assert.That(meshes[1].color.r, Is.EqualTo(232f / 255f).Within(0.001f));
                Assert.That(meshes[1].color.g, Is.EqualTo(168f / 255f).Within(0.001f));
            }
            finally
            {
                DestroyMeshes(meshes);
                Object.DestroyImmediate(networkObject);
            }
        }

        [Test]
        public void DashedStrokeLeavesMeshGaps()
        {
            var networkObject = new GameObject("RoadNetwork_Dashed_Marking_Test");
            var solidMeshes = new List<RoadMarkingMeshData>();
            var dashedMeshes = new List<RoadMarkingMeshData>();

            try
            {
                var network = networkObject.AddComponent<RoadNetwork>();
                network.meshSegmentLengthMeters = 100f;
                network.maxSurfaceSampleLengthMeters = 1f;

                var solid = CreateSegment(network, CreateSingleStrokeProfile(RoadLineKind.Solid), 10f);
                var dashed = CreateSegment(network, CreateSingleStrokeProfile(RoadLineKind.Dashed), 10f);

                solidMeshes = RoadMarkingMeshBuilder.Build(solid, network);
                dashedMeshes = RoadMarkingMeshBuilder.Build(dashed, network);

                Assert.That(solidMeshes, Has.Count.EqualTo(1));
                Assert.That(dashedMeshes, Has.Count.EqualTo(1));
                Assert.That(dashedMeshes[0].mesh.triangles.Length, Is.LessThan(solidMeshes[0].mesh.triangles.Length));
            }
            finally
            {
                DestroyMeshes(solidMeshes);
                DestroyMeshes(dashedMeshes);
                Object.DestroyImmediate(networkObject);
            }
        }

        [Test]
        public void RegenerateCreatesSeparateMarkingsRootAndColoredMaterial()
        {
            var networkObject = new GameObject("RoadNetwork_Marking_Generator_Test");
            Material fallbackMaterial = null;

            try
            {
                fallbackMaterial = CreateTestMaterial("Fallback Marking Material");
                var network = networkObject.AddComponent<RoadNetwork>();
                network.markingMaterial = fallbackMaterial;
                var segment = CreateSegment(network, RoadProfile.CreateDefaultTwoLane(), 20f);

                RoadSegmentSurfaceGenerator.Regenerate(segment, false);

                Assert.That(segment.surfacesRoot, Is.Not.Null);
                Assert.That(segment.markingsRoot, Is.Not.Null);
                Assert.That(segment.generatedMarkingObjects, Has.Count.EqualTo(3));

                var centerRenderer = segment.generatedMarkingObjects[1].GetComponent<MeshRenderer>();
                Assert.That(centerRenderer, Is.Not.Null);
                Assert.That(centerRenderer.sharedMaterial, Is.Not.SameAs(fallbackMaterial));
                Assert.That(centerRenderer.sharedMaterial.renderQueue, Is.GreaterThanOrEqualTo((int)RenderQueue.Geometry + 20));
                Assert.That(centerRenderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off));
                Assert.That(centerRenderer.receiveShadows, Is.False);
                foreach (var markingObject in segment.generatedMarkingObjects)
                {
                    Assert.That(markingObject.GetComponent<Collider>(), Is.Null);
                }
                var color = ReadMaterialColor(centerRenderer.sharedMaterial);
                Assert.That(color.r, Is.EqualTo(232f / 255f).Within(0.001f));
                Assert.That(color.g, Is.EqualTo(168f / 255f).Within(0.001f));

                var markingsRoot = segment.markingsRoot;
                RoadSegmentSurfaceGenerator.Clear(segment, false);

                Assert.That(segment.markingsRoot, Is.Null);
                Assert.That(segment.generatedMarkingObjects, Is.Empty);
                Assert.That(markingsRoot == null, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(networkObject);
                Object.DestroyImmediate(fallbackMaterial);
            }
        }

        private static RoadSegment CreateSegment(RoadNetwork network, RoadProfile profile, float lengthMeters)
        {
            var segmentObject = new GameObject("RoadSegment_Marking_Test");
            segmentObject.transform.SetParent(network.transform, false);
            var segment = segmentObject.AddComponent<RoadSegment>();
            segment.controlPoints = new[]
            {
                new SplinePoint(new Vector3(0f, 0f, 0f)),
                new SplinePoint(new Vector3(0f, 0f, lengthMeters)),
            };
            segment.profileKeys = new[]
            {
                new RoadProfileKey { profile = profile },
            };
            return segment;
        }

        private static RoadProfile CreateSingleStrokeProfile(RoadLineKind kind)
        {
            var profile = new RoadProfile
            {
                leftShoulderWidthMeters = 0f,
                rightShoulderWidthMeters = 0f,
            };
            profile.lanes.Add(new RoadLane { widthMeters = 3f, direction = RoadLaneDirection.Forward });
            profile.boundaryLines.Add(RoadBoundaryLine.Single("Unused", new RoadLineStroke { kind = RoadLineKind.None }));
            profile.boundaryLines.Add(RoadBoundaryLine.Single(
                "Test Stroke",
                new RoadLineStroke
                {
                    kind = kind,
                    widthMeters = 0.2f,
                    color = Color.white,
                    dashLengthMeters = 2f,
                    dashGapMeters = 1f,
                }));
            return profile;
        }

        private static void DestroyMeshes(IEnumerable<RoadMarkingMeshData> meshes)
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

        private static Material CreateTestMaterial(string name)
        {
            var shader = Shader.Find("Standard") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Hidden/InternalErrorShader");
            Assert.That(shader, Is.Not.Null);
            return new Material(shader)
            {
                name = name,
            };
        }

        private static Color ReadMaterialColor(Material material)
        {
            if (material.HasProperty("_BaseColor"))
            {
                return material.GetColor("_BaseColor");
            }
            if (material.HasProperty("_Color"))
            {
                return material.GetColor("_Color");
            }

            return Color.clear;
        }
    }
}

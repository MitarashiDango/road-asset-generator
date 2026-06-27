using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
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
                AssertLightingReadyMarkingMesh(meshes[0].mesh);
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
        public void TightCurveMarkingsFollowSurfaceOffset()
        {
            var networkObject = new GameObject("RoadNetwork_Tight_Curve_Marking_Test");
            var surfaceMeshes = new List<RoadSurfaceMeshData>();
            var markingMeshes = new List<RoadMarkingMeshData>();

            try
            {
                var network = networkObject.AddComponent<RoadNetwork>();
                network.meshSegmentLengthMeters = 100f;
                network.maxSurfaceSampleLengthMeters = 1f;
                network.maxSurfaceSampleAngleDegrees = 4f;
                network.markingVertexOffsetMeters = 0.02f;
                var segment = CreateSegment(
                    network,
                    RoadProfile.CreateDefaultTwoLane(),
                    new[]
                    {
                        new Vector3(0f, 0f, 0f),
                        new Vector3(0f, 0f, 15f),
                        new Vector3(15f, 0f, 30f),
                        new Vector3(30f, 0f, 30f),
                    });

                surfaceMeshes = RoadSurfaceMeshBuilder.Build(segment, network);
                markingMeshes = RoadMarkingMeshBuilder.Build(segment, network);

                Assert.That(surfaceMeshes, Has.Count.EqualTo(1));
                Assert.That(markingMeshes, Has.Count.EqualTo(3));
                Assert.That(markingMeshes[1].mesh.vertexCount, Is.GreaterThan(6));
                Assert.That(markingMeshes[1].mesh.bounds.size.x, Is.GreaterThan(10f));
                AssertLightingReadyMarkingMesh(markingMeshes[1].mesh);
                foreach (var vertex in markingMeshes[1].mesh.vertices)
                {
                    Assert.That(vertex.y, Is.EqualTo(0.02f).Within(0.001f));
                }
            }
            finally
            {
                DestroySurfaceMeshes(surfaceMeshes);
                DestroyMeshes(markingMeshes);
                Object.DestroyImmediate(networkObject);
            }
        }

        [Test]
        public void LongSolidMarkingSplitsBeforeUInt16VertexLimit()
        {
            var networkObject = new GameObject("RoadNetwork_Long_Marking_Split_Test");
            var meshes = new List<RoadMarkingMeshData>();

            try
            {
                var network = networkObject.AddComponent<RoadNetwork>();
                network.meshSegmentLengthMeters = 20000f;
                network.maxSurfaceSampleLengthMeters = 0.25f;
                network.maxSurfaceSampleAngleDegrees = 45f;
                var segment = CreateSegment(network, CreateSingleStrokeProfile(RoadLineKind.Solid), 9000f);

                meshes = RoadMarkingMeshBuilder.Build(segment, network);

                Assert.That(meshes, Has.Count.GreaterThan(1));
                foreach (var meshData in meshes)
                {
                    Assert.That(meshData.mesh.indexFormat, Is.EqualTo(IndexFormat.UInt16));
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
                Assert.That(centerRenderer.receiveShadows, Is.True);
                Assert.That(centerRenderer.lightProbeUsage, Is.EqualTo(LightProbeUsage.BlendProbes));
                Assert.That(centerRenderer.reflectionProbeUsage, Is.EqualTo(ReflectionProbeUsage.BlendProbes));
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

        [Test]
        public void RepeatedRegenerationKeepsGeneratedHierarchyStable()
        {
            var networkObject = new GameObject("RoadNetwork_Repeated_Regeneration_Test");
            RoadSegment segment = null;

            try
            {
                var network = networkObject.AddComponent<RoadNetwork>();
                segment = CreateSegment(network, RoadProfile.CreateDefaultTwoLane(), 20f);

                RoadSegmentSurfaceGenerator.Regenerate(segment, false);
                var surfaceCount = segment.generatedSurfaceObjects.Count;
                var markingCount = segment.generatedMarkingObjects.Count;

                for (var i = 0; i < 100; i++)
                {
                    RoadSegmentSurfaceGenerator.Regenerate(segment, false);
                    Assert.That(segment.transform.childCount, Is.EqualTo(2));
                    Assert.That(segment.generatedSurfaceObjects, Has.Count.EqualTo(surfaceCount));
                    Assert.That(segment.generatedMarkingObjects, Has.Count.EqualTo(markingCount));
                }
            }
            finally
            {
                if (segment != null)
                {
                    RoadSegmentSurfaceGenerator.Clear(segment, false);
                }
                Object.DestroyImmediate(networkObject);
            }
        }

        [Test]
        public void DefaultMarkingShaderNamesResolve()
        {
            var builtInShader = Shader.Find("MitarashiDango/RoadAssetGenerator/RoadMarkingDepthBiasedBuiltIn");
            var urpShader = Shader.Find("MitarashiDango/RoadAssetGenerator/RoadMarkingDepthBiasedURP");

            Assert.That(builtInShader, Is.Not.Null);
            Assert.That(builtInShader.name, Is.EqualTo("MitarashiDango/RoadAssetGenerator/RoadMarkingDepthBiasedBuiltIn"));
            Assert.That(urpShader, Is.Not.Null);
            Assert.That(urpShader.name, Is.EqualTo("MitarashiDango/RoadAssetGenerator/RoadMarkingDepthBiasedURP"));
        }

        [Test]
        public void UrpMarkingShaderDefinesForwardLitPass()
        {
            var shaderSource = AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Packages/com.matcha-soft.road-asset-generator/Runtime/Shaders/RoadMarkingDepthBiasedURP.shader");

            Assert.That(shaderSource, Is.Not.Null);
            Assert.That(shaderSource.text, Does.Contain("Name \"RoadMarkingForwardLit\""));
            Assert.That(shaderSource.text, Does.Contain("\"LightMode\" = \"UniversalForward\""));
            Assert.That(shaderSource.text, Does.Contain("UniversalFragmentPBR"));
            Assert.That(shaderSource.text, Does.Not.Contain("Name \"RoadMarkingUnlit\""));
        }

        [Test]
        public void GeneratedLayersUseNetworkDefaultsAndIndependentSegmentOverrides()
        {
            var networkObject = new GameObject("RoadNetwork_Generated_Layer_Test");
            RoadSegment segment = null;

            try
            {
                var network = networkObject.AddComponent<RoadNetwork>();
                network.defaultGeneratedSurfaceLayer = 3;
                network.defaultGeneratedMarkingLayer = 4;
                segment = CreateSegment(network, RoadProfile.CreateDefaultTwoLane(), 20f);

                RoadSegmentSurfaceGenerator.Regenerate(segment, false);

                AssertGeneratedLayer(segment.surfacesRoot, segment.generatedSurfaceObjects, 3);
                AssertGeneratedLayer(segment.markingsRoot, segment.generatedMarkingObjects, 4);

                segment.overrideGeneratedSurfaceLayer = true;
                segment.generatedSurfaceLayer = 5;
                RoadSegmentSurfaceGenerator.Regenerate(segment, false);

                AssertGeneratedLayer(segment.surfacesRoot, segment.generatedSurfaceObjects, 5);
                AssertGeneratedLayer(segment.markingsRoot, segment.generatedMarkingObjects, 4);

                segment.overrideGeneratedSurfaceLayer = false;
                segment.overrideGeneratedMarkingLayer = true;
                segment.generatedMarkingLayer = 6;
                RoadSegmentSurfaceGenerator.ApplyGeneratedLayers(segment, false);

                AssertGeneratedLayer(segment.surfacesRoot, segment.generatedSurfaceObjects, 3);
                AssertGeneratedLayer(segment.markingsRoot, segment.generatedMarkingObjects, 6);
            }
            finally
            {
                if (segment != null)
                {
                    RoadSegmentSurfaceGenerator.Clear(segment, false);
                }
                Object.DestroyImmediate(networkObject);
            }
        }

        private static RoadSegment CreateSegment(RoadNetwork network, RoadProfile profile, float lengthMeters)
        {
            return CreateSegment(
                network,
                profile,
                new[]
                {
                    new Vector3(0f, 0f, 0f),
                    new Vector3(0f, 0f, lengthMeters),
                });
        }

        private static RoadSegment CreateSegment(RoadNetwork network, RoadProfile profile, Vector3[] controlPoints)
        {
            var segmentObject = new GameObject("RoadSegment_Marking_Test");
            segmentObject.transform.SetParent(network.transform, false);
            var segment = segmentObject.AddComponent<RoadSegment>();
            segment.controlPoints = new SplinePoint[controlPoints.Length];
            for (var i = 0; i < controlPoints.Length; i++)
            {
                segment.controlPoints[i] = new SplinePoint(controlPoints[i]);
            }

            segment.profileKeys = new[]
            {
                new RoadProfileKey { profile = profile },
            };
            return segment;
        }

        private static void AssertLightingReadyMarkingMesh(Mesh mesh)
        {
            Assert.That(mesh, Is.Not.Null);
            Assert.That(mesh.normals, Has.Length.EqualTo(mesh.vertexCount));
            Assert.That(mesh.tangents, Has.Length.EqualTo(mesh.vertexCount));
            Assert.That(mesh.uv, Has.Length.EqualTo(mesh.vertexCount));
            Assert.That(mesh.uv2, Has.Length.EqualTo(mesh.vertexCount));
            Assert.That(mesh.uv[0].x, Is.EqualTo(0f).Within(0.001f));
            Assert.That(mesh.uv[1].x, Is.EqualTo(1f).Within(0.001f));

            foreach (var normal in mesh.normals)
            {
                Assert.That(normal.sqrMagnitude, Is.GreaterThan(0.99f));
                Assert.That(Vector3.Dot(normal.normalized, Vector3.up), Is.GreaterThan(0.999f));
            }

            foreach (var tangent in mesh.tangents)
            {
                var tangentDirection = new Vector3(tangent.x, tangent.y, tangent.z);
                Assert.That(tangentDirection.sqrMagnitude, Is.GreaterThan(0.99f));
                Assert.That(tangent.w, Is.EqualTo(-1f).Within(0.001f));
            }

            foreach (var uv in mesh.uv2)
            {
                Assert.That(uv.x, Is.InRange(0f, 1f));
                Assert.That(uv.y, Is.InRange(0f, 1f));
            }
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

        private static void DestroySurfaceMeshes(IEnumerable<RoadSurfaceMeshData> meshes)
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

        private static void AssertGeneratedLayer(GameObject root, IReadOnlyList<GameObject> generatedObjects, int expectedLayer)
        {
            Assert.That(root, Is.Not.Null);
            Assert.That(root.layer, Is.EqualTo(expectedLayer));
            Assert.That(generatedObjects, Is.Not.Null);
            Assert.That(generatedObjects, Has.Count.GreaterThan(0));
            foreach (var generatedObject in generatedObjects)
            {
                Assert.That(generatedObject, Is.Not.Null);
                Assert.That(generatedObject.layer, Is.EqualTo(expectedLayer));
            }
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

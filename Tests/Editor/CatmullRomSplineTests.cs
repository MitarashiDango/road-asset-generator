using NUnit.Framework;
using UnityEngine;

namespace MitarashiDango.RoadAssetGenerator.Tests
{
    public class CatmullRomSplineTests
    {
        [Test]
        public void StraightLineLengthAndDistanceSamplingAreStable()
        {
            var spline = new CatmullRomSpline(new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(0f, 0f, 10f),
            });

            var table = spline.BuildArcLengthTable();
            var sample = table.SampleByDistance(5f);

            Assert.That(table.TotalLengthMeters, Is.EqualTo(10f).Within(0.001f));
            Assert.That(sample.position.x, Is.EqualTo(0f).Within(0.001f));
            Assert.That(sample.position.y, Is.EqualTo(0f).Within(0.001f));
            Assert.That(sample.position.z, Is.EqualTo(5f).Within(0.001f));
            Assert.That(Vector3.Dot(sample.tangent, Vector3.forward), Is.GreaterThan(0.999f));
        }

        [Test]
        public void CurvatureIsZeroOnStraightLineAndPositiveOnBend()
        {
            var straight = new CatmullRomSpline(new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(0f, 0f, 10f),
            });
            Assert.That(straight.EstimateCurvature(0.5f), Is.EqualTo(0f).Within(0.0001f));

            var bend = new CatmullRomSpline(new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(10f, 0f, 0f),
                new Vector3(10f, 0f, 10f),
                new Vector3(20f, 0f, 10f),
            });
            Assert.That(bend.EstimateCurvature(1f), Is.GreaterThan(0.001f));
        }

        [Test]
        public void FrameUsesFallbackWhenTangentIsVertical()
        {
            var spline = new CatmullRomSpline(new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(0f, 10f, 0f),
            });

            var frame = spline.EvaluateFrame(0.5f);

            Assert.That(frame.tangent.y, Is.GreaterThan(0.999f));
            Assert.That(frame.right.sqrMagnitude, Is.EqualTo(1f).Within(0.001f));
            Assert.That(frame.up.sqrMagnitude, Is.EqualTo(1f).Within(0.001f));
            Assert.That(float.IsNaN(frame.right.x), Is.False);
            Assert.That(float.IsNaN(frame.up.x), Is.False);
        }

        [Test]
        public void NonUniformCurveSamplesByDistanceOnCurve()
        {
            var spline = new CatmullRomSpline(new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(4f, 0f, 1f),
                new Vector3(8f, 0f, 9f),
                new Vector3(14f, 0f, 12f),
            });

            var table = spline.BuildArcLengthTable(48);
            var halfway = table.SampleByDistance(table.TotalLengthMeters * 0.5f);

            Assert.That(table.TotalLengthMeters, Is.GreaterThan(Vector3.Distance(spline.EvaluatePosition(0f), spline.EvaluatePosition(spline.MaxParameter))));
            Assert.That(halfway.distanceMeters, Is.EqualTo(table.TotalLengthMeters * 0.5f).Within(0.001f));
            Assert.That(halfway.parameter, Is.GreaterThan(0f));
            Assert.That(halfway.parameter, Is.LessThan(spline.MaxParameter));
            Assert.That(halfway.position.x, Is.GreaterThan(0f));
            Assert.That(halfway.position.x, Is.LessThan(14f));
        }

        [Test]
        public void EndpointTangentsFollowFirstAndLastSegments()
        {
            var startDirection = new Vector3(5f, 0f, 2f).normalized;
            var endDirection = new Vector3(4f, 0f, 6f).normalized;
            var spline = new CatmullRomSpline(new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(5f, 0f, 2f),
                new Vector3(9f, 0f, 8f),
            });

            Assert.That(Vector3.Dot(spline.EvaluateTangent(0f), startDirection), Is.GreaterThan(0.99f));
            Assert.That(Vector3.Dot(spline.EvaluateTangent(spline.MaxParameter), endDirection), Is.GreaterThan(0.99f));
        }

        [Test]
        public void CurvatureRadiusIsFiniteOnTightBend()
        {
            var spline = new CatmullRomSpline(new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(3f, 0f, 0f),
                new Vector3(3f, 0f, 3f),
                new Vector3(6f, 0f, 3f),
            });

            var radius = spline.EstimateCurvatureRadius(1f);

            Assert.That(float.IsInfinity(radius), Is.False);
            Assert.That(radius, Is.GreaterThan(0f));
            Assert.That(radius, Is.LessThan(10f));
        }
    }
}

using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace MitarashiDango.RoadAssetGenerator.Tests
{
    public class RoadProfileValidationTests
    {
        [Test]
        public void ProfileCloneDoesNotShareMutableLists()
        {
            var original = RoadProfile.CreateDefaultTwoLane();
            var clone = original.Clone();

            clone.lanes[0].widthMeters = 4.5f;
            clone.boundaryLines[1].strokes[0].kind = RoadLineKind.Dashed;

            Assert.That(original.lanes[0].widthMeters, Is.EqualTo(3f).Within(0.001f));
            Assert.That(original.boundaryLines[1].strokes[0].kind, Is.EqualTo(RoadLineKind.Solid));
        }

        [Test]
        public void ValidateProfileKeysReportsOrderRangeAndMvpLimit()
        {
            var gameObject = new GameObject("RoadSegment_Test");
            try
            {
                var segment = gameObject.AddComponent<RoadSegment>();
                segment.controlPoints = new[]
                {
                    new SplinePoint(new Vector3(0f, 0f, 0f)),
                    new SplinePoint(new Vector3(0f, 0f, 10f)),
                };
                segment.profileKeys = new[]
                {
                    new RoadProfileKey { positionMeters = 0f, profile = RoadProfile.CreateDefaultTwoLane() },
                    new RoadProfileKey { positionMeters = 12f, profile = RoadProfile.CreateDefaultTwoLane() },
                    new RoadProfileKey { positionMeters = 6f, profile = RoadProfile.CreateDefaultTwoLane() },
                };

                var issues = RoadNetworkValidator.ValidateSegment(segment);

                Assert.That(issues.Any(i => i.code == RoadNetworkValidationCode.MultipleProfileKeysUnsupported), Is.True);
                Assert.That(issues.Any(i => i.code == RoadNetworkValidationCode.ProfileKeyBeyondLength), Is.True);
                Assert.That(issues.Any(i => i.code == RoadNetworkValidationCode.ProfileKeyOrder), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ValidateProfileReportsBoundaryCountAndStrokeCount()
        {
            var profile = RoadProfile.CreateDefaultTwoLane();
            profile.boundaryLines.RemoveAt(profile.boundaryLines.Count - 1);
            profile.boundaryLines[0].strokes.Add(RoadLineStroke.White(RoadLineKind.Solid));
            profile.boundaryLines[0].strokes.Add(RoadLineStroke.White(RoadLineKind.Solid));

            var segment = CreateSegment(profile);
            try
            {
                var issues = RoadNetworkValidator.ValidateSegment(segment);

                Assert.That(issues.Any(i => i.code == RoadNetworkValidationCode.BoundaryLineCountMismatch), Is.True);
                Assert.That(issues.Any(i => i.code == RoadNetworkValidationCode.BoundaryLineStrokeCount), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(segment.gameObject);
            }
        }

        [Test]
        public void ValidateProfileKeyReportsMissingEmbeddedProfile()
        {
            var segment = CreateSegment(null);
            try
            {
                var issues = RoadNetworkValidator.ValidateSegment(segment);

                Assert.That(issues.Any(i =>
                    i.code == RoadNetworkValidationCode.MissingProfile &&
                    i.severity == RoadNetworkValidationSeverity.Error), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(segment.gameObject);
            }
        }

        [Test]
        public void ValidateSegmentWarnsWhenCurvatureRadiusIsBelowHalfWidth()
        {
            var profile = RoadProfile.CreateDefaultTwoLane();
            profile.leftShoulderWidthMeters = 5f;
            profile.rightShoulderWidthMeters = 5f;
            foreach (var lane in profile.lanes)
            {
                lane.widthMeters = 5f;
            }

            var segment = CreateSegment(profile);
            try
            {
                segment.controlPoints = new[]
                {
                    new SplinePoint(new Vector3(0f, 0f, 0f)),
                    new SplinePoint(new Vector3(3f, 0f, 0f)),
                    new SplinePoint(new Vector3(3f, 0f, 3f)),
                    new SplinePoint(new Vector3(6f, 0f, 3f)),
                };

                var issues = RoadNetworkValidator.ValidateSegment(segment, splineSamplesPerSegment: 48, curvatureSampleStepMeters: 0.5f);

                Assert.That(issues.Any(i => i.code == RoadNetworkValidationCode.CurvatureRadiusBelowHalfWidth), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(segment.gameObject);
            }
        }

        private static RoadSegment CreateSegment(RoadProfile profile)
        {
            var gameObject = new GameObject("RoadSegment_Profile_Test");
            var segment = gameObject.AddComponent<RoadSegment>();
            segment.controlPoints = new[]
            {
                new SplinePoint(new Vector3(0f, 0f, 0f)),
                new SplinePoint(new Vector3(0f, 0f, 10f)),
            };
            segment.profileKeys = new[]
            {
                new RoadProfileKey { profile = profile },
            };
            return segment;
        }
    }
}

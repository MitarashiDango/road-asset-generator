using System;
using System.Collections.Generic;
using UnityEngine;

namespace MitarashiDango.RoadAssetGenerator
{
    public enum RoadNetworkValidationSeverity { Info, Warning, Error }
    public enum RoadNetworkValidationCode
    {
        MissingSegment,
        NotEnoughControlPoints,
        MissingProfileKey,
        MissingProfile,
        ProfileKeyOrder,
        ProfileKeyBeyondLength,
        MultipleProfileKeysUnsupported,
        CurvatureRadiusBelowHalfWidth,
        BoundaryLineCountMismatch,
        BoundaryLineStrokeCount,
    }

    /// <summary>道路ネットワーク検証の結果 1 件。</summary>
    [Serializable]
    public class RoadNetworkValidationIssue
    {
        public RoadNetworkValidationSeverity severity;
        public RoadNetworkValidationCode code;
        public UnityEngine.Object source;
        public string message;
        public float distanceMeters;

        public RoadNetworkValidationIssue(
            RoadNetworkValidationSeverity severity,
            RoadNetworkValidationCode code,
            UnityEngine.Object source,
            string message,
            float distanceMeters = 0f)
        {
            this.severity = severity;
            this.code = code;
            this.source = source;
            this.message = message;
            this.distanceMeters = distanceMeters;
        }
    }

    /// <summary>MVP で必要な道路ネットワークの検証ロジック。</summary>
    public static class RoadNetworkValidator
    {
        public static List<RoadNetworkValidationIssue> ValidateSegment(
            RoadSegment segment,
            int splineSamplesPerSegment = 24,
            float curvatureSampleStepMeters = 2f)
        {
            var issues = new List<RoadNetworkValidationIssue>();
            ValidateSegment(segment, issues, splineSamplesPerSegment, curvatureSampleStepMeters);
            return issues;
        }

        public static void ValidateSegment(
            RoadSegment segment,
            List<RoadNetworkValidationIssue> issues,
            int splineSamplesPerSegment = 24,
            float curvatureSampleStepMeters = 2f)
        {
            if (issues == null)
            {
                return;
            }

            if (segment == null)
            {
                issues.Add(new RoadNetworkValidationIssue(
                    RoadNetworkValidationSeverity.Error,
                    RoadNetworkValidationCode.MissingSegment,
                    null,
                    "道路区間が指定されていません。"));
                return;
            }

            if (segment.controlPoints == null || segment.controlPoints.Length < 2)
            {
                issues.Add(new RoadNetworkValidationIssue(
                    RoadNetworkValidationSeverity.Error,
                    RoadNetworkValidationCode.NotEnoughControlPoints,
                    segment,
                    "道路区間には 2 点以上の制御点が必要です。"));
                return;
            }

            var spline = new CatmullRomSpline(segment.controlPoints);
            var table = spline.BuildArcLengthTable(splineSamplesPerSegment);
            var length = table.TotalLengthMeters;

            ValidateProfileKeys(segment, length, issues);
            ValidateCurvature(segment, spline, table, issues, curvatureSampleStepMeters);
        }

        public static void ValidateProfileKeys(
            RoadSegment segment,
            float splineLengthMeters,
            List<RoadNetworkValidationIssue> issues)
        {
            if (issues == null)
            {
                return;
            }

            if (segment == null || segment.profileKeys == null || segment.profileKeys.Length == 0)
            {
                issues.Add(new RoadNetworkValidationIssue(
                    RoadNetworkValidationSeverity.Error,
                    RoadNetworkValidationCode.MissingProfileKey,
                    segment,
                    "道路区間には 1 件以上のプロファイルキーが必要です。"));
                return;
            }

            if (segment.profileKeys.Length > 1)
            {
                issues.Add(new RoadNetworkValidationIssue(
                    RoadNetworkValidationSeverity.Warning,
                    RoadNetworkValidationCode.MultipleProfileKeysUnsupported,
                    segment,
                    "複数のプロファイルキーは MVP 生成では未対応です。先頭キーを使用して生成します。"));
            }

            var previous = float.NegativeInfinity;
            for (var i = 0; i < segment.profileKeys.Length; i++)
            {
                var key = segment.profileKeys[i];
                if (key == null)
                {
                    issues.Add(new RoadNetworkValidationIssue(
                        RoadNetworkValidationSeverity.Error,
                        RoadNetworkValidationCode.MissingProfileKey,
                        segment,
                        $"プロファイルキー {i} が未設定です。"));
                    continue;
                }

                if (key.positionMeters < previous)
                {
                    issues.Add(new RoadNetworkValidationIssue(
                        RoadNetworkValidationSeverity.Warning,
                        RoadNetworkValidationCode.ProfileKeyOrder,
                        segment,
                        "プロファイルキーは位置昇順で並べてください。",
                        key.positionMeters));
                }

                if (key.positionMeters > splineLengthMeters)
                {
                    issues.Add(new RoadNetworkValidationIssue(
                        RoadNetworkValidationSeverity.Warning,
                        RoadNetworkValidationCode.ProfileKeyBeyondLength,
                        segment,
                        "プロファイルキー位置が道路区間長を超えています。",
                        key.positionMeters));
                }

                ValidateProfile(segment, key.profile, issues);
                previous = key.positionMeters;
            }
        }

        public static void ValidateProfile(
            UnityEngine.Object source,
            RoadProfile profile,
            List<RoadNetworkValidationIssue> issues)
        {
            if (issues == null)
            {
                return;
            }

            if (profile == null)
            {
                issues.Add(new RoadNetworkValidationIssue(
                    RoadNetworkValidationSeverity.Error,
                    RoadNetworkValidationCode.MissingProfile,
                    source,
                    "プロファイルキーには埋め込み RoadProfile が必要です。"));
                return;
            }

            var laneCount = profile.lanes?.Count ?? 0;
            var boundaryCount = profile.boundaryLines?.Count ?? 0;
            if (boundaryCount != laneCount + 1)
            {
                issues.Add(new RoadNetworkValidationIssue(
                    RoadNetworkValidationSeverity.Warning,
                    RoadNetworkValidationCode.BoundaryLineCountMismatch,
                    source,
                    "境界線リストの要素数は車線数 + 1 にしてください。"));
            }

            if (profile.boundaryLines == null)
            {
                return;
            }

            foreach (var boundaryLine in profile.boundaryLines)
            {
                var strokeCount = boundaryLine?.strokes?.Count ?? 0;
                if (strokeCount < 1 || strokeCount > 2)
                {
                    issues.Add(new RoadNetworkValidationIssue(
                        RoadNetworkValidationSeverity.Warning,
                        RoadNetworkValidationCode.BoundaryLineStrokeCount,
                        source,
                        "境界線に設定できるストロークは 1〜2 本です。"));
                }
            }
        }

        private static void ValidateCurvature(
            RoadSegment segment,
            CatmullRomSpline spline,
            CatmullRomArcLengthTable table,
            List<RoadNetworkValidationIssue> issues,
            float curvatureSampleStepMeters)
        {
            var profile = segment.GetActiveProfile();
            if (profile == null || profile.HalfWidthMeters <= CatmullRomSpline.Epsilon)
            {
                return;
            }

            var step = Mathf.Max(0.25f, curvatureSampleStepMeters);
            for (var distance = 0f; distance <= table.TotalLengthMeters; distance += step)
            {
                var sample = table.SampleByDistance(distance);
                if (sample.CurvatureRadiusMeters < profile.HalfWidthMeters)
                {
                    issues.Add(new RoadNetworkValidationIssue(
                        RoadNetworkValidationSeverity.Warning,
                        RoadNetworkValidationCode.CurvatureRadiusBelowHalfWidth,
                        segment,
                        "曲率半径が道路半幅を下回っています。内側エッジが自己交差する可能性があります。",
                        sample.distanceMeters));
                }
            }
        }
    }
}

#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MitarashiDango.RoadAssetGenerator
{
    /// <summary>Runtime 検証結果を Editor Console へ出力する。</summary>
    public static class RoadNetworkValidationReporter
    {
        public static void LogValidation(RoadNetwork network)
        {
            if (network == null)
            {
                return;
            }

            var issues = new List<RoadNetworkValidationIssue>();
            var segments = new List<RoadSegment>();
            network.CollectSegments(segments);
            foreach (var segment in segments)
            {
                RoadNetworkValidator.ValidateSegment(segment, issues);
            }

            if (issues.Count == 0)
            {
                Debug.Log($"[RoadAssetGenerator] {network.name}: 検証問題はありません。", network);
                return;
            }

            foreach (var issue in issues)
            {
                var message = $"[RoadAssetGenerator] {network.name}: {issue.message}";
                if (issue.distanceMeters > 0f)
                {
                    message += $" (distance: {issue.distanceMeters:0.##} m)";
                }

                switch (issue.severity)
                {
                    case RoadNetworkValidationSeverity.Error:
                        Debug.LogError(message, issue.source);
                        break;
                    case RoadNetworkValidationSeverity.Warning:
                        Debug.LogWarning(message, issue.source);
                        break;
                    default:
                        Debug.Log(message, issue.source);
                        break;
                }
            }

            EditorGUIUtility.PingObject(network);
        }
    }
}
#endif

#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MitarashiDango.RoadAssetGenerator
{
    /// <summary>RoadNetwork の Inspector 操作。</summary>
    [CustomEditor(typeof(RoadNetwork))]
    [CanEditMultipleObjects]
    public class RoadNetworkEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUI.BeginChangeCheck();
            DrawPropertiesExcluding(serializedObject, "m_Script");
            var changed = EditorGUI.EndChangeCheck();
            var applied = serializedObject.ApplyModifiedProperties();
            if (changed && applied)
            {
                ScheduleTargets();
            }

            EditorGUILayout.Space();
            DrawGenerationButtons();
            DrawValidationButton();
        }

        private void DrawGenerationButtons()
        {
            EditorGUILayout.LabelField("Surface Generation", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Regenerate All Surfaces"))
                {
                    var group = Undo.GetCurrentGroup();
                    Undo.SetCurrentGroupName("Regenerate Road Network Surfaces");
                    foreach (var selectedTarget in targets)
                    {
                        RegenerateAll((RoadNetwork)selectedTarget);
                    }
                    Undo.CollapseUndoOperations(group);
                }

                if (GUILayout.Button("Clear All Surfaces"))
                {
                    var group = Undo.GetCurrentGroup();
                    Undo.SetCurrentGroupName("Clear Road Network Surfaces");
                    foreach (var selectedTarget in targets)
                    {
                        ClearAll((RoadNetwork)selectedTarget);
                    }
                    Undo.CollapseUndoOperations(group);
                }
            }
        }

        private void DrawValidationButton()
        {
            if (GUILayout.Button("Validate Road Network"))
            {
                foreach (var selectedTarget in targets)
                {
                    RoadNetworkValidationReporter.LogValidation((RoadNetwork)selectedTarget);
                }
            }
        }

        private static void RegenerateAll(RoadNetwork network)
        {
            foreach (var segment in CollectSegments(network))
            {
                RoadSegmentSurfaceGenerator.Regenerate(segment, true);
            }
        }

        private static void ClearAll(RoadNetwork network)
        {
            foreach (var segment in CollectSegments(network))
            {
                RoadSegmentSurfaceGenerator.Clear(segment, true);
            }
        }

        private static List<RoadSegment> CollectSegments(RoadNetwork network)
        {
            var segments = new List<RoadSegment>();
            if (network != null)
            {
                network.CollectSegments(segments);
            }

            return segments;
        }

        private void ScheduleTargets()
        {
            foreach (var selectedTarget in targets)
            {
                RoadNetworkPreviewScheduler.Schedule((RoadNetwork)selectedTarget, true);
            }
        }
    }
}
#endif

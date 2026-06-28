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
            DrawNetworkProperties();
            var generationChanged = EditorGUI.EndChangeCheck();

            EditorGUI.BeginChangeCheck();
            DrawGeneratedLayerProperties();
            var layerChanged = EditorGUI.EndChangeCheck();

            var applied = serializedObject.ApplyModifiedProperties();
            if (applied)
            {
                if (layerChanged)
                {
                    ApplyTargetsGeneratedLayers(true);
                }
                if (generationChanged)
                {
                    ScheduleTargets();
                }
            }

            EditorGUILayout.Space();
            DrawGenerationButtons();
            DrawValidationButton();
        }

        private void DrawNetworkProperties()
        {
            EditorGUILayout.LabelField("New Segment Defaults", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("defaultSurfaceStyleTemplate"),
                new GUIContent("Default Surface Style"));
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("defaultProfileTemplate"),
                new GUIContent("Default Profile Template"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Fallbacks", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("textureLengthMeters"),
                new GUIContent("Fallback Texture Length Meters"));
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("surfaceMaterial"),
                new GUIContent("Fallback Surface Material"));
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("markingMaterial"),
                new GUIContent("Fallback Marking Material"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Generation", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("meshSegmentLengthMeters"));
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("markingVertexOffsetMeters"),
                new GUIContent(
                    "Marking Surface Offset Meters",
                    "Distance to lift generated markings from the road surface along the road normal."));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("maxSurfaceSampleLengthMeters"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("maxSurfaceSampleAngleDegrees"));
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("generateSurfaceColliders"),
                new GUIContent("Generate Surface Colliders"));
        }

        private void DrawGeneratedLayerProperties()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Generated Object Layers", EditorStyles.boldLabel);
            DrawLayerProperty(
                serializedObject.FindProperty("defaultGeneratedSurfaceLayer"),
                new GUIContent("Default Surface Layer"));
            DrawLayerProperty(
                serializedObject.FindProperty("defaultGeneratedMarkingLayer"),
                new GUIContent("Default Marking Layer"));
        }

        private void DrawGenerationButtons()
        {
            EditorGUILayout.LabelField("Road Generation", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Regenerate All Roads"))
                {
                    var group = Undo.GetCurrentGroup();
                    Undo.SetCurrentGroupName("Regenerate Road Network Geometry");
                    foreach (var selectedTarget in targets)
                    {
                        RegenerateAll((RoadNetwork)selectedTarget);
                    }
                    Undo.CollapseUndoOperations(group);
                }

                if (GUILayout.Button("Clear All Roads"))
                {
                    var group = Undo.GetCurrentGroup();
                    Undo.SetCurrentGroupName("Clear Road Network Geometry");
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

        private static void DrawLayerProperty(SerializedProperty property, GUIContent label)
        {
            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
            var value = EditorGUILayout.LayerField(
                label,
                RoadGeneratedLayerSettings.NormalizeLayer(property.intValue));
            EditorGUI.showMixedValue = false;
            if (value != property.intValue)
            {
                property.intValue = value;
            }
        }

        private void ApplyTargetsGeneratedLayers(bool registerUndo)
        {
            foreach (var selectedTarget in targets)
            {
                foreach (var segment in CollectSegments((RoadNetwork)selectedTarget))
                {
                    RoadSegmentSurfaceGenerator.ApplyGeneratedLayers(segment, registerUndo);
                }
            }
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

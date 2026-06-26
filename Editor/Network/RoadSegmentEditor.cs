#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace MitarashiDango.RoadAssetGenerator
{
    /// <summary>RoadSegment の Inspector と SceneView 制御点編集。</summary>
    [CustomEditor(typeof(RoadSegment))]
    [CanEditMultipleObjects]
    public class RoadSegmentEditor : Editor
    {
        private RoadProfileTemplateAsset templateToApply;
        private RoadSurfaceStyleAsset surfaceStyleToApply;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUI.BeginChangeCheck();
            DrawPropertiesExcluding(
                serializedObject,
                "m_Script",
                "roadNetwork",
                "useSurfaceStyle",
                "surfaceStyle",
                "overrideSurfaceSamplingSettings",
                "maxSurfaceSampleLengthMeters",
                "maxSurfaceSampleAngleDegrees",
                "surfacesRoot",
                "markingsRoot",
                "generatedSurfaceObjects",
                "generatedMarkingObjects");
            DrawSurfaceStyleUi();
            DrawSurfaceSamplingUi();
            var changed = EditorGUI.EndChangeCheck();
            var applied = serializedObject.ApplyModifiedProperties();
            if (changed && applied)
            {
                RegisterTargetsGeneratedHierarchyUndo("Edit Road Segment");
                ScheduleTargets();
            }

            EditorGUILayout.Space();
            DrawSurfaceStyleApplyUi();
            DrawTemplateApplyUi();
            DrawControlPointUtilityButtons();
            DrawGenerationButtons();
            DrawGeneratedObjectInfo();
        }

        private void DrawSurfaceStyleUi()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Surface Style", EditorStyles.boldLabel);

            var useProperty = serializedObject.FindProperty("useSurfaceStyle");
            var styleProperty = serializedObject.FindProperty("surfaceStyle");

            EditorGUILayout.PropertyField(useProperty, new GUIContent("Use Segment Surface Style"));
            var enabled = useProperty.hasMultipleDifferentValues || useProperty.boolValue;
            using (new EditorGUI.DisabledScope(!enabled))
            {
                EditorGUILayout.PropertyField(styleProperty, true);
            }
        }

        private void DrawSurfaceSamplingUi()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Surface Sampling", EditorStyles.boldLabel);

            var overrideProperty = serializedObject.FindProperty("overrideSurfaceSamplingSettings");
            var lengthProperty = serializedObject.FindProperty("maxSurfaceSampleLengthMeters");
            var angleProperty = serializedObject.FindProperty("maxSurfaceSampleAngleDegrees");

            EditorGUILayout.PropertyField(overrideProperty, new GUIContent("Override Surface Sampling"));
            var enabled = overrideProperty.hasMultipleDifferentValues || overrideProperty.boolValue;
            using (new EditorGUI.DisabledScope(!enabled))
            {
                EditorGUILayout.PropertyField(lengthProperty, new GUIContent("Max Surface Sample Length Meters"));
                EditorGUILayout.PropertyField(angleProperty, new GUIContent("Max Surface Sample Angle Degrees"));
            }
        }

        private void DrawSurfaceStyleApplyUi()
        {
            EditorGUILayout.LabelField("Surface Style Template", EditorStyles.boldLabel);
            surfaceStyleToApply = (RoadSurfaceStyleAsset)EditorGUILayout.ObjectField(
                "Template",
                surfaceStyleToApply,
                typeof(RoadSurfaceStyleAsset),
                false);

            using (new EditorGUI.DisabledScope(surfaceStyleToApply == null))
            {
                if (GUILayout.Button("Apply To Surface Style"))
                {
                    foreach (var selectedTarget in targets)
                    {
                        ApplySurfaceStyle((RoadSegment)selectedTarget, surfaceStyleToApply);
                    }
                }
            }
        }

        private void OnSceneGUI()
        {
            var segment = (RoadSegment)target;
            if (segment == null)
            {
                return;
            }

            DrawSplinePreview(segment);
            DrawValidationWarnings(segment);
            DrawControlPointHandles(segment);
            HandleShiftClickAppend(segment);
        }

        private void DrawTemplateApplyUi()
        {
            EditorGUILayout.LabelField("Profile Template", EditorStyles.boldLabel);
            templateToApply = (RoadProfileTemplateAsset)EditorGUILayout.ObjectField(
                "Template",
                templateToApply,
                typeof(RoadProfileTemplateAsset),
                false);

            using (new EditorGUI.DisabledScope(templateToApply == null))
            {
                if (GUILayout.Button("Apply To First Profile Key"))
                {
                    foreach (var selectedTarget in targets)
                    {
                        ApplyTemplate((RoadSegment)selectedTarget, templateToApply);
                    }
                }
            }
        }

        private void DrawControlPointUtilityButtons()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Control Points", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Append Point"))
                {
                    foreach (var selectedTarget in targets)
                    {
                        AppendControlPoint((RoadSegment)selectedTarget);
                    }
                }

                var canDelete = true;
                foreach (var selectedTarget in targets)
                {
                    var segment = (RoadSegment)selectedTarget;
                    canDelete &= segment.controlPoints != null && segment.controlPoints.Length > 2;
                }

                using (new EditorGUI.DisabledScope(!canDelete))
                {
                    if (GUILayout.Button("Delete Last Point"))
                    {
                        foreach (var selectedTarget in targets)
                        {
                            DeleteLastControlPoint((RoadSegment)selectedTarget);
                        }
                    }
                }
            }
        }

        private void DrawGenerationButtons()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Road Generation", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Regenerate Road"))
                {
                    var group = Undo.GetCurrentGroup();
                    Undo.SetCurrentGroupName("Regenerate Road Geometry");
                    foreach (var selectedTarget in targets)
                    {
                        RoadSegmentSurfaceGenerator.Regenerate((RoadSegment)selectedTarget, true);
                    }
                    Undo.CollapseUndoOperations(group);
                }

                if (GUILayout.Button("Clear Road"))
                {
                    var group = Undo.GetCurrentGroup();
                    Undo.SetCurrentGroupName("Clear Road Geometry");
                    foreach (var selectedTarget in targets)
                    {
                        RoadSegmentSurfaceGenerator.Clear((RoadSegment)selectedTarget, true);
                    }
                    Undo.CollapseUndoOperations(group);
                }
            }
        }

        private void DrawGeneratedObjectInfo()
        {
            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(true))
            {
                var segment = (RoadSegment)target;
                EditorGUILayout.ObjectField("Surfaces Root", segment.surfacesRoot, typeof(GameObject), true);
                EditorGUILayout.IntField("Surface Object Count", segment.generatedSurfaceObjects?.Count ?? 0);
                EditorGUILayout.ObjectField("Markings Root", segment.markingsRoot, typeof(GameObject), true);
                EditorGUILayout.IntField("Marking Object Count", segment.generatedMarkingObjects?.Count ?? 0);
            }
        }

        private static void DrawSplinePreview(RoadSegment segment)
        {
            if (!segment.TryCreateSpline(out var spline))
            {
                return;
            }

            var table = spline.BuildArcLengthTable(24);
            var pointCount = Mathf.Clamp(Mathf.CeilToInt(table.TotalLengthMeters / 2f) + 1, 2, 128);
            var points = new Vector3[pointCount];
            for (var i = 0; i < pointCount; i++)
            {
                var distance = table.TotalLengthMeters * i / (pointCount - 1);
                points[i] = segment.transform.TransformPoint(table.SampleByDistance(distance).position);
            }

            Handles.color = new Color(0.1f, 0.7f, 1f, 0.85f);
            Handles.DrawAAPolyLine(3f, points);
        }

        private static void DrawValidationWarnings(RoadSegment segment)
        {
            if (!segment.TryCreateSpline(out var spline))
            {
                return;
            }

            var issues = RoadNetworkValidator.ValidateSegment(segment);
            var hasCurvatureWarning = false;
            foreach (var issue in issues)
            {
                if (issue.code == RoadNetworkValidationCode.CurvatureRadiusBelowHalfWidth)
                {
                    hasCurvatureWarning = true;
                    break;
                }
            }

            if (!hasCurvatureWarning)
            {
                return;
            }

            var table = spline.BuildArcLengthTable(24);
            Handles.color = new Color(1f, 0.25f, 0.1f, 0.9f);
            foreach (var issue in issues)
            {
                if (issue.code != RoadNetworkValidationCode.CurvatureRadiusBelowHalfWidth)
                {
                    continue;
                }

                var sample = table.SampleByDistance(issue.distanceMeters);
                var worldPosition = segment.transform.TransformPoint(sample.position);
                var normal = segment.transform.TransformDirection(sample.frame.up);
                if (normal.sqrMagnitude <= CatmullRomSpline.Epsilon)
                {
                    normal = Vector3.up;
                }
                normal.Normalize();

                var right = segment.transform.TransformDirection(sample.frame.right);
                if (right.sqrMagnitude <= CatmullRomSpline.Epsilon)
                {
                    right = Vector3.right;
                }
                right.Normalize();

                var size = HandleUtility.GetHandleSize(worldPosition) * 0.25f;
                Handles.DrawWireDisc(worldPosition, normal, size);
                Handles.DrawAAPolyLine(2f, worldPosition - right * size, worldPosition + right * size);
            }
        }

        private static void DrawControlPointHandles(RoadSegment segment)
        {
            var points = segment.controlPoints;
            if (points == null)
            {
                return;
            }

            for (var i = 0; i < points.Length; i++)
            {
                if (points[i] == null)
                {
                    points[i] = new SplinePoint();
                }

                var worldPosition = segment.transform.TransformPoint(points[i].position);
                Handles.color = Color.cyan;
                var size = HandleUtility.GetHandleSize(worldPosition) * 0.08f;
                Handles.SphereHandleCap(0, worldPosition, Quaternion.identity, size, EventType.Repaint);
                Handles.Label(worldPosition + Vector3.up * size * 2f, i.ToString());

                EditorGUI.BeginChangeCheck();
                var movedWorldPosition = Handles.PositionHandle(worldPosition, segment.transform.rotation);
                if (EditorGUI.EndChangeCheck())
                {
                    RoadSegmentSurfaceGenerator.RegisterGeneratedHierarchyUndo(segment, "Move Road Control Point");
                    Undo.RecordObject(segment, "Move Road Control Point");
                    points[i].position = segment.transform.InverseTransformPoint(movedWorldPosition);
                    EditorUtility.SetDirty(segment);
                    RoadNetworkPreviewScheduler.Schedule(segment, true);
                }
            }
        }

        private static void HandleShiftClickAppend(RoadSegment segment)
        {
            var evt = Event.current;
            if (evt == null || evt.alt)
            {
                return;
            }

            if (evt.shift && evt.type == EventType.Layout)
            {
                HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            }

            if (!evt.shift || evt.type != EventType.MouseDown || evt.button != 0)
            {
                return;
            }

            var ray = HandleUtility.GUIPointToWorldRay(evt.mousePosition);
            var worldPosition = GetAppendWorldPosition(segment, ray);
            AppendControlPoint(segment, segment.transform.InverseTransformPoint(worldPosition));
            evt.Use();
        }

        private static Vector3 GetAppendWorldPosition(RoadSegment segment, Ray ray)
        {
            if (Physics.Raycast(ray, out var hit, 100000f))
            {
                return hit.point;
            }

            var planePoint = segment.transform.position;
            var points = segment.controlPoints;
            if (points != null && points.Length > 0 && points[points.Length - 1] != null)
            {
                planePoint = segment.transform.TransformPoint(points[points.Length - 1].position);
            }

            var plane = new Plane(Vector3.up, planePoint);
            if (plane.Raycast(ray, out var distance))
            {
                return ray.GetPoint(distance);
            }

            return planePoint + segment.transform.forward * 5f;
        }

        private static void ApplySurfaceStyle(RoadSegment segment, RoadSurfaceStyleAsset template)
        {
            if (segment == null || template == null)
            {
                return;
            }

            RoadSegmentSurfaceGenerator.RegisterGeneratedHierarchyUndo(segment, "Apply Road Surface Style");
            Undo.RecordObject(segment, "Apply Road Surface Style");
            segment.useSurfaceStyle = true;
            segment.surfaceStyle = template.CreateStyleCopy();
            EditorUtility.SetDirty(segment);
            RoadNetworkPreviewScheduler.Schedule(segment, true);
        }

        private static void ApplyTemplate(RoadSegment segment, RoadProfileTemplateAsset template)
        {
            if (segment == null || template == null)
            {
                return;
            }

            RoadSegmentSurfaceGenerator.RegisterGeneratedHierarchyUndo(segment, "Apply Road Profile Template");
            Undo.RecordObject(segment, "Apply Road Profile Template");
            if (segment.profileKeys == null || segment.profileKeys.Length == 0 || segment.profileKeys[0] == null)
            {
                segment.profileKeys = new[] { new RoadProfileKey() };
            }

            segment.profileKeys[0].profile = template.profile?.Clone();
            EditorUtility.SetDirty(segment);
            RoadNetworkPreviewScheduler.Schedule(segment, true);
        }

        private static void AppendControlPoint(RoadSegment segment)
        {
            var localPosition = Vector3.zero;
            var points = segment.controlPoints;
            if (points != null && points.Length > 0 && points[points.Length - 1] != null)
            {
                localPosition = points[points.Length - 1].position + Vector3.forward * 5f;
            }

            AppendControlPoint(segment, localPosition);
        }

        private static void AppendControlPoint(RoadSegment segment, Vector3 localPosition)
        {
            if (segment == null)
            {
                return;
            }

            RoadSegmentSurfaceGenerator.RegisterGeneratedHierarchyUndo(segment, "Append Road Control Point");
            Undo.RecordObject(segment, "Append Road Control Point");
            var oldPoints = segment.controlPoints ?? Array.Empty<SplinePoint>();
            var newPoints = new SplinePoint[oldPoints.Length + 1];
            Array.Copy(oldPoints, newPoints, oldPoints.Length);
            newPoints[newPoints.Length - 1] = new SplinePoint(localPosition);
            segment.controlPoints = newPoints;
            EditorUtility.SetDirty(segment);
            RoadNetworkPreviewScheduler.Schedule(segment, true);
        }

        private static void DeleteLastControlPoint(RoadSegment segment)
        {
            if (segment == null || segment.controlPoints == null || segment.controlPoints.Length <= 2)
            {
                return;
            }

            RoadSegmentSurfaceGenerator.RegisterGeneratedHierarchyUndo(segment, "Delete Road Control Point");
            Undo.RecordObject(segment, "Delete Road Control Point");
            var newPoints = new SplinePoint[segment.controlPoints.Length - 1];
            Array.Copy(segment.controlPoints, newPoints, newPoints.Length);
            segment.controlPoints = newPoints;
            EditorUtility.SetDirty(segment);
            RoadNetworkPreviewScheduler.Schedule(segment, true);
        }

        private void ScheduleTargets()
        {
            foreach (var selectedTarget in targets)
            {
                RoadNetworkPreviewScheduler.Schedule((RoadSegment)selectedTarget, true);
            }
        }

        private void RegisterTargetsGeneratedHierarchyUndo(string undoName)
        {
            foreach (var selectedTarget in targets)
            {
                RoadSegmentSurfaceGenerator.RegisterGeneratedHierarchyUndo((RoadSegment)selectedTarget, undoName);
            }
        }
    }
}
#endif

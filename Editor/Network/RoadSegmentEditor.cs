#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MitarashiDango.RoadAssetGenerator
{
    /// <summary>RoadSegment の Inspector と SceneView 制御点編集。</summary>
    [CustomEditor(typeof(RoadSegment))]
    [CanEditMultipleObjects]
    public class RoadSegmentEditor : Editor
    {
        private const int ShapePreserveSampleCount = 9;
        private const int ShapePreserveSearchIterations = 4;
        private const int ShapePreserveArcLengthSamplesPerSegment = 24;
        private const int DistanceAtParameterSearchIterations = 18;
        private const float ControlPointMarkerSize = 0.12f;
        private const float SelectedControlPointMarkerSize = 0.16f;
        private const float ControlPointMarkerMinWorldSize = 0.18f;
        private const float SelectedControlPointMarkerMinWorldSize = 0.24f;
        private const float MarkerPickScale = 1.8f;

        private static readonly Dictionary<int, int> SelectedControlPointIndices = new Dictionary<int, int>();
        private static readonly Vector3[] ShapePreserveSearchDirections =
        {
            Vector3.right,
            Vector3.left,
            Vector3.up,
            Vector3.down,
            Vector3.forward,
            Vector3.back,
        };

        private RoadProfileTemplateAsset templateToApply;
        private RoadSurfaceStyleAsset surfaceStyleToApply;

        private enum MarkerClickKind
        {
            None,
            Primary,
            Context,
        }

        static RoadSegmentEditor()
        {
            Selection.selectionChanged += CleanupStaleSelectedControlPointEntries;
            EditorApplication.hierarchyChanged += CleanupStaleSelectedControlPointEntries;
            Undo.undoRedoPerformed += HandleUndoRedo;
        }

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
                "maxSurfaceColumnWidthMeters",
                "overrideSurfaceColliderSettings",
                "generateSurfaceColliders",
                "overrideGeneratedSurfaceLayer",
                "generatedSurfaceLayer",
                "overrideGeneratedMarkingLayer",
                "generatedMarkingLayer",
                "surfacesRoot",
                "markingsRoot",
                "generatedSurfaceObjects",
                "generatedMarkingObjects");
            DrawSurfaceStyleUi();
            DrawSurfaceSamplingUi();
            DrawSurfaceColliderUi();
            var generationChanged = EditorGUI.EndChangeCheck();

            EditorGUI.BeginChangeCheck();
            DrawGeneratedLayerUi();
            var layerChanged = EditorGUI.EndChangeCheck();

            var applied = serializedObject.ApplyModifiedProperties();
            if (applied)
            {
                if (generationChanged || layerChanged)
                {
                    RegisterTargetsGeneratedHierarchyUndo("Edit Road Segment");
                }
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

            var styleProperty = serializedObject.FindProperty("surfaceStyle");

            EditorGUILayout.PropertyField(styleProperty, true);
        }

        private void DrawSurfaceSamplingUi()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Surface Sampling", EditorStyles.boldLabel);

            var overrideProperty = serializedObject.FindProperty("overrideSurfaceSamplingSettings");
            var lengthProperty = serializedObject.FindProperty("maxSurfaceSampleLengthMeters");
            var angleProperty = serializedObject.FindProperty("maxSurfaceSampleAngleDegrees");
            var columnWidthProperty = serializedObject.FindProperty("maxSurfaceColumnWidthMeters");

            EditorGUILayout.PropertyField(overrideProperty, new GUIContent("Override Surface Sampling"));
            var enabled = overrideProperty.hasMultipleDifferentValues || overrideProperty.boolValue;
            using (new EditorGUI.DisabledScope(!enabled))
            {
                EditorGUILayout.PropertyField(lengthProperty, new GUIContent("Max Surface Sample Length Meters"));
                EditorGUILayout.PropertyField(angleProperty, new GUIContent("Max Surface Sample Angle Degrees"));
                EditorGUILayout.PropertyField(columnWidthProperty, new GUIContent("Max Surface Column Width Meters"));
            }
        }

        private void DrawSurfaceColliderUi()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Surface Colliders", EditorStyles.boldLabel);

            var overrideProperty = serializedObject.FindProperty("overrideSurfaceColliderSettings");
            var generateProperty = serializedObject.FindProperty("generateSurfaceColliders");

            EditorGUILayout.PropertyField(
                overrideProperty,
                new GUIContent("Override Surface Collider Settings"));
            var enabled = overrideProperty.hasMultipleDifferentValues || overrideProperty.boolValue;
            using (new EditorGUI.DisabledScope(!enabled))
            {
                var inheritedValue = GetInheritedGenerateSurfaceColliders(out var inheritedMixedValue);
                var currentValue = enabled
                    ? generateProperty.boolValue
                    : inheritedValue;
                EditorGUI.showMixedValue = enabled
                    ? generateProperty.hasMultipleDifferentValues
                    : inheritedMixedValue;
                var value = EditorGUILayout.Toggle(
                    new GUIContent("Generate Surface Colliders"),
                    currentValue);
                EditorGUI.showMixedValue = false;
                if (enabled && value != generateProperty.boolValue)
                {
                    generateProperty.boolValue = value;
                }
            }
        }

        private void DrawGeneratedLayerUi()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Generated Object Layers", EditorStyles.boldLabel);

            DrawLayerOverride(
                serializedObject.FindProperty("overrideGeneratedSurfaceLayer"),
                serializedObject.FindProperty("generatedSurfaceLayer"),
                new GUIContent("Override Surface Layer"),
                new GUIContent("Surface Layer"),
                GetInheritedSurfaceLayer());
            DrawLayerOverride(
                serializedObject.FindProperty("overrideGeneratedMarkingLayer"),
                serializedObject.FindProperty("generatedMarkingLayer"),
                new GUIContent("Override Marking Layer"),
                new GUIContent("Marking Layer"),
                GetInheritedMarkingLayer());
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

        private static void DrawLayerOverride(
            SerializedProperty overrideProperty,
            SerializedProperty layerProperty,
            GUIContent overrideLabel,
            GUIContent layerLabel,
            int inheritedLayer)
        {
            EditorGUILayout.PropertyField(overrideProperty, overrideLabel);
            var enabled = overrideProperty.hasMultipleDifferentValues || overrideProperty.boolValue;
            using (new EditorGUI.DisabledScope(!enabled))
            {
                var currentLayer = enabled
                    ? RoadGeneratedLayerSettings.NormalizeLayer(layerProperty.intValue)
                    : inheritedLayer;
                EditorGUI.showMixedValue = enabled && layerProperty.hasMultipleDifferentValues;
                var value = EditorGUILayout.LayerField(layerLabel, currentLayer);
                EditorGUI.showMixedValue = false;
                if (enabled && value != layerProperty.intValue)
                {
                    layerProperty.intValue = value;
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
            NormalizeSelectedControlPointIndex(segment);
            DrawControlPointMarkers(segment);
            DrawSelectedControlPointHandle(segment);
            HandleControlPointDeleteKey(segment);
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
                if (GUILayout.Button("Prepend Point"))
                {
                    foreach (var selectedTarget in targets)
                    {
                        PrependControlPoint((RoadSegment)selectedTarget);
                    }
                }

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
                    canDelete &= segment.controlPoints != null &&
                        segment.controlPoints.Length > 0 &&
                        CanDeleteControlPoint(segment, segment.controlPoints.Length - 1);
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

            if (targets.Length != 1)
            {
                return;
            }

            var segmentTarget = target as RoadSegment;
            var hasSelectedPoint = HasSelectedControlPoint(segmentTarget, out var selectedIndex);
            EditorGUILayout.LabelField(
                "Selected Point",
                hasSelectedPoint ? selectedIndex.ToString() : "None");
            using (new EditorGUI.DisabledScope(!hasSelectedPoint || !CanDeleteControlPoint(segmentTarget, selectedIndex)))
            {
                if (GUILayout.Button("Delete Selected Point"))
                {
                    DeleteControlPoint(segmentTarget, selectedIndex);
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

        private static void DrawControlPointMarkers(RoadSegment segment)
        {
            var points = segment.controlPoints;
            if (points == null)
            {
                return;
            }

            HasSelectedControlPoint(segment, out var selectedIndex);
            for (var i = 0; i < points.Length; i++)
            {
                var worldPosition = segment.transform.TransformPoint(
                    GetControlPointPositionOrDefault(points, i, Vector3.zero));
                var selected = i == selectedIndex;
                var size = CalculateControlPointMarkerSize(worldPosition, selected);
                var oldColor = Handles.color;
                Handles.color = selected
                    ? new Color(1f, 0.75f, 0.15f, 0.95f)
                    : new Color(0.1f, 0.85f, 1f, 0.9f);
                Handles.SphereHandleCap(0, worldPosition, Quaternion.identity, size, EventType.Repaint);
                Handles.Label(worldPosition + Vector3.up * size * 2f, i.ToString());
                Handles.color = oldColor;

                var click = DrawSceneMarkerButton(worldPosition, size);
                if (click == MarkerClickKind.Primary)
                {
                    SetSelectedControlPointIndex(segment, i);
                    SceneView.RepaintAll();
                }
                else if (click == MarkerClickKind.Context)
                {
                    SetSelectedControlPointIndex(segment, i);
                    ShowControlPointContextMenu(segment, i);
                    SceneView.RepaintAll();
                }
            }
        }

        private static float CalculateControlPointMarkerSize(Vector3 worldPosition, bool selected)
        {
            var scaledSize = HandleUtility.GetHandleSize(worldPosition) *
                (selected ? SelectedControlPointMarkerSize : ControlPointMarkerSize);
            var minSize = selected ? SelectedControlPointMarkerMinWorldSize : ControlPointMarkerMinWorldSize;
            return Mathf.Max(scaledSize, minSize);
        }

        private static void DrawSelectedControlPointHandle(RoadSegment segment)
        {
            if (!HasSelectedControlPoint(segment, out var index))
            {
                return;
            }

            var points = segment.controlPoints;
            if (points == null || index < 0 || index >= points.Length)
            {
                return;
            }

            var localPosition = GetControlPointPositionOrDefault(points, index, Vector3.zero);
            var worldPosition = segment.transform.TransformPoint(localPosition);
            EditorGUI.BeginChangeCheck();
            var movedWorldPosition = Handles.PositionHandle(worldPosition, segment.transform.rotation);
            if (EditorGUI.EndChangeCheck())
            {
                RoadSegmentSurfaceGenerator.RegisterGeneratedHierarchyUndo(segment, "Move Road Control Point");
                Undo.RecordObject(segment, "Move Road Control Point");
                var point = GetOrCreateControlPoint(points, index);
                if (point == null)
                {
                    return;
                }

                point.position = segment.transform.InverseTransformPoint(movedWorldPosition);
                EditorUtility.SetDirty(segment);
                RoadNetworkPreviewScheduler.Schedule(segment, true);
            }
        }

        private static void HandleControlPointDeleteKey(RoadSegment segment)
        {
            var evt = Event.current;
            if (evt == null || evt.type != EventType.KeyDown)
            {
                return;
            }

            if (evt.keyCode != KeyCode.Delete && evt.keyCode != KeyCode.Backspace)
            {
                return;
            }

            if (HasSelectedControlPoint(segment, out var index) && DeleteControlPoint(segment, index))
            {
                evt.Use();
            }
        }

        private static MarkerClickKind DrawSceneMarkerButton(Vector3 worldPosition, float size)
        {
            var evt = Event.current;
            if (evt == null)
            {
                return MarkerClickKind.None;
            }

            var controlId = GUIUtility.GetControlID(FocusType.Passive);
            switch (evt.type)
            {
                case EventType.Layout:
                    HandleUtility.AddControl(
                        controlId,
                        HandleUtility.DistanceToCircle(worldPosition, size * MarkerPickScale));
                    break;
                case EventType.MouseDown:
                    if (!evt.alt && HandleUtility.nearestControl == controlId)
                    {
                        if (evt.button == 0 || evt.button == 1)
                        {
                            GUIUtility.hotControl = controlId;
                            var click = evt.button == 0
                                ? MarkerClickKind.Primary
                                : MarkerClickKind.Context;
                            evt.Use();
                            GUIUtility.hotControl = 0;
                            return click;
                        }
                    }
                    break;
                case EventType.MouseUp:
                case EventType.Ignore:
                    if (GUIUtility.hotControl == controlId)
                    {
                        GUIUtility.hotControl = 0;
                    }
                    break;
            }

            return MarkerClickKind.None;
        }

        private static int GetSelectedControlPointIndex(RoadSegment segment)
        {
            if (segment == null)
            {
                return -1;
            }

            return SelectedControlPointIndices.TryGetValue(segment.GetInstanceID(), out var index)
                ? index
                : -1;
        }

        private static void SetSelectedControlPointIndex(RoadSegment segment, int index)
        {
            if (segment == null)
            {
                return;
            }

            var points = segment.controlPoints;
            var segmentId = segment.GetInstanceID();
            if (points == null || points.Length == 0)
            {
                SelectedControlPointIndices.Remove(segmentId);
                return;
            }

            SelectedControlPointIndices[segmentId] = Mathf.Clamp(index, 0, points.Length - 1);
        }

        private static int NormalizeSelectedControlPointIndex(RoadSegment segment)
        {
            if (segment == null)
            {
                return -1;
            }

            var segmentId = segment.GetInstanceID();
            var points = segment.controlPoints;
            if (points == null || points.Length == 0)
            {
                SelectedControlPointIndices.Remove(segmentId);
                return -1;
            }

            if (!SelectedControlPointIndices.TryGetValue(segmentId, out var index))
            {
                return -1;
            }

            if (index < 0)
            {
                SelectedControlPointIndices.Remove(segmentId);
                return -1;
            }

            var normalized = Mathf.Min(index, points.Length - 1);
            SelectedControlPointIndices[segmentId] = normalized;
            return normalized;
        }

        private static bool HasSelectedControlPoint(RoadSegment segment, out int index)
        {
            index = NormalizeSelectedControlPointIndex(segment);
            return index >= 0;
        }

        private static void CleanupStaleSelectedControlPointEntries()
        {
            if (SelectedControlPointIndices.Count == 0)
            {
                return;
            }

            var staleIds = new List<int>();
            foreach (var pair in SelectedControlPointIndices)
            {
                if (EditorUtility.InstanceIDToObject(pair.Key) as RoadSegment == null)
                {
                    staleIds.Add(pair.Key);
                }
            }

            foreach (var id in staleIds)
            {
                SelectedControlPointIndices.Remove(id);
            }
        }

        private static void HandleUndoRedo()
        {
            CleanupStaleSelectedControlPointEntries();
            SceneView.RepaintAll();
        }

        private static void HandleShiftClickAppend(RoadSegment segment)
        {
            var evt = Event.current;
            if (evt == null || evt.alt || evt.type == EventType.Used || GUIUtility.hotControl != 0)
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

        private static Vector3 GetControlPointPositionOrDefault(SplinePoint[] points, int index, Vector3 fallback)
        {
            if (points == null || index < 0 || index >= points.Length)
            {
                return fallback;
            }

            return points[index]?.position ?? fallback;
        }

        private static SplinePoint GetOrCreateControlPoint(SplinePoint[] points, int index)
        {
            if (points == null || index < 0 || index >= points.Length)
            {
                return null;
            }

            return points[index] ?? (points[index] = new SplinePoint());
        }

        private static Vector3[] SnapshotControlPointPositions(SplinePoint[] points)
        {
            if (points == null || points.Length == 0)
            {
                return Array.Empty<Vector3>();
            }

            var positions = new Vector3[points.Length];
            for (var i = 0; i < points.Length; i++)
            {
                positions[i] = GetControlPointPositionOrDefault(points, i, Vector3.zero);
            }

            return positions;
        }

        private static Vector3 CalculatePrependPosition(RoadSegment segment)
        {
            var positions = SnapshotControlPointPositions(segment != null ? segment.controlPoints : null);
            if (positions.Length == 0)
            {
                return Vector3.zero;
            }

            var first = positions[0];
            if (positions.Length >= 2)
            {
                var direction = first - positions[1];
                if (direction.sqrMagnitude > CatmullRomSpline.Epsilon * CatmullRomSpline.Epsilon)
                {
                    return first + direction;
                }
            }

            return first + Vector3.back * 5f;
        }

        private static Vector3 CalculateAppendPosition(RoadSegment segment)
        {
            var positions = SnapshotControlPointPositions(segment != null ? segment.controlPoints : null);
            if (positions.Length == 0)
            {
                return Vector3.zero;
            }

            var last = positions[positions.Length - 1];
            if (positions.Length >= 2)
            {
                var direction = last - positions[positions.Length - 2];
                if (direction.sqrMagnitude > CatmullRomSpline.Epsilon * CatmullRomSpline.Epsilon)
                {
                    return last + direction;
                }
            }

            return last + Vector3.forward * 5f;
        }

        private static Vector3 CalculateShapePreservingInsertPosition(RoadSegment segment, int insertIndex)
        {
            var positions = SnapshotControlPointPositions(segment != null ? segment.controlPoints : null);
            if (positions.Length == 0)
            {
                return Vector3.zero;
            }

            insertIndex = Mathf.Clamp(insertIndex, 0, positions.Length);
            if (insertIndex <= 0)
            {
                return CalculatePrependPosition(segment);
            }
            if (insertIndex >= positions.Length)
            {
                return CalculateAppendPosition(segment);
            }

            var fallbackCandidate = GetChordMidpoint(positions, insertIndex);
            var preferredCandidate = fallbackCandidate;
            var bestPosition = fallbackCandidate;
            var bestError = float.PositiveInfinity;
            TryUseShapePreserveCandidate(positions, insertIndex, fallbackCandidate, ref bestPosition, ref bestError);
            if (TryGetParameterMidpoint(positions, insertIndex, out var parameterMidpoint))
            {
                preferredCandidate = parameterMidpoint;
                TryUseShapePreserveCandidate(positions, insertIndex, parameterMidpoint, ref bestPosition, ref bestError);
            }
            if (TryGetArcLengthMidpoint(positions, insertIndex, out var arcLengthMidpoint))
            {
                preferredCandidate = arcLengthMidpoint;
                TryUseShapePreserveCandidate(positions, insertIndex, arcLengthMidpoint, ref bestPosition, ref bestError);
            }

            if (float.IsInfinity(bestError) || float.IsNaN(bestError))
            {
                return preferredCandidate;
            }

            var step = CalculateShapePreserveInitialStep(positions, insertIndex);
            if (step <= CatmullRomSpline.Epsilon)
            {
                return bestPosition;
            }

            for (var iteration = 0; iteration < ShapePreserveSearchIterations; iteration++)
            {
                foreach (var direction in ShapePreserveSearchDirections)
                {
                    var candidate = bestPosition + direction * step;
                    var error = EvaluateInsertionShapeError(positions, insertIndex, candidate);
                    if (!float.IsNaN(error) && error < bestError)
                    {
                        bestError = error;
                        bestPosition = candidate;
                    }
                }

                step *= 0.5f;
            }

            return bestPosition;
        }

        private static void TryUseShapePreserveCandidate(
            Vector3[] positions,
            int insertIndex,
            Vector3 candidate,
            ref Vector3 bestPosition,
            ref float bestError)
        {
            var error = EvaluateInsertionShapeError(positions, insertIndex, candidate);
            if (float.IsInfinity(error) || float.IsNaN(error) || error >= bestError)
            {
                return;
            }

            bestPosition = candidate;
            bestError = error;
        }

        private static float CalculateShapePreserveInitialStep(Vector3[] positions, int insertIndex)
        {
            if (positions == null || insertIndex <= 0 || insertIndex >= positions.Length)
            {
                return 0f;
            }

            var adjacentDistance = Vector3.Distance(positions[insertIndex - 1], positions[insertIndex]);
            var influenceLength = 0f;
            var spline = new CatmullRomSpline(positions);
            if (spline.IsValid)
            {
                var table = spline.BuildArcLengthTable(ShapePreserveArcLengthSamplesPerSegment);
                var startParameter = Mathf.Clamp(insertIndex - 2f, 0f, spline.MaxParameter);
                var endParameter = Mathf.Clamp(insertIndex + 1f, 0f, spline.MaxParameter);
                influenceLength = EstimateDistanceAtParameter(table, endParameter) -
                    EstimateDistanceAtParameter(table, startParameter);
            }

            return Mathf.Max(adjacentDistance * 0.25f, influenceLength * 0.08f);
        }

        private static float EvaluateInsertionShapeError(Vector3[] oldPositions, int insertIndex, Vector3 candidate)
        {
            if (oldPositions == null || oldPositions.Length < 2 || insertIndex <= 0 || insertIndex >= oldPositions.Length)
            {
                return float.PositiveInfinity;
            }

            var oldSpline = new CatmullRomSpline(oldPositions);
            var newSpline = new CatmullRomSpline(InsertPosition(oldPositions, insertIndex, candidate));
            if (!oldSpline.IsValid || !newSpline.IsValid)
            {
                return float.PositiveInfinity;
            }

            var oldTable = oldSpline.BuildArcLengthTable(ShapePreserveArcLengthSamplesPerSegment);
            var newTable = newSpline.BuildArcLengthTable(ShapePreserveArcLengthSamplesPerSegment);
            var leftIndex = insertIndex - 1;
            var rightIndex = insertIndex;
            var oldStartParameter = Mathf.Clamp(leftIndex - 1f, 0f, oldSpline.MaxParameter);
            var oldEndParameter = Mathf.Clamp(rightIndex + 1f, 0f, oldSpline.MaxParameter);
            var newStartParameter = oldStartParameter >= insertIndex ? oldStartParameter + 1f : oldStartParameter;
            var newEndParameter = oldEndParameter >= insertIndex ? oldEndParameter + 1f : oldEndParameter;
            newStartParameter = Mathf.Clamp(newStartParameter, 0f, newSpline.MaxParameter);
            newEndParameter = Mathf.Clamp(newEndParameter, 0f, newSpline.MaxParameter);

            var oldStartDistance = EstimateDistanceAtParameter(oldTable, oldStartParameter);
            var oldEndDistance = EstimateDistanceAtParameter(oldTable, oldEndParameter);
            var newStartDistance = EstimateDistanceAtParameter(newTable, newStartParameter);
            var newEndDistance = EstimateDistanceAtParameter(newTable, newEndParameter);
            if (oldEndDistance - oldStartDistance <= CatmullRomSpline.Epsilon ||
                newEndDistance - newStartDistance <= CatmullRomSpline.Epsilon)
            {
                return float.PositiveInfinity;
            }

            var error = 0f;
            for (var sampleIndex = 0; sampleIndex < ShapePreserveSampleCount; sampleIndex++)
            {
                var t = ShapePreserveSampleCount == 1
                    ? 0f
                    : sampleIndex / (float)(ShapePreserveSampleCount - 1);
                var oldDistance = Mathf.Lerp(oldStartDistance, oldEndDistance, t);
                var newDistance = Mathf.Lerp(newStartDistance, newEndDistance, t);
                var oldPosition = oldTable.SampleByDistance(oldDistance).position;
                var newPosition = newTable.SampleByDistance(newDistance).position;
                error += (oldPosition - newPosition).sqrMagnitude;
            }

            return error / ShapePreserveSampleCount;
        }

        private static float EstimateDistanceAtParameter(CatmullRomArcLengthTable table, float parameter)
        {
            if (table == null || table.TotalLengthMeters <= CatmullRomSpline.Epsilon)
            {
                return 0f;
            }

            var minParameter = table.ParameterAtDistance(0f);
            var maxParameter = table.ParameterAtDistance(table.TotalLengthMeters);
            var targetParameter = Mathf.Clamp(parameter, minParameter, maxParameter);
            var low = 0f;
            var high = table.TotalLengthMeters;
            for (var i = 0; i < DistanceAtParameterSearchIterations; i++)
            {
                var mid = (low + high) * 0.5f;
                if (table.ParameterAtDistance(mid) < targetParameter)
                {
                    low = mid;
                }
                else
                {
                    high = mid;
                }
            }

            return (low + high) * 0.5f;
        }

        private static bool TryGetArcLengthMidpoint(Vector3[] positions, int insertIndex, out Vector3 midpoint)
        {
            midpoint = Vector3.zero;
            if (positions == null || positions.Length < 2 || insertIndex <= 0 || insertIndex >= positions.Length)
            {
                return false;
            }

            var spline = new CatmullRomSpline(positions);
            if (!spline.IsValid)
            {
                return false;
            }

            var table = spline.BuildArcLengthTable(ShapePreserveArcLengthSamplesPerSegment);
            if (table.TotalLengthMeters <= CatmullRomSpline.Epsilon)
            {
                return false;
            }

            var startDistance = EstimateDistanceAtParameter(table, insertIndex - 1f);
            var endDistance = EstimateDistanceAtParameter(table, insertIndex);
            if (endDistance - startDistance <= CatmullRomSpline.Epsilon)
            {
                return false;
            }

            midpoint = table.SampleByDistance((startDistance + endDistance) * 0.5f).position;
            return true;
        }

        private static bool TryGetParameterMidpoint(Vector3[] positions, int insertIndex, out Vector3 midpoint)
        {
            midpoint = Vector3.zero;
            if (positions == null || positions.Length < 2 || insertIndex <= 0 || insertIndex >= positions.Length)
            {
                return false;
            }

            var spline = new CatmullRomSpline(positions);
            if (!spline.IsValid)
            {
                return false;
            }

            midpoint = spline.EvaluatePosition(insertIndex - 0.5f);
            return true;
        }

        private static Vector3 GetChordMidpoint(Vector3[] positions, int insertIndex)
        {
            if (positions == null || positions.Length == 0)
            {
                return Vector3.zero;
            }

            insertIndex = Mathf.Clamp(insertIndex, 1, positions.Length - 1);
            return (positions[insertIndex - 1] + positions[insertIndex]) * 0.5f;
        }

        private static Vector3[] InsertPosition(Vector3[] positions, int insertIndex, Vector3 candidate)
        {
            var newPositions = new Vector3[positions.Length + 1];
            for (var i = 0; i < insertIndex; i++)
            {
                newPositions[i] = positions[i];
            }

            newPositions[insertIndex] = candidate;
            for (var i = insertIndex; i < positions.Length; i++)
            {
                newPositions[i + 1] = positions[i];
            }

            return newPositions;
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

        private static bool PrependControlPoint(RoadSegment segment)
        {
            return InsertControlPoint(
                segment,
                0,
                CalculatePrependPosition(segment),
                "Prepend Road Control Point");
        }

        private static void AppendControlPoint(RoadSegment segment)
        {
            AppendControlPoint(segment, CalculateAppendPosition(segment));
        }

        private static void AppendControlPoint(RoadSegment segment, Vector3 localPosition)
        {
            InsertControlPoint(
                segment,
                segment != null ? segment.controlPoints?.Length ?? 0 : 0,
                localPosition,
                "Append Road Control Point");
        }

        private static bool InsertBeforeControlPoint(RoadSegment segment, int index)
        {
            if (segment == null)
            {
                return false;
            }

            var points = segment.controlPoints;
            var length = points?.Length ?? 0;
            if (length == 0 || index <= 0)
            {
                return PrependControlPoint(segment);
            }

            var insertIndex = Mathf.Clamp(index, 0, length);
            return InsertControlPoint(
                segment,
                insertIndex,
                CalculateShapePreservingInsertPosition(segment, insertIndex),
                "Insert Road Control Point");
        }

        private static bool InsertAfterControlPoint(RoadSegment segment, int index)
        {
            if (segment == null)
            {
                return false;
            }

            var points = segment.controlPoints;
            var length = points?.Length ?? 0;
            if (length == 0 || index >= length - 1)
            {
                AppendControlPoint(segment);
                return true;
            }

            var insertIndex = Mathf.Clamp(index + 1, 0, length);
            return InsertControlPoint(
                segment,
                insertIndex,
                CalculateShapePreservingInsertPosition(segment, insertIndex),
                "Insert Road Control Point");
        }

        private static bool InsertControlPoint(RoadSegment segment, int insertIndex, Vector3 localPosition)
        {
            return InsertControlPoint(segment, insertIndex, localPosition, "Insert Road Control Point");
        }

        private static bool InsertControlPoint(
            RoadSegment segment,
            int insertIndex,
            Vector3 localPosition,
            string undoName)
        {
            if (segment == null)
            {
                return false;
            }

            var oldPoints = segment.controlPoints ?? Array.Empty<SplinePoint>();
            insertIndex = Mathf.Clamp(insertIndex, 0, oldPoints.Length);
            RoadSegmentSurfaceGenerator.RegisterGeneratedHierarchyUndo(segment, undoName);
            Undo.RecordObject(segment, undoName);
            var newPoints = new SplinePoint[oldPoints.Length + 1];
            for (var i = 0; i < insertIndex; i++)
            {
                newPoints[i] = oldPoints[i];
            }

            newPoints[insertIndex] = new SplinePoint(localPosition);
            for (var i = insertIndex; i < oldPoints.Length; i++)
            {
                newPoints[i + 1] = oldPoints[i];
            }

            segment.controlPoints = newPoints;
            SetSelectedControlPointIndex(segment, insertIndex);
            EditorUtility.SetDirty(segment);
            RoadNetworkPreviewScheduler.Schedule(segment, true);
            SceneView.RepaintAll();
            return true;
        }

        private static void DeleteLastControlPoint(RoadSegment segment)
        {
            if (segment?.controlPoints == null)
            {
                return;
            }

            DeleteControlPoint(segment, segment.controlPoints.Length - 1);
        }

        private static bool DeleteControlPoint(RoadSegment segment, int deleteIndex)
        {
            if (!CanDeleteControlPoint(segment, deleteIndex))
            {
                return false;
            }

            var oldPoints = segment.controlPoints;
            RoadSegmentSurfaceGenerator.RegisterGeneratedHierarchyUndo(segment, "Delete Road Control Point");
            Undo.RecordObject(segment, "Delete Road Control Point");
            var newPoints = new SplinePoint[oldPoints.Length - 1];
            var newIndex = 0;
            for (var i = 0; i < oldPoints.Length; i++)
            {
                if (i == deleteIndex)
                {
                    continue;
                }

                newPoints[newIndex] = oldPoints[i];
                newIndex++;
            }

            segment.controlPoints = newPoints;
            if (newPoints.Length > 0)
            {
                SetSelectedControlPointIndex(segment, Mathf.Clamp(deleteIndex - 1, 0, newPoints.Length - 1));
            }
            else
            {
                SelectedControlPointIndices.Remove(segment.GetInstanceID());
            }

            EditorUtility.SetDirty(segment);
            RoadNetworkPreviewScheduler.Schedule(segment, true);
            SceneView.RepaintAll();
            return true;
        }

        private static bool CanDeleteControlPoint(RoadSegment segment, int index)
        {
            if (segment == null || segment.controlPoints == null || segment.controlPoints.Length <= 2)
            {
                return false;
            }

            if (index < 0 || index >= segment.controlPoints.Length)
            {
                return false;
            }

            if (index == 0 && segment.startConnection != null && segment.startConnection.IsConnected)
            {
                return false;
            }

            if (index == segment.controlPoints.Length - 1 &&
                segment.endConnection != null &&
                segment.endConnection.IsConnected)
            {
                return false;
            }

            return true;
        }

        private static void ShowControlPointContextMenu(RoadSegment segment, int index)
        {
            var menu = new GenericMenu();
            menu.AddItem(
                new GUIContent("Insert Before"),
                false,
                () => InsertBeforeControlPoint(segment, index));
            menu.AddItem(
                new GUIContent("Insert After"),
                false,
                () => InsertAfterControlPoint(segment, index));
            if (CanDeleteControlPoint(segment, index))
            {
                menu.AddItem(
                    new GUIContent("Delete Point"),
                    false,
                    () => DeleteControlPoint(segment, index));
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Delete Point"));
            }

            menu.AddItem(
                new GUIContent("Frame Selected"),
                false,
                () => FrameControlPoint(segment, index));
            menu.ShowAsContext();
        }

        private static bool FrameControlPoint(RoadSegment segment, int index)
        {
            if (segment == null || segment.controlPoints == null || index < 0 || index >= segment.controlPoints.Length)
            {
                return false;
            }

            var sceneView = SceneView.lastActiveSceneView ?? SceneView.currentDrawingSceneView;
            if (sceneView == null)
            {
                return false;
            }

            var worldPosition = segment.transform.TransformPoint(
                GetControlPointPositionOrDefault(segment.controlPoints, index, Vector3.zero));
            var size = Mathf.Max(HandleUtility.GetHandleSize(worldPosition), 0.5f);
            sceneView.Frame(new Bounds(worldPosition, Vector3.one * size), false);
            return true;
        }

        private void ScheduleTargets()
        {
            foreach (var selectedTarget in targets)
            {
                RoadNetworkPreviewScheduler.Schedule((RoadSegment)selectedTarget, true);
            }
        }

        private void ApplyTargetsGeneratedLayers(bool registerUndo)
        {
            foreach (var selectedTarget in targets)
            {
                RoadSegmentSurfaceGenerator.ApplyGeneratedLayers((RoadSegment)selectedTarget, registerUndo);
            }
        }

        private void RegisterTargetsGeneratedHierarchyUndo(string undoName)
        {
            foreach (var selectedTarget in targets)
            {
                RoadSegmentSurfaceGenerator.RegisterGeneratedHierarchyUndo((RoadSegment)selectedTarget, undoName);
            }
        }

        private int GetInheritedSurfaceLayer()
        {
            if (targets.Length != 1)
            {
                return RoadGeneratedLayerSettings.DefaultLayer;
            }

            var segment = target as RoadSegment;
            return RoadGeneratedLayerSettings.ResolveSurfaceLayer(null, segment != null ? segment.Network : null);
        }

        private int GetInheritedMarkingLayer()
        {
            if (targets.Length != 1)
            {
                return RoadGeneratedLayerSettings.DefaultLayer;
            }

            var segment = target as RoadSegment;
            return RoadGeneratedLayerSettings.ResolveMarkingLayer(null, segment != null ? segment.Network : null);
        }

        private bool GetInheritedGenerateSurfaceColliders(out bool mixedValue)
        {
            mixedValue = false;
            var hasValue = false;
            var inheritedValue = RoadSurfaceColliderSettings.DefaultGenerateSurfaceColliders;

            foreach (var selectedTarget in targets)
            {
                var segment = selectedTarget as RoadSegment;
                var currentValue = RoadSurfaceColliderSettings.ResolveGenerateSurfaceColliders(
                    null,
                    segment != null ? segment.Network : null);
                if (!hasValue)
                {
                    inheritedValue = currentValue;
                    hasValue = true;
                    continue;
                }

                if (currentValue != inheritedValue)
                {
                    mixedValue = true;
                    return inheritedValue;
                }
            }

            return inheritedValue;
        }
    }
}
#endif

#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MitarashiDango.RoadAssetGenerator
{
    /// <summary>
    /// <see cref="RoadConfig"/> の編集とテクスチャ・マテリアルのベイクを行うメインのエディタウィンドウ。
    /// UI Toolkit アセット (<c>RoadAssetGeneratorWindow.uxml</c>) と、レーンや境界線を動的に組み立てる
    /// セクションで構成される。
    /// </summary>
    public class RoadAssetGeneratorWindow : EditorWindow
    {
        // 現在バインドされているプリセットアセット。ユーザが選択していない場合は永続化されない
        // 一時アセット (HideFlags.DontSave) を指す。
        [SerializeField] private RoadConfigAsset currentAsset;
        private RoadConfigAsset tempAsset;
        private SerializedObject serializedObject;

        // CreateGUI で解決した UI 参照のキャッシュ。
        private ObjectField presetField;
        private VisualElement lanesContainer;
        private VisualElement linesContainer;
        private IMGUIContainer previewContainer;
        private Label infoLabel;
        private Image albedoPreview;
        private Texture2D previewTex;

        [MenuItem("Tools/Road Asset Generator/Open Window")]
        public static void Open()
        {
            var w = GetWindow<RoadAssetGeneratorWindow>("Road Asset Gen");
            w.minSize = new Vector2(420, 600);
        }

        // -----------------------------------------------------------------
        // CreateGUI:UI Toolkit のメインエントリポイント
        // -----------------------------------------------------------------
        public void CreateGUI()
        {
            var root = rootVisualElement;
            root.Clear();

            var uxml = LoadUxml();
            if (uxml == null)
            {
                root.Add(new Label("Could not locate RoadAssetGeneratorWindow.uxml. Ensure the UI folder is intact."));
                return;
            }
            uxml.CloneTree(root);

            presetField     = root.Q<ObjectField>("preset-field");
            lanesContainer  = root.Q<VisualElement>("lanes-container");
            linesContainer  = root.Q<VisualElement>("lines-container");
            previewContainer = root.Q<IMGUIContainer>("preview-container");
            infoLabel       = root.Q<Label>("info-label");
            albedoPreview   = root.Q<Image>("albedo-preview");

            presetField.objectType = typeof(RoadConfigAsset);

            EnsureCurrentAsset();
            BindToAsset(currentAsset);

            presetField.RegisterValueChangedCallback(evt =>
            {
                var asset = evt.newValue as RoadConfigAsset;
                if (asset != null)
                {
                    currentAsset = asset;
                }
                else
                {
                    currentAsset = null;
                    EnsureCurrentAsset();
                }
                BindToAsset(currentAsset);
            });

            WireButtons(root);

            previewContainer.onGUIHandler = DrawCrossSectionIMGUI;
        }

        private void WireButtons(VisualElement root)
        {
            root.Q<Button>("save-as-preset-button").clicked += SaveCurrentAsNewPreset;
            root.Q<Button>("create-defaults-button").clicked += () => RoadPresetMenu.CreateBuiltInPresets();

            root.Q<Button>("preset-no-overtaking").clicked += () => ApplyPreset(RoadConfig.PresetMountainRoad_NoOvertaking());
            root.Q<Button>("preset-passing-ok").clicked    += () => ApplyPreset(RoadConfig.PresetMountainRoad_PassingOK());
            root.Q<Button>("preset-four-lane").clicked     += () => ApplyPreset(RoadConfig.PresetFourLane());
            root.Q<Button>("preset-narrow").clicked        += () => ApplyPreset(RoadConfig.PresetNarrowLane15());
            root.Q<Button>("preset-single-lane").clicked   += () => ApplyPreset(RoadConfig.PresetSingleLane());
            root.Q<Button>("preset-no-markings").clicked   += () => ApplyPreset(RoadConfig.PresetNoLaneMarkings());

            root.Q<Button>("output-folder-browse").clicked += BrowseOutputFolder;
            root.Q<Button>("add-lane-button").clicked += AddLane;

            var resetBtn = root.Q<Button>("reset-button");
            if (resetBtn != null)
            {
                resetBtn.clicked += ResetCurrentToDefault;
            }

            root.Q<Button>("refresh-preview-button").clicked += RefreshAlbedoPreview;
            root.Q<Button>("generate-button").clicked += OnGenerate;
        }

        // -----------------------------------------------------------------
        // アセットバインディング
        // -----------------------------------------------------------------
        private bool IsUsingTempAsset => currentAsset != null && currentAsset == tempAsset;

        private void EnsureCurrentAsset()
        {
            if (currentAsset != null)
            {
                return;
            }

            if (tempAsset == null)
            {
                tempAsset = ScriptableObject.CreateInstance<RoadConfigAsset>();
                tempAsset.name = "(Unsaved Preset)";
                // DontSave で永続化を抑制しつつ、SerializedObject 経由の通常の編集は可能にする。
                tempAsset.hideFlags = HideFlags.DontSave;
                tempAsset.config = RoadConfig.PresetMountainRoad_NoOvertaking();
            }
            currentAsset = tempAsset;
        }

        private void BindToAsset(RoadConfigAsset asset)
        {
            if (asset == null)
            {
                EnsureCurrentAsset();
                asset = currentAsset;
            }

            currentAsset = asset;
            currentAsset.config.EnsureLineCount();
            serializedObject = new SerializedObject(currentAsset);

            // 子フィールドが見た目上 disabled に見えないよう、ObjectField には常に実バインド先を表示する
            // (一時アセットの場合も含む)。
            if (presetField != null && !ReferenceEquals(presetField.value, asset))
            {
                presetField.SetValueWithoutNotify(asset);
            }

            UpdateTempAssetIndicator();

            rootVisualElement.Bind(serializedObject);
            RebuildLanesSection();
            RebuildLinesSection();
            UpdateInfoLabel();

            var lanesProp = serializedObject.FindProperty("config.lanes");
            rootVisualElement.TrackPropertyValue(lanesProp, _ =>
            {
                if (currentAsset == null)
                {
                    return;
                }
                currentAsset.config.EnsureLineCount();
                serializedObject.Update();
                RebuildLinesSection();
                UpdateInfoLabel();
            });

            rootVisualElement.TrackSerializedObjectValue(serializedObject, _ => UpdateInfoLabel());
        }

        private void UpdateTempAssetIndicator()
        {
            var indicator = rootVisualElement.Q<Label>("temp-asset-indicator");
            if (indicator != null)
            {
                indicator.style.display = IsUsingTempAsset ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private void UpdateInfoLabel()
        {
            if (infoLabel == null || currentAsset == null)
            {
                return;
            }
            var c = currentAsset.config;
            infoLabel.text = $"Total width: {c.TotalWidthMeters:F2}m   |   Tile length: {c.output.textureLengthMeters:F1}m   |   Lanes: {c.lanes.Count}";
        }

        // -----------------------------------------------------------------
        // Lanes セクション(手動再構築)
        // -----------------------------------------------------------------
        private void RebuildLanesSection()
        {
            lanesContainer.Clear();
            serializedObject.Update();
            var lanesProp = serializedObject.FindProperty("config.lanes");

            for (var i = 0; i < lanesProp.arraySize; i++)
            {
                var idx = i;
                var laneProp = lanesProp.GetArrayElementAtIndex(idx);
                lanesContainer.Add(BuildLaneRow(idx, laneProp));
            }
        }

        private VisualElement BuildLaneRow(int idx, SerializedProperty laneProp)
        {
            var wrapper = new VisualElement();
            wrapper.AddToClassList("lane-foldout-wrapper");

            var header = new VisualElement();
            header.AddToClassList("lane-foldout-header");

            var foldout = new Foldout
            {
                text = FormatLaneFoldoutTitle(idx, laneProp),
                value = false,
            };
            foldout.AddToClassList("lane-foldout");
            foldout.style.flexGrow = 1;

            var totalLanes = serializedObject.FindProperty("config.lanes").arraySize;
            var upBtn = new Button(() => MoveLane(idx, -1)) { text = "▲", tooltip = "Move up" };
            upBtn.AddToClassList("reorder-button");
            upBtn.SetEnabled(idx > 0);

            var downBtn = new Button(() => MoveLane(idx, +1)) { text = "▼", tooltip = "Move down" };
            downBtn.AddToClassList("reorder-button");
            downBtn.SetEnabled(idx < totalLanes - 1);

            var removeBtn = new Button(() => RemoveLane(idx)) { text = "×" };
            removeBtn.AddToClassList("remove-button");

            header.Add(foldout);
            header.Add(upBtn);
            header.Add(downBtn);
            header.Add(removeBtn);
            wrapper.Add(header);

            // ラベル / 幅などの基本フィールドはヘッダではなく Foldout 本体に置くことで、ヘッダ行が
            // 横方向に膨張するのを防ぐ。内部ラベルを持たせて、サブセクションのフィールドと見た目を
            // 揃える。
            var labelField = new TextField("Label");
            labelField.BindProperty(laneProp.FindPropertyRelative("label"));
            labelField.RegisterValueChangedCallback(_ => foldout.text = FormatLaneFoldoutTitle(idx, laneProp));
            foldout.Add(labelField);

            var widthField = new FloatField("Width (m)");
            widthField.BindProperty(laneProp.FindPropertyRelative("widthMeters"));
            widthField.RegisterValueChangedCallback(_ => foldout.text = FormatLaneFoldoutTitle(idx, laneProp));
            foldout.Add(widthField);

            var directionField = new EnumField("Direction");
            directionField.tooltip = "Traffic flow along V axis. Forward = V+ (away). Backward = V- (oncoming). Diamond markers' slant is auto-flipped for Backward lanes so chevrons point in the direction of travel.";
            directionField.BindProperty(laneProp.FindPropertyRelative("direction"));
            directionField.RegisterValueChangedCallback(_ => foldout.text = FormatLaneFoldoutTitle(idx, laneProp));
            foldout.Add(directionField);

            BuildLaneTintSection(foldout, laneProp);
            BuildLaneRumbleSection(foldout, laneProp);
            BuildLaneSpeedReductionDotLineSection(foldout, laneProp);

            return wrapper;
        }

        private static string FormatLaneFoldoutTitle(int idx, SerializedProperty laneProp)
        {
            var labelStr = laneProp.FindPropertyRelative("label").stringValue;
            var widthVal = laneProp.FindPropertyRelative("widthMeters").floatValue;
            var dirIdx = laneProp.FindPropertyRelative("direction").enumValueIndex;
            var arrow = (LaneDirection)dirIdx == LaneDirection.Forward ? " ↑" : " ↓";
            return $"[{idx}] {labelStr} ({widthVal:F2}m){arrow}";
        }

        private void BuildLaneTintSection(Foldout parent, SerializedProperty laneProp)
        {
            var section = NewLaneSubSection("Surface Tint (路面色)");

            var enableToggle = new Toggle("Enable");
            enableToggle.BindProperty(laneProp.FindPropertyRelative("surfaceTint"));
            section.Add(enableToggle);

            var colorField = new ColorField("Tint Color");
            colorField.BindProperty(laneProp.FindPropertyRelative("surfaceTintColor"));
            section.Add(colorField);

            var strengthField = new Slider("Tint Strength", 0f, 1f) { showInputField = true };
            strengthField.BindProperty(laneProp.FindPropertyRelative("surfaceTintStrength"));
            section.Add(strengthField);

            void UpdateVisibility()
            {
                var on = laneProp.FindPropertyRelative("surfaceTint").boolValue;
                colorField.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
                strengthField.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
            }
            UpdateVisibility();
            enableToggle.RegisterValueChangedCallback(_ => UpdateVisibility());

            parent.Add(section);
        }

        private void BuildLaneRumbleSection(Foldout parent, SerializedProperty laneProp)
        {
            var section = NewLaneSubSection("Rumble Strips (路面凹凸舗装)");

            var enableToggle = new Toggle("Enable");
            enableToggle.BindProperty(laneProp.FindPropertyRelative("rumbleStrip"));
            section.Add(enableToggle);

            var detailGroup = new VisualElement();

            var widthField = AddBoundFloat(detailGroup, laneProp, "rumbleStripWidthMeters",  "Stripe Thickness (m)",
                "V軸方向(進行方向)の帯1本の太さ。鋪装に塗られた帯の厚みに相当。");
            var spacingField = AddBoundFloat(detailGroup, laneProp, "rumbleStripSpacingMeters", "Stripe Spacing (m)",
                "V軸方向(進行方向)の帯と帯の間隔(中心間ではなく gap の長さ)。");
            AddBoundColor(detailGroup, laneProp, "rumbleStripColor", "Stripe Color");
            AddBoundFloat(detailGroup, laneProp, "rumbleStripStartOffsetMeters", "Start Offset (m)",
                "タイル先頭(V=0)から最初の帯の開始位置までのオフセット。spacing/2 に設定するとパターンがタイル内中央配置になる。");
            AddBoundFloat(detailGroup, laneProp, "rumbleStripInsetMeters", "Edge Inset (m)",
                "車線端から帯の U 軸方向の内側終端までの距離。区画線と帯が重ならないようにスペースを作る。");
            AddBoundFloat(detailGroup, laneProp, "rumbleStripPaintHeightFactor", "Paint Height Factor",
                "Rumble strip paint thickness contribution to the normal map (default 1.5 — thicker than thin painted lines). Modulated by Weathering.PaintHeightStrength and Line Edge Wear.");

            var (tileWarn, snapBtn) = AddTileWarnAndSnap(detailGroup,
                "Snap & center pattern",
                "Adjust stripe spacing so the period divides the texture tile evenly, and set the start offset so stripes are centered (half-gap on each tile boundary).",
                () => SnapRumbleToTile(laneProp));

            section.Add(detailGroup);

            void UpdateTileWarning() => UpdateRumbleStyleTileWarning(laneProp, tileWarn, snapBtn);
            void UpdateVisibility()
            {
                var on = laneProp.FindPropertyRelative("rumbleStrip").boolValue;
                detailGroup.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
                UpdateTileWarning();
            }

            UpdateVisibility();
            enableToggle.RegisterValueChangedCallback(_ => UpdateVisibility());
            widthField.RegisterValueChangedCallback(_ => UpdateTileWarning());
            spacingField.RegisterValueChangedCallback(_ => UpdateTileWarning());
            snapBtn.clicked += UpdateTileWarning;

            TrackTileLengthChange(section, laneProp.serializedObject, UpdateTileWarning);

            parent.Add(section);
        }

        private void BuildLaneSpeedReductionDotLineSection(Foldout parent, SerializedProperty laneProp)
        {
            var section = NewLaneSubSection("Speed Reduction Dot Line (減速ドットライン)");

            var enableToggle = new Toggle("Enable");
            enableToggle.BindProperty(laneProp.FindPropertyRelative("speedReductionDotLine"));
            section.Add(enableToggle);

            var detailGroup = new VisualElement();

            AddBoundFloat(detailGroup, laneProp, "speedReductionDotLineWidthMeters",  "Dot Width (m)");
            var heightField = AddBoundFloat(detailGroup, laneProp, "speedReductionDotLineHeightMeters", "Dot Height (m)");
            var spacingField = AddBoundFloat(detailGroup, laneProp, "speedReductionDotLineSpacingMeters", "Dot Spacing (m)");
            AddBoundColor(detailGroup, laneProp, "speedReductionDotLineColor", "Dot Color");
            AddBoundFloat(detailGroup, laneProp, "speedReductionDotLineSlantMeters", "Slant (m)",
                "V-axis offset between the dot's two columns. Positive: the lane-interior side tilts toward the traffic direction (the dot leans in the direction of travel). Negative: opposite. 0: rectangle. With Side=Both the right-side stroke is auto-mirrored, and Backward lanes auto-flip the slant — so a single positive value gives the correct visual on both lanes of a 2-way road.");
            AddBoundFloat(detailGroup, laneProp, "speedReductionDotLineInsetMeters", "Edge Inset (m)",
                "Distance from the chosen lane edge to the dot's nearest edge. Use this to leave space between the dot line and the centerline / shoulder.");
            AddBoundEnum(detailGroup, laneProp, "speedReductionDotLineSide", "Side",
                "Which lane edge(s) to place the dot line on. 'Both' generates two columns (one near each edge) sharing the same slant.");
            AddBoundFloat(detailGroup, laneProp, "speedReductionDotLineStartOffsetMeters", "Start Offset (m)",
                "V-axis phase offset within one tile period.");
            AddBoundFloat(detailGroup, laneProp, "speedReductionDotLinePaintHeightFactor", "Paint Height Factor",
                "Speed reduction dot line paint thickness contribution to the normal map. Modulated by Weathering.PaintHeightStrength and Line Edge Wear.");

            var (tileWarn, snapBtn) = AddTileWarnAndSnap(detailGroup,
                "Snap & center pattern",
                "Adjust dot spacing so the period divides the texture tile evenly, and set the start offset so dots are centered (half-gap on each tile boundary).",
                () => SnapLaneSpeedReductionDotLineToTile(laneProp));

            section.Add(detailGroup);

            void UpdateTileWarning() => UpdateSpeedReductionDotLineTileWarning(laneProp, tileWarn, snapBtn);
            void UpdateVisibility()
            {
                var on = laneProp.FindPropertyRelative("speedReductionDotLine").boolValue;
                detailGroup.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
                UpdateTileWarning();
            }

            UpdateVisibility();
            enableToggle.RegisterValueChangedCallback(_ => UpdateVisibility());
            heightField.RegisterValueChangedCallback(_ => UpdateTileWarning());
            spacingField.RegisterValueChangedCallback(_ => UpdateTileWarning());
            snapBtn.clicked += UpdateTileWarning;

            TrackTileLengthChange(section, laneProp.serializedObject, UpdateTileWarning);

            parent.Add(section);
        }

        private void AddLane()
        {
            Undo.RecordObject(currentAsset, "Add Lane");
            currentAsset.config.lanes.Add(new LaneConfig
            {
                label = $"Lane {currentAsset.config.lanes.Count + 1}",
                widthMeters = 3.0f,
            });
            currentAsset.config.EnsureLineCount();
            EditorUtility.SetDirty(currentAsset);
            serializedObject.Update();
            RebuildLanesSection();
            RebuildLinesSection();
            UpdateInfoLabel();
        }

        private void MoveLane(int index, int direction)
        {
            if (currentAsset == null)
            {
                return;
            }
            var lanes = currentAsset.config.lanes;
            var newIndex = index + direction;
            if (newIndex < 0 || newIndex >= lanes.Count)
            {
                return;
            }
            Undo.RecordObject(currentAsset, "Reorder Lane");
            (lanes[index], lanes[newIndex]) = (lanes[newIndex], lanes[index]);
            EditorUtility.SetDirty(currentAsset);
            serializedObject.Update();
            RebuildLanesSection();
        }

        private void RemoveLane(int index)
        {
            if (currentAsset.config.lanes.Count <= 1)
            {
                EditorUtility.DisplayDialog("Road Asset Generator", "At least one lane is required.", "OK");
                return;
            }
            Undo.RecordObject(currentAsset, "Remove Lane");
            currentAsset.config.lanes.RemoveAt(index);
            currentAsset.config.EnsureLineCount();
            EditorUtility.SetDirty(currentAsset);
            serializedObject.Update();
            RebuildLanesSection();
            RebuildLinesSection();
            UpdateInfoLabel();
        }

        // -----------------------------------------------------------------
        // Lines セクション(Foldout として手動再構築)
        // -----------------------------------------------------------------
        private void RebuildLinesSection()
        {
            linesContainer.Clear();
            serializedObject.Update();
            var linesProp = serializedObject.FindProperty("config.lines");

            for (var i = 0; i < linesProp.arraySize; i++)
            {
                var lineProp = linesProp.GetArrayElementAtIndex(i);
                linesContainer.Add(BuildLineRow(i, lineProp));
            }
        }

        // 各 line foldout は、並べ替え/削除ボタン付きの動的な stroke リストと「Add Stroke」ボタンを表示する。
        private VisualElement BuildLineRow(int index, SerializedProperty lineProp)
        {
            var labelStr = lineProp.FindPropertyRelative("label").stringValue;

            var wrapper = new VisualElement();
            wrapper.AddToClassList("line-foldout-wrapper");

            var header = new VisualElement();
            header.AddToClassList("line-foldout-header");

            var foldout = new Foldout
            {
                text = $"[{index}] {labelStr}",
                value = index == 0 || index == 1,
            };
            foldout.AddToClassList("line-foldout");
            foldout.style.flexGrow = 1;

            var totalLines = serializedObject.FindProperty("config.lines").arraySize;
            var upBtn = new Button(() => MoveLine(index, -1)) { text = "▲", tooltip = "Move up" };
            upBtn.AddToClassList("reorder-button");
            upBtn.SetEnabled(index > 0);

            var downBtn = new Button(() => MoveLine(index, +1)) { text = "▼", tooltip = "Move down" };
            downBtn.AddToClassList("reorder-button");
            downBtn.SetEnabled(index < totalLines - 1);

            header.Add(foldout);
            header.Add(upBtn);
            header.Add(downBtn);
            wrapper.Add(header);

            var labelField = new TextField("Label");
            labelField.BindProperty(lineProp.FindPropertyRelative("label"));
            labelField.RegisterValueChangedCallback(evt => foldout.text = $"[{index}] {evt.newValue}");
            foldout.Add(labelField);

            var strokesContainer = new VisualElement();
            strokesContainer.AddToClassList("strokes-container");
            foldout.Add(strokesContainer);
            BuildStrokesList(strokesContainer, index, lineProp);

            var addStrokeBtn = new Button(() => AddStroke(index)) { text = "+ Add Stroke" };
            addStrokeBtn.AddToClassList("add-stroke-button");
            foldout.Add(addStrokeBtn);

            return wrapper;
        }

        private void BuildStrokesList(VisualElement container, int lineIndex, SerializedProperty lineProp)
        {
            container.Clear();
            var strokesProp = lineProp.FindPropertyRelative("strokes");
            var spacingsProp = lineProp.FindPropertyRelative("spacingsMeters");
            if (strokesProp == null)
            {
                return;
            }

            var n = strokesProp.arraySize;
            for (var i = 0; i < n; i++)
            {
                var sIdx = i;
                var styleProp = strokesProp.GetArrayElementAtIndex(sIdx);

                container.Add(BuildStrokeWrapper(lineIndex, sIdx, n, styleProp));

                if (sIdx < n - 1 && spacingsProp != null && sIdx < spacingsProp.arraySize)
                {
                    container.Add(BuildSpacingRow(spacingsProp.GetArrayElementAtIndex(sIdx)));
                }
            }
        }

        private VisualElement BuildStrokeWrapper(int lineIndex, int strokeIdx, int totalStrokes, SerializedProperty styleProp)
        {
            var strokeWrap = new VisualElement();
            strokeWrap.AddToClassList("stroke-wrapper");

            var sHeader = new VisualElement();
            sHeader.AddToClassList("stroke-header");

            var sType = (LineType)styleProp.FindPropertyRelative("type").enumValueIndex;
            var sFoldout = new Foldout
            {
                text = $"Stroke {strokeIdx + 1}: {sType}",
                value = strokeIdx == 0,
            };
            sFoldout.style.flexGrow = 1;

            var sUp = new Button(() => MoveStroke(lineIndex, strokeIdx, -1)) { text = "▲", tooltip = "Move stroke up" };
            sUp.AddToClassList("reorder-button");
            sUp.SetEnabled(strokeIdx > 0);

            var sDown = new Button(() => MoveStroke(lineIndex, strokeIdx, +1)) { text = "▼", tooltip = "Move stroke down" };
            sDown.AddToClassList("reorder-button");
            sDown.SetEnabled(strokeIdx < totalStrokes - 1);

            var sRemove = new Button(() => RemoveStroke(lineIndex, strokeIdx)) { text = "✕", tooltip = "Remove stroke" };
            sRemove.AddToClassList("remove-button");
            sRemove.SetEnabled(totalStrokes > 1);

            sHeader.Add(sFoldout);
            sHeader.Add(sUp);
            sHeader.Add(sDown);
            sHeader.Add(sRemove);
            strokeWrap.Add(sHeader);

            sFoldout.Add(BuildLineStyleSection($"Stroke {strokeIdx + 1}", styleProp));

            var typeProp = styleProp.FindPropertyRelative("type");
            sFoldout.TrackPropertyValue(typeProp, _ =>
                sFoldout.text = $"Stroke {strokeIdx + 1}: {(LineType)typeProp.enumValueIndex}");

            return strokeWrap;
        }

        private static VisualElement BuildSpacingRow(SerializedProperty spacingProp)
        {
            var gapRow = new VisualElement();
            gapRow.AddToClassList("stroke-gap-row");
            gapRow.Add(new Label("↕ Spacing (m)"));
            var f = new FloatField();
            f.style.flexGrow = 1;
            f.BindProperty(spacingProp);
            gapRow.Add(f);
            return gapRow;
        }

        private void MoveLine(int index, int direction)
        {
            if (currentAsset == null)
            {
                return;
            }
            var lines = currentAsset.config.lines;
            var newIndex = index + direction;
            if (newIndex < 0 || newIndex >= lines.Count)
            {
                return;
            }
            Undo.RecordObject(currentAsset, "Reorder Boundary Line");
            (lines[index], lines[newIndex]) = (lines[newIndex], lines[index]);
            EditorUtility.SetDirty(currentAsset);
            serializedObject.Update();
            RebuildLinesSection();
        }

        private void AddStroke(int lineIdx)
        {
            if (currentAsset == null)
            {
                return;
            }
            var line = currentAsset.config.lines[lineIdx];
            line.strokes ??= new List<LineStyle>();
            line.spacingsMeters ??= new List<float>();
            Undo.RecordObject(currentAsset, "Add Line Stroke");
            var newStyle = line.strokes.Count > 0
                ? line.strokes[line.strokes.Count - 1].Clone()
                : new LineStyle();
            if (line.strokes.Count > 0)
            {
                line.spacingsMeters.Add(0.15f);
            }
            line.strokes.Add(newStyle);
            EditorUtility.SetDirty(currentAsset);
            serializedObject.Update();
            RebuildLinesSection();
        }

        private void RemoveStroke(int lineIdx, int strokeIdx)
        {
            if (currentAsset == null)
            {
                return;
            }
            var line = currentAsset.config.lines[lineIdx];
            if (line.strokes == null || line.strokes.Count <= 1)
            {
                return;
            }
            Undo.RecordObject(currentAsset, "Remove Line Stroke");
            line.strokes.RemoveAt(strokeIdx);
            // spacingsMeters は Count - 1 個。削除した stroke を挟んでいた gap を 1 つ取り除く。
            if (line.spacingsMeters != null && line.spacingsMeters.Count > 0)
            {
                var spacingToRemove = Mathf.Min(strokeIdx, line.spacingsMeters.Count - 1);
                line.spacingsMeters.RemoveAt(spacingToRemove);
            }
            EditorUtility.SetDirty(currentAsset);
            serializedObject.Update();
            RebuildLinesSection();
        }

        private void MoveStroke(int lineIdx, int strokeIdx, int dir)
        {
            if (currentAsset == null)
            {
                return;
            }
            var line = currentAsset.config.lines[lineIdx];
            if (line.strokes == null)
            {
                return;
            }
            var newIdx = strokeIdx + dir;
            if (newIdx < 0 || newIdx >= line.strokes.Count)
            {
                return;
            }
            Undo.RecordObject(currentAsset, "Reorder Line Stroke");
            (line.strokes[strokeIdx], line.strokes[newIdx]) = (line.strokes[newIdx], line.strokes[strokeIdx]);
            EditorUtility.SetDirty(currentAsset);
            serializedObject.Update();
            RebuildLinesSection();
        }

        // -----------------------------------------------------------------
        // タイル長スナップヘルパー
        // -----------------------------------------------------------------

        // (size + spacing) が tileLength を許容差内で割り切るかを判定する。
        private static bool TileLengthMatches(float size, float spacing, float tileLength, float tolerance = 0.02f)
        {
            var period = size + spacing;
            if (period < 0.001f || tileLength <= 0f)
            {
                return true;
            }
            var ratio = tileLength / period;
            return Mathf.Abs(ratio - Mathf.Round(ratio)) <= tolerance;
        }

        // (size + newSpacing) が tileLength を割り切るような新 spacing を返す。元の値からの差が
        // 最小になるように選び、minSpacing 以上を確保する。条件を満たす値が無い場合(size が
        // 既に tileLength を超えている等)は float.NaN を返す。
        private static float SnapSpacingToTile(float size, float currentSpacing, float tileLength, float minSpacing)
        {
            if (tileLength <= 0f || size <= 0f)
            {
                return float.NaN;
            }
            var curSpacing = Mathf.Max(0f, currentSpacing);
            var curPeriod = size + curSpacing;
            if (curPeriod < 0.001f)
            {
                return float.NaN;
            }

            // (tileLength / currentPeriod) に最も近い整数 N を選んで newSpacing = (tileLength / N) - size
            // を導出する。minSpacing を下回る場合は N を 1 ずつ減らして(= 周期を長く / spacing を大きく)
            // 条件を満たす値を探す。
            var n = Mathf.Max(1, Mathf.RoundToInt(tileLength / curPeriod));
            while (n > 1)
            {
                var newSpacing = tileLength / n - size;
                if (newSpacing >= minSpacing)
                {
                    return newSpacing;
                }
                n--;
            }
            // n == 1:タイルあたりマーカー 1 個。それでも収まらなければ NaN。
            var fallback = tileLength - size;
            return fallback >= minSpacing ? fallback : float.NaN;
        }

        // spacing をスナップして同時に offset を再センタリングし、パターンがタイル内で対称になるようにする。
        // 3 つの「Snap & center」ボタン(Dashed / Diamond の LineStyle、路面凹凸舗装、レーン山形マーカー)から
        // 共有される。
        private static void SnapAndCenter(SerializedProperty sizeProp, SerializedProperty spacingProp, SerializedProperty offsetProp, float tileLength, float minSpacing)
        {
            var newSpacing = SnapSpacingToTile(sizeProp.floatValue, spacingProp.floatValue, tileLength, minSpacing);
            if (float.IsNaN(newSpacing))
            {
                return;
            }
            var so = sizeProp.serializedObject;
            so.Update();
            spacingProp.floatValue = newSpacing;
            offsetProp.floatValue = newSpacing * 0.5f;
            so.ApplyModifiedProperties();
        }

        private static SerializedProperty GetTileLengthProp(SerializedObject so)
        {
            return so.FindProperty("config.output.textureLengthMeters");
        }

        private void SnapLineStyleToTile(SerializedProperty styleProp)
        {
            var lt = (LineType)styleProp.FindPropertyRelative("type").enumValueIndex;
            if (lt != LineType.Dashed && lt != LineType.Diamond)
            {
                return;
            }
            var tileProp = GetTileLengthProp(styleProp.serializedObject);
            if (tileProp == null)
            {
                return;
            }

            var (sizeName, spacingName) = lt == LineType.Dashed
                ? ("dashLengthMeters", "dashGapMeters")
                : ("diamondSizeMeters", "diamondSpacingMeters");
            SnapAndCenter(
                styleProp.FindPropertyRelative(sizeName),
                styleProp.FindPropertyRelative(spacingName),
                styleProp.FindPropertyRelative("dashOffsetMeters"),
                tileProp.floatValue,
                minSpacing: 0.05f);
        }

        private void SnapRumbleToTile(SerializedProperty laneProp)
        {
            var tileProp = GetTileLengthProp(laneProp.serializedObject);
            if (tileProp == null)
            {
                return;
            }
            // rumbleStripSpacingMeters は RoadConfig.cs 側で [Min(0.1f)]。
            SnapAndCenter(
                laneProp.FindPropertyRelative("rumbleStripWidthMeters"),
                laneProp.FindPropertyRelative("rumbleStripSpacingMeters"),
                laneProp.FindPropertyRelative("rumbleStripStartOffsetMeters"),
                tileProp.floatValue,
                minSpacing: 0.1f);
        }

        private void SnapLaneSpeedReductionDotLineToTile(SerializedProperty laneProp)
        {
            var tileProp = GetTileLengthProp(laneProp.serializedObject);
            if (tileProp == null)
            {
                return;
            }
            // speedReductionDotLineSpacingMeters は RoadConfig.cs 側で [Min(0.1f)]。
            SnapAndCenter(
                laneProp.FindPropertyRelative("speedReductionDotLineHeightMeters"),
                laneProp.FindPropertyRelative("speedReductionDotLineSpacingMeters"),
                laneProp.FindPropertyRelative("speedReductionDotLineStartOffsetMeters"),
                tileProp.floatValue,
                minSpacing: 0.1f);
        }

        private VisualElement BuildLineStyleSection(string label, SerializedProperty styleProp)
        {
            var section = new VisualElement();
            section.AddToClassList("line-foldout-style-section");

            var heading = new Label(label);
            heading.AddToClassList("bold");
            section.Add(heading);

            var typeField = new EnumField("Type");
            typeField.BindProperty(styleProp.FindPropertyRelative("type"));
            section.Add(typeField);

            var colorField = AddBoundColor(section, styleProp, "color", "Color");
            var widthField = AddBoundFloat(section, styleProp, "widthMeters", "Width (m)");

            var dashLengthField = AddBoundFloat(section, styleProp, "dashLengthMeters", "Dash Length (m)");
            var dashGapField    = AddBoundFloat(section, styleProp, "dashGapMeters",    "Dash Gap (m)");
            // dashOffsetMeters は Diamond の位相オフセットも兼ねている。
            var dashOffsetField = AddBoundFloat(section, styleProp, "dashOffsetMeters", "Dash / Phase Offset (m)");

            var diamondSizeField    = AddBoundFloat(section, styleProp, "diamondSizeMeters",    "Diamond Size (m)");
            var diamondSpacingField = AddBoundFloat(section, styleProp, "diamondSpacingMeters", "Diamond Spacing (m)");
            AddBoundFloat(section, styleProp, "diamondSlantMeters", "Diamond Slant (m)",
                "Top/bottom U-axis offset of the parallelogram. Positive leans right, negative leans left, 0 gives a thick rectangle.");
            AddBoundFloat(section, styleProp, "paintHeightFactor", "Paint Height Factor",
                "Per-stroke paint thickness contribution to the normal map. 0 = flat, 1 = standard, > 1 = thicker. Modulated by Weathering.PaintHeightStrength and Line Edge Wear.");

            var (tileWarn, snapBtn) = AddTileWarnAndSnap(section,
                "Snap & center pattern",
                "Adjust spacing so the period divides the texture tile evenly, and set the phase offset so the pattern is centered (half-gap padding on each tile boundary).",
                () => SnapLineStyleToTile(styleProp));

            void UpdateTileWarning() => UpdateLineStyleTileWarning(styleProp, tileWarn, snapBtn);
            void UpdateVisibility()
            {
                var typeIdx = styleProp.FindPropertyRelative("type").enumValueIndex;
                var typeIsNone    = (LineType)typeIdx == LineType.None;
                var typeIsDashed  = (LineType)typeIdx == LineType.Dashed;
                var typeIsDiamond = (LineType)typeIdx == LineType.Diamond;
                colorField.style.display = typeIsNone ? DisplayStyle.None : DisplayStyle.Flex;
                widthField.style.display = typeIsNone ? DisplayStyle.None : DisplayStyle.Flex;
                dashLengthField.style.display = typeIsDashed ? DisplayStyle.Flex : DisplayStyle.None;
                dashGapField.style.display    = typeIsDashed ? DisplayStyle.Flex : DisplayStyle.None;
                dashOffsetField.style.display = (typeIsDashed || typeIsDiamond) ? DisplayStyle.Flex : DisplayStyle.None;
                diamondSizeField.style.display    = typeIsDiamond ? DisplayStyle.Flex : DisplayStyle.None;
                diamondSpacingField.style.display = typeIsDiamond ? DisplayStyle.Flex : DisplayStyle.None;
                UpdateTileWarning();
            }

            UpdateVisibility();
            typeField.RegisterValueChangedCallback(_ => UpdateVisibility());
            dashLengthField.RegisterValueChangedCallback(_ => UpdateTileWarning());
            dashGapField.RegisterValueChangedCallback(_ => UpdateTileWarning());
            diamondSizeField.RegisterValueChangedCallback(_ => UpdateTileWarning());
            diamondSpacingField.RegisterValueChangedCallback(_ => UpdateTileWarning());
            snapBtn.clicked += UpdateTileWarning;

            TrackTileLengthChange(section, styleProp.serializedObject, UpdateTileWarning);

            return section;
        }

        // -----------------------------------------------------------------
        // タイル長警告ヘルパー(line / rumble / diamond セクションで共有)
        // -----------------------------------------------------------------
        private static (HelpBox warn, Button snap) AddTileWarnAndSnap(VisualElement parent, string buttonText, string buttonTooltip, System.Action onClick)
        {
            var tileWarn = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
            parent.Add(tileWarn);

            var snapBtn = new Button(onClick) { text = buttonText, tooltip = buttonTooltip };
            snapBtn.AddToClassList("snap-fix-button");
            parent.Add(snapBtn);

            return (tileWarn, snapBtn);
        }

        private static void TrackTileLengthChange(VisualElement holder, SerializedObject so, System.Action update)
        {
            var tileLenProp = GetTileLengthProp(so);
            if (tileLenProp != null)
            {
                holder.TrackPropertyValue(tileLenProp, _ => update());
            }
        }

        private static void ApplyTileWarning(HelpBox tileWarn, Button snapBtn, bool show, string message)
        {
            if (show)
            {
                tileWarn.text = message;
                tileWarn.style.display = DisplayStyle.Flex;
                snapBtn.style.display = DisplayStyle.Flex;
            }
            else
            {
                tileWarn.style.display = DisplayStyle.None;
                snapBtn.style.display = DisplayStyle.None;
            }
        }

        private static void UpdateLineStyleTileWarning(SerializedProperty styleProp, HelpBox tileWarn, Button snapBtn)
        {
            var lt = (LineType)styleProp.FindPropertyRelative("type").enumValueIndex;
            if (lt != LineType.Dashed && lt != LineType.Diamond)
            {
                ApplyTileWarning(tileWarn, snapBtn, false, null);
                return;
            }
            var tileProp = GetTileLengthProp(styleProp.serializedObject);
            if (tileProp == null)
            {
                ApplyTileWarning(tileWarn, snapBtn, false, null);
                return;
            }
            var tile = tileProp.floatValue;
            var (size, spacing, kind) = lt == LineType.Dashed
                ? (styleProp.FindPropertyRelative("dashLengthMeters").floatValue,
                   styleProp.FindPropertyRelative("dashGapMeters").floatValue,
                   "dash")
                : (styleProp.FindPropertyRelative("diamondSizeMeters").floatValue,
                   styleProp.FindPropertyRelative("diamondSpacingMeters").floatValue,
                   "diamond");
            if (TileLengthMatches(size, spacing, tile))
            {
                ApplyTileWarning(tileWarn, snapBtn, false, null);
            }
            else
            {
                var period = size + spacing;
                ApplyTileWarning(tileWarn, snapBtn, true,
                    $"Tile length ({tile:F1}m) is not an integer multiple of {kind} period ({period:F2}m). Pattern will tear when tiled.");
            }
        }

        private static void UpdateRumbleStyleTileWarning(SerializedProperty laneProp, HelpBox tileWarn, Button snapBtn)
        {
            if (!laneProp.FindPropertyRelative("rumbleStrip").boolValue)
            {
                ApplyTileWarning(tileWarn, snapBtn, false, null);
                return;
            }
            var tileProp = GetTileLengthProp(laneProp.serializedObject);
            if (tileProp == null)
            {
                ApplyTileWarning(tileWarn, snapBtn, false, null);
                return;
            }
            var tile = tileProp.floatValue;
            var w = laneProp.FindPropertyRelative("rumbleStripWidthMeters").floatValue;
            var sp = laneProp.FindPropertyRelative("rumbleStripSpacingMeters").floatValue;
            if (TileLengthMatches(w, sp, tile))
            {
                ApplyTileWarning(tileWarn, snapBtn, false, null);
            }
            else
            {
                var period = w + sp;
                ApplyTileWarning(tileWarn, snapBtn, true,
                    $"Tile length ({tile:F1}m) is not an integer multiple of rumble period ({period:F2}m). Spacing will be uneven at tile seams.");
            }
        }

        private static void UpdateSpeedReductionDotLineTileWarning(SerializedProperty laneProp, HelpBox tileWarn, Button snapBtn)
        {
            if (!laneProp.FindPropertyRelative("speedReductionDotLine").boolValue)
            {
                ApplyTileWarning(tileWarn, snapBtn, false, null);
                return;
            }
            var tileProp = GetTileLengthProp(laneProp.serializedObject);
            if (tileProp == null)
            {
                ApplyTileWarning(tileWarn, snapBtn, false, null);
                return;
            }
            var tile = tileProp.floatValue;
            var h = laneProp.FindPropertyRelative("speedReductionDotLineHeightMeters").floatValue;
            var sp = laneProp.FindPropertyRelative("speedReductionDotLineSpacingMeters").floatValue;
            if (TileLengthMatches(h, sp, tile))
            {
                ApplyTileWarning(tileWarn, snapBtn, false, null);
            }
            else
            {
                var period = h + sp;
                ApplyTileWarning(tileWarn, snapBtn, true,
                    $"Tile length ({tile:F1}m) is not an integer multiple of dot period ({period:F2}m). Pattern will tear when tiled.");
            }
        }

        // -----------------------------------------------------------------
        // 既定値リセット / プリセット適用 / 新規プリセット保存
        // -----------------------------------------------------------------
        private void ResetCurrentToDefault()
        {
            if (currentAsset == null)
            {
                EnsureCurrentAsset();
            }
            var targetName = IsUsingTempAsset ? "the unsaved settings" : $"\"{currentAsset.name}\"";
            var ok = EditorUtility.DisplayDialog(
                "Reset Settings",
                $"Reset {targetName} to default values?\n\nThis will overwrite all current configuration. (Undo is supported.)",
                "Reset",
                "Cancel");
            if (!ok)
            {
                return;
            }

            Undo.RecordObject(currentAsset, "Reset Road Configuration");
            currentAsset.config = RoadConfig.PresetMountainRoad_NoOvertaking();
            currentAsset.config.EnsureLineCount();
            EditorUtility.SetDirty(currentAsset);
            serializedObject.Update();
            RebuildLanesSection();
            RebuildLinesSection();
            UpdateInfoLabel();
        }

        private void ApplyPreset(RoadConfig preset)
        {
            if (currentAsset == null)
            {
                EnsureCurrentAsset();
            }
            if (!IsUsingTempAsset)
            {
                var ok = EditorUtility.DisplayDialog(
                    "Apply Preset",
                    $"This will overwrite the loaded preset asset \"{currentAsset.name}\". Continue?",
                    "Overwrite",
                    "Cancel");
                if (!ok)
                {
                    return;
                }
            }
            Undo.RecordObject(currentAsset, "Apply Preset");
            currentAsset.config = preset;
            currentAsset.config.EnsureLineCount();
            EditorUtility.SetDirty(currentAsset);
            serializedObject.Update();
            RebuildLanesSection();
            RebuildLinesSection();
            UpdateInfoLabel();
        }

        private void SaveCurrentAsNewPreset()
        {
            const string defaultName = "RoadPreset";
            var path = EditorUtility.SaveFilePanelInProject(
                "Save Road Preset",
                defaultName,
                "asset",
                "Choose where to save the preset asset.",
                "Assets");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            var newAsset = ScriptableObject.CreateInstance<RoadConfigAsset>();
            newAsset.config = JsonClone(currentAsset.config);
            AssetDatabase.CreateAsset(newAsset, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            currentAsset = newAsset;
            BindToAsset(currentAsset);
            EditorGUIUtility.PingObject(newAsset);
        }

        // -----------------------------------------------------------------
        // 断面プレビュー(IMGUI で毎フレーム描画)
        // -----------------------------------------------------------------
        private void DrawCrossSectionIMGUI()
        {
            if (currentAsset == null)
            {
                return;
            }
            var c = currentAsset.config;
            if (c.TotalWidthMeters <= 0f)
            {
                return;
            }

            var rect = previewContainer.contentRect;
            if (rect.width <= 0f)
            {
                return;
            }

            // 背景はアスファルト色。境界線スロットには色を塗らないため、レーン塗りの「すき間」として
            // アスファルト色が見え、境界線が U 軸方向に占有している領域を可視化する。
            var asphaltCol  = new Color(0.18f, 0.18f, 0.20f);
            var shoulderCol = new Color(0.32f, 0.31f, 0.28f);
            EditorGUI.DrawRect(rect, asphaltCol);

            var pxPerM = rect.width / c.TotalWidthMeters;
            c.EnsureLineCount();

            // パス 1:路側帯とレーンを描画(line slot 部分は塗らずアスファルトを透過)。
            var xCursor = rect.x;
            var lsW = c.leftShoulder.widthMeters * pxPerM;
            EditorGUI.DrawRect(new Rect(xCursor, rect.y, lsW, rect.height), shoulderCol);
            xCursor += lsW;

            for (var b = 0; b <= c.lanes.Count; b++)
            {
                c.lines[b].ComputeSlotInfo(out _, out _, out var slotW);
                xCursor += slotW * pxPerM;
                if (b < c.lanes.Count)
                {
                    var lw = c.lanes[b].widthMeters * pxPerM;
                    var laneCol = (b % 2 == 0) ? new Color(0.28f, 0.27f, 0.25f) : new Color(0.30f, 0.29f, 0.27f);
                    EditorGUI.DrawRect(new Rect(xCursor, rect.y, lw, rect.height), laneCol);
                    xCursor += lw;
                }
            }

            var rsW = c.rightShoulder.widthMeters * pxPerM;
            EditorGUI.DrawRect(new Rect(xCursor, rect.y, rsW, rect.height), shoulderCol);

            // パス 2:各境界線をその配置軸 (slot_left + leftHalf) に描画する。
            xCursor = rect.x + lsW;
            for (var b = 0; b <= c.lanes.Count; b++)
            {
                c.lines[b].ComputeSlotInfo(out var lh, out _, out var slotW);
                var boundaryX = xCursor + lh * pxPerM;
                DrawPreviewLine(boundaryX, rect, pxPerM, c.lines[b]);
                xCursor += slotW * pxPerM;
                if (b < c.lanes.Count)
                {
                    xCursor += c.lanes[b].widthMeters * pxPerM;
                }
            }
        }

        private static void DrawPreviewLine(float x, Rect rect, float pxPerM, LineConfig line)
        {
            if (line.strokes == null || line.strokes.Count == 0)
            {
                return;
            }

            var gaps = line.spacingsMeters;
            var gapCount = Mathf.Min(line.strokes.Count - 1, gaps?.Count ?? 0);
            var total = 0f;
            for (var i = 0; i < gapCount; i++)
            {
                total += Mathf.Max(0f, gaps[i]);
            }

            var cursor = -total * 0.5f;
            for (var i = 0; i < line.strokes.Count; i++)
            {
                DrawSinglePreviewStroke(x + cursor * pxPerM, rect, pxPerM, line.strokes[i]);
                if (i < gapCount)
                {
                    cursor += Mathf.Max(0f, gaps[i]);
                }
            }
        }

        private static void DrawSinglePreviewStroke(float xc, Rect rect, float pxPerM, LineStyle s)
        {
            if (s.type == LineType.None)
            {
                return;
            }
            var halfW = Mathf.Max(1f, s.widthMeters * pxPerM * 0.5f);
            if (s.type == LineType.Solid)
            {
                EditorGUI.DrawRect(new Rect(xc - halfW, rect.y, halfW * 2f, rect.height), s.color);
                return;
            }

            // Dashed と Diamond は粗いダッシュ近似を共有する。実テクスチャの方が描画の正確性において優先される。
            const int dashes = 6;
            var dashH = rect.height / (dashes * 2);
            for (var i = 0; i < dashes; i++)
            {
                EditorGUI.DrawRect(new Rect(xc - halfW, rect.y + i * dashH * 2 + dashH * 0.5f, halfW * 2f, dashH), s.color);
            }
        }

        // -----------------------------------------------------------------
        // Albedo プレビューサムネイル
        // -----------------------------------------------------------------
        private void RefreshAlbedoPreview()
        {
            if (currentAsset == null)
            {
                return;
            }
            try
            {
                var tempCfg = JsonClone(currentAsset.config);
                tempCfg.output.resolution = TextureResolution._512;
                tempCfg.output.generateNormal = false;
                tempCfg.output.generateMetallicSmoothness = false;
                tempCfg.output.generateAO = false;
                tempCfg.output.generateMaterial = false;

                var gen = RoadTextureBaker.Bake(tempCfg);
                if (previewTex != null)
                {
                    DestroyImmediate(previewTex);
                }
                previewTex = gen.albedo;
                albedoPreview.image = previewTex;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[RoadAssetGenerator] Preview failed: {e.Message}");
            }
        }

        // -----------------------------------------------------------------
        // Output Folder のブラウズ(OS のフォルダ選択ダイアログ、結果はプロジェクト相対に変換)
        // -----------------------------------------------------------------
        private void BrowseOutputFolder()
        {
            if (currentAsset == null)
            {
                return;
            }

            var current = currentAsset.config.output.outputFolder;
            if (string.IsNullOrEmpty(current))
            {
                current = "Assets";
            }

            // プロジェクトルート = Application.dataPath の親ディレクトリ (= Assets/ の上の階層)。
            var projectRoot = Path.GetDirectoryName(Application.dataPath).Replace('\\', '/');
            var absStart = Path.IsPathRooted(current)
                ? current
                : Path.Combine(projectRoot, current);

            var selected = EditorUtility.OpenFolderPanel("Select Output Folder", absStart, string.Empty);
            if (string.IsNullOrEmpty(selected))
            {
                return;
            }

            var selectedNorm = selected.Replace('\\', '/');
            var projectNorm  = projectRoot.Replace('\\', '/');
            if (!selectedNorm.StartsWith(projectNorm))
            {
                EditorUtility.DisplayDialog(
                    "Road Asset Generator",
                    "Output folder must be inside the project (Assets/ or Packages/).",
                    "OK");
                return;
            }

            var relative = selectedNorm.Substring(projectNorm.Length).TrimStart('/');
            if (!relative.StartsWith("Assets") && !relative.StartsWith("Packages"))
            {
                EditorUtility.DisplayDialog(
                    "Road Asset Generator",
                    "Output folder must be inside Assets/ or Packages/.",
                    "OK");
                return;
            }

            Undo.RecordObject(currentAsset, "Set Output Folder");
            currentAsset.config.output.outputFolder = relative;
            EditorUtility.SetDirty(currentAsset);
            serializedObject.Update();
        }

        // -----------------------------------------------------------------
        // Generate
        // -----------------------------------------------------------------
        private void OnGenerate()
        {
            if (currentAsset == null)
            {
                return;
            }

            var folder = currentAsset.config.output.outputFolder;
            var prefix = currentAsset.config.output.namePrefix;
            if (string.IsNullOrEmpty(folder))
            {
                folder = "Assets";
            }
            if (string.IsNullOrEmpty(prefix))
            {
                prefix = "road";
            }

            if (!folder.StartsWith("Assets") && !folder.StartsWith("Packages"))
            {
                EditorUtility.DisplayDialog(
                    "Road Asset Generator",
                    $"Output folder must be inside the project (Assets/ or Packages/).\nCurrent: \"{folder}\"",
                    "OK");
                return;
            }

            if (!ConfirmOverwriteIfExisting(folder, prefix))
            {
                return;
            }

            try
            {
                EditorUtility.DisplayProgressBar("Road Asset Generator", "Baking textures...", 0.3f);
                var gen = RoadTextureBaker.Bake(currentAsset.config);
                EditorUtility.DisplayProgressBar("Road Asset Generator", "Saving assets...", 0.7f);
                var paths = RoadMaterialFactory.SaveAndCreateAssets(gen, currentAsset.config);
                EditorUtility.ClearProgressBar();

                Debug.Log($"[RoadAssetGenerator] Generated assets in {currentAsset.config.output.outputFolder}");

                if (!string.IsNullOrEmpty(paths.materialPath))
                {
                    var mat = AssetDatabase.LoadAssetAtPath<Material>(paths.materialPath);
                    if (mat != null)
                    {
                        Selection.activeObject = mat;
                        EditorGUIUtility.PingObject(mat);
                    }
                }
                EditorUtility.DisplayDialog(
                    "Road Asset Generator",
                    $"Successfully generated.\n\nFolder: {currentAsset.config.output.outputFolder}\nName Prefix: {currentAsset.config.output.namePrefix}\n\n" +
                    $"Road dimensions: width {currentAsset.config.TotalWidthMeters:F2} m × tile length {currentAsset.config.output.textureLengthMeters:F1} m.\n" +
                    "Set the target mesh's UV tiling so the texture is mapped at 1 m = 1 unit.",
                    "OK");
            }
            catch (System.Exception e)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogException(e);
                EditorUtility.DisplayDialog("Road Asset Generator", $"Error: {e.Message}", "OK");
            }
        }

        private static bool ConfirmOverwriteIfExisting(string folder, string prefix)
        {
            string[] candidates =
            {
                $"{folder}/{prefix}_albedo.png",
                $"{folder}/{prefix}_normal.png",
                $"{folder}/{prefix}_metallicSmoothness.png",
                $"{folder}/{prefix}_ao.png",
                $"{folder}/{prefix}_material.mat",
            };
            var existing = new List<string>();
            foreach (var p in candidates)
            {
                if (File.Exists(p))
                {
                    existing.Add(p);
                }
            }
            if (existing.Count == 0)
            {
                return true;
            }
            return EditorUtility.DisplayDialog(
                "Overwrite existing assets?",
                "The following files already exist and will be overwritten:\n\n" + string.Join("\n", existing),
                "Overwrite",
                "Cancel");
        }

        // -----------------------------------------------------------------
        // バインド済みフィールドの生成ヘルパー(セクションビルダーが上から下に読めるよう小さく保つ)
        // -----------------------------------------------------------------
        private static FloatField AddBoundFloat(VisualElement parent, SerializedProperty owner, string relativeName, string label, string tooltip = null)
        {
            var f = new FloatField(label);
            if (!string.IsNullOrEmpty(tooltip))
            {
                f.tooltip = tooltip;
            }
            f.BindProperty(owner.FindPropertyRelative(relativeName));
            parent.Add(f);
            return f;
        }

        private static ColorField AddBoundColor(VisualElement parent, SerializedProperty owner, string relativeName, string label)
        {
            var f = new ColorField(label);
            f.BindProperty(owner.FindPropertyRelative(relativeName));
            parent.Add(f);
            return f;
        }

        private static EnumField AddBoundEnum(VisualElement parent, SerializedProperty owner, string relativeName, string label, string tooltip = null)
        {
            var f = new EnumField(label);
            if (!string.IsNullOrEmpty(tooltip))
            {
                f.tooltip = tooltip;
            }
            f.BindProperty(owner.FindPropertyRelative(relativeName));
            parent.Add(f);
            return f;
        }

        private static VisualElement NewLaneSubSection(string heading)
        {
            var section = new VisualElement();
            section.AddToClassList("lane-sub-section");
            section.Add(new Label(heading) { style = { unityFontStyleAndWeight = FontStyle.Bold } });
            return section;
        }

        // -----------------------------------------------------------------
        // その他のヘルパー
        // -----------------------------------------------------------------
        private static VisualTreeAsset LoadUxml()
        {
            var guids = AssetDatabase.FindAssets("RoadAssetGeneratorWindow t:VisualTreeAsset");
            foreach (var g in guids)
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                if (p.EndsWith(".uxml"))
                {
                    return AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(p);
                }
            }
            return null;
        }

        private static RoadConfig JsonClone(RoadConfig c)
        {
            var json = JsonUtility.ToJson(c);
            return JsonUtility.FromJson<RoadConfig>(json);
        }

        private void OnDisable()
        {
            if (previewTex != null)
            {
                DestroyImmediate(previewTex);
            }
            if (tempAsset != null)
            {
                DestroyImmediate(tempAsset);
            }
        }
    }
}
#endif

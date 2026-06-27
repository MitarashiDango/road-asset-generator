#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MitarashiDango.RoadAssetGenerator
{
    public class PolygonEditorWindow : EditorWindow
    {
        [SerializeField] private PolygonDataAsset currentAsset;

        private Vector2 _viewCenter = new Vector2(0f, 0.5f);
        private float _pixelsPerUnit = 200f;
        private int _selectedRing = -1;
        private int _selectedVertex = -1;
        private bool _isDraggingVertex;
        private bool _isPanning;
        private Vector2 _lastMousePos;
        private Vector2 _sidebarScroll;
        private float _sidebarWidth = 320f;
        private bool _isResizingSidebar;

        private readonly Dictionary<string, bool> _foldouts = new Dictionary<string, bool>();

        // 頂点グループのプレビュー用 weight。
        private readonly Dictionary<string, float> _previewWeights = new Dictionary<string, float>();

        // 現在フレームで使用する、weight 適用済み頂点位置のキャッシュ。
        // プレビュー用 weight が無効なら null。有効なら rings[ri][vi] == 最終位置。
        private Vector2[][] _resolvedPositions;
        private bool _hasActiveWeights;

        private const float MinPixelsPerUnit = 30f;
        private const float MaxPixelsPerUnit = 800f;
        private const float VertexHandleRadius = 5f;
        private const float SelectDistSq = 100f; // 10 px 分の距離を二乗した値。

        private static readonly Color BgColor = new Color(0.18f, 0.18f, 0.18f);
        private static readonly Color GridMajor = new Color(0.35f, 0.35f, 0.35f, 0.8f);
        private static readonly Color GridMinor = new Color(0.25f, 0.25f, 0.25f, 0.5f);
        private static readonly Color BoundsColor = new Color(0.55f, 0.55f, 0.35f, 0.6f);
        private static readonly Color EdgeColor = new Color(0.9f, 0.9f, 0.3f);
        private static readonly Color HoleColor = new Color(0.9f, 0.4f, 0.4f);
        private static readonly Color VertexNormal = new Color(0.3f, 0.8f, 1.0f);
        private static readonly Color VertexSelected = new Color(1.0f, 0.5f, 0.1f);
        private static readonly Color LabelBg = new Color(0f, 0f, 0f, 0.6f);

        [MenuItem("Tools/Road Asset Generator/Polygon Editor")]
        public static void Open()
        {
            var w = GetWindow<PolygonEditorWindow>("Polygon Editor");
            w.minSize = new Vector2(700, 400);
        }

        public static void Open(PolygonDataAsset asset)
        {
            var w = GetWindow<PolygonEditorWindow>("Polygon Editor");
            w.minSize = new Vector2(700, 400);
            w.currentAsset = asset;
            w._selectedRing = -1;
            w._selectedVertex = -1;
        }

        private PolygonData Data => currentAsset != null ? currentAsset.data : null;

        private Vector2 NormToGui(float u, float v, Rect r)
        {
            return new Vector2(
                r.center.x + (u - _viewCenter.x) * _pixelsPerUnit,
                r.center.y - (v - _viewCenter.y) * _pixelsPerUnit);
        }

        private Vector2 GuiToNorm(Vector2 p, Rect r)
        {
            return new Vector2(
                (p.x - r.center.x) / _pixelsPerUnit + _viewCenter.x,
                -(p.y - r.center.y) / _pixelsPerUnit + _viewCenter.y);
        }

        private void RecordUndo(string name)
        {
            Undo.RecordObject(currentAsset, name);
        }

        private void MarkDirty()
        {
            EditorUtility.SetDirty(currentAsset);
        }

        private bool Foldout(string key, string label, bool defaultOpen = true)
        {
            if (!_foldouts.TryGetValue(key, out var state))
            {
                state = defaultOpen;
            }
            state = EditorGUILayout.Foldout(state, label, true);
            _foldouts[key] = state;
            return state;
        }

        /// <summary>
        /// 現在の <see cref="_previewWeights"/> を <see cref="Data"/> に適用した頂点位置をキャッシュする。
        /// 有効な weight が一つもない場合は <see cref="_resolvedPositions"/> を null にして基本位置を使用する。
        /// </summary>
        private void UpdateResolvedPositions()
        {
            _hasActiveWeights = false;
            if (Data == null)
            {
                _resolvedPositions = null;
                return;
            }

            foreach (var g in Data.vertexGroups)
            {
                if (g == null || string.IsNullOrEmpty(g.name)) continue;
                if (_previewWeights.TryGetValue(g.name, out var w) && !Mathf.Approximately(w, 0f))
                {
                    _hasActiveWeights = true;
                    break;
                }
            }

            if (!_hasActiveWeights)
            {
                _resolvedPositions = null;
                return;
            }

            _resolvedPositions = Data.Resolve(_previewWeights).rings;
        }

        /// <summary>
        /// 描画用の頂点位置（weight 適用後）を取得する。weight が無効な場合は基本位置を返す。
        /// </summary>
        private Vector2 GetDisplayPos(int ri, int vi)
        {
            if (_resolvedPositions != null &&
                ri >= 0 && ri < _resolvedPositions.Length &&
                vi >= 0 && vi < _resolvedPositions[ri].Length)
            {
                return _resolvedPositions[ri][vi];
            }
            return Data.rings[ri].vertices[vi].position;
        }

        /// <summary>
        /// 頂点 (ri, vi) の weight 由来オフセット（解決済み位置 - 基本位置）を返す。
        /// ドラッグ時に視覚位置をマウスに追従させるため、マウス座標からこのオフセットを引いて基本位置に書き戻す。
        /// </summary>
        private Vector2 GetWeightOffset(int ri, int vi)
        {
            if (!_hasActiveWeights) return Vector2.zero;
            return GetDisplayPos(ri, vi) - Data.rings[ri].vertices[vi].position;
        }

        // =====================================================================
        // 描画処理
        // =====================================================================

        private void OnGUI()
        {
            DrawToolbar();

            if (currentAsset == null)
            {
                EditorGUILayout.HelpBox("PolygonDataAsset を選択または作成してください。", MessageType.Info);
                return;
            }

            UpdateResolvedPositions();

            var top = GUILayoutUtility.GetLastRect().yMax;
            var canvasRect = new Rect(0, top, position.width - _sidebarWidth - 4, position.height - top);
            var resizeRect = new Rect(canvasRect.xMax, top, 4, canvasRect.height);
            var sidebarRect = new Rect(resizeRect.xMax, top, _sidebarWidth, canvasRect.height);

            DrawCanvas(canvasRect);
            HandleResizeSplitter(resizeRect);
            DrawSidebar(sidebarRect);
        }

        // =====================================================================
        // ツールバー
        // =====================================================================

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            var newAsset = (PolygonDataAsset)EditorGUILayout.ObjectField(
                currentAsset, typeof(PolygonDataAsset), false, GUILayout.Width(250));
            if (newAsset != currentAsset)
            {
                currentAsset = newAsset;
                _selectedRing = -1;
                _selectedVertex = -1;
            }

            if (GUILayout.Button("New", EditorStyles.toolbarButton, GUILayout.Width(40)))
            {
                CreateNewAsset();
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Fit View", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                FitView();
            }

            EditorGUILayout.EndHorizontal();
        }

        // =====================================================================
        // キャンバス
        // =====================================================================

        private void DrawCanvas(Rect rect)
        {
            var evt = Event.current;

            if (evt.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rect, BgColor);

                GUI.BeginClip(rect);
                var local = new Rect(0, 0, rect.width, rect.height);
                DrawGrid(local);
                DrawNormBounds(local);
                if (Data != null)
                {
                    DrawPolygonEdges(local);
                    DrawVertexHandles(local);
                    DrawVertexLabels(local);
                }
                GUI.EndClip();
            }

            if (rect.Contains(evt.mousePosition) || _isDraggingVertex || _isPanning)
            {
                HandleCanvasInput(rect, evt);
            }
        }

        private void DrawGrid(Rect r)
        {
            var topLeft = GuiToNorm(new Vector2(r.x, r.y), r);
            var botRight = GuiToNorm(new Vector2(r.xMax, r.yMax), r);
            var uMin = Mathf.Floor(Mathf.Min(topLeft.x, botRight.x) * 4f) / 4f;
            var uMax = Mathf.Ceil(Mathf.Max(topLeft.x, botRight.x) * 4f) / 4f;
            var vMin = Mathf.Floor(Mathf.Min(topLeft.y, botRight.y) * 4f) / 4f;
            var vMax = Mathf.Ceil(Mathf.Max(topLeft.y, botRight.y) * 4f) / 4f;

            Handles.color = GridMinor;
            for (var u = uMin; u <= uMax; u += 0.25f)
            {
                if (Mathf.Abs(u % 0.5f) < 0.01f) continue;
                var a = NormToGui(u, vMin, r);
                var b = NormToGui(u, vMax, r);
                Handles.DrawLine(V3(a), V3(b));
            }
            for (var v = vMin; v <= vMax; v += 0.25f)
            {
                if (Mathf.Abs(v % 0.5f) < 0.01f) continue;
                var a = NormToGui(uMin, v, r);
                var b = NormToGui(uMax, v, r);
                Handles.DrawLine(V3(a), V3(b));
            }

            Handles.color = GridMajor;
            for (var u = Mathf.Ceil(uMin * 2f) / 2f; u <= uMax; u += 0.5f)
            {
                var a = NormToGui(u, vMin, r);
                var b = NormToGui(u, vMax, r);
                Handles.DrawLine(V3(a), V3(b));
            }
            for (var v = Mathf.Ceil(vMin * 2f) / 2f; v <= vMax; v += 0.5f)
            {
                var a = NormToGui(uMin, v, r);
                var b = NormToGui(uMax, v, r);
                Handles.DrawLine(V3(a), V3(b));
            }
        }

        private void DrawNormBounds(Rect r)
        {
            Handles.color = BoundsColor;
            var tl = NormToGui(-1, 1, r);
            var tr = NormToGui(1, 1, r);
            var bl = NormToGui(-1, 0, r);
            var br = NormToGui(1, 0, r);
            Handles.DrawDottedLine(V3(tl), V3(tr), 4f);
            Handles.DrawDottedLine(V3(tr), V3(br), 4f);
            Handles.DrawDottedLine(V3(br), V3(bl), 4f);
            Handles.DrawDottedLine(V3(bl), V3(tl), 4f);
        }

        private void DrawPolygonEdges(Rect r)
        {
            for (var ri = 0; ri < Data.rings.Count; ri++)
            {
                var ring = Data.rings[ri];
                if (ring.vertices.Count < 2) continue;

                var verts = ring.vertices;
                var isCCW = IsCCWRing(ring);
                Handles.color = isCCW ? EdgeColor : HoleColor;

                for (var i = 0; i < verts.Count; i++)
                {
                    var a = GetDisplayPos(ri, i);
                    var b = GetDisplayPos(ri, (i + 1) % verts.Count);
                    var pa = NormToGui(a.x, a.y, r);
                    var pb = NormToGui(b.x, b.y, r);
                    Handles.DrawLine(V3(pa), V3(pb));
                }
            }
        }

        private void DrawVertexHandles(Rect r)
        {
            for (var ri = 0; ri < Data.rings.Count; ri++)
            {
                var ring = Data.rings[ri];
                for (var vi = 0; vi < ring.vertices.Count; vi++)
                {
                    var pos = GetDisplayPos(ri, vi);
                    var gui = NormToGui(pos.x, pos.y, r);
                    var selected = ri == _selectedRing && vi == _selectedVertex;
                    Handles.color = selected ? VertexSelected : VertexNormal;
                    Handles.DrawSolidDisc(V3(gui), Vector3.forward, VertexHandleRadius);
                }
            }
        }

        private void DrawVertexLabels(Rect r)
        {
            var style = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            for (var ri = 0; ri < Data.rings.Count; ri++)
            {
                var ring = Data.rings[ri];
                for (var vi = 0; vi < ring.vertices.Count; vi++)
                {
                    var v = ring.vertices[vi];
                    if (string.IsNullOrEmpty(v.name)) continue;
                    var pos = GetDisplayPos(ri, vi);
                    var gui = NormToGui(pos.x, pos.y, r);
                    var labelRect = new Rect(gui.x - 30, gui.y - 22, 60, 16);
                    EditorGUI.DrawRect(labelRect, LabelBg);
                    GUI.Label(labelRect, v.name, style);
                }
            }
        }

        // =====================================================================
        // キャンバス入力
        // =====================================================================

        private void HandleCanvasInput(Rect rect, Event evt)
        {
            switch (evt.type)
            {
                case EventType.MouseDown:
                    HandleMouseDown(rect, evt);
                    break;
                case EventType.MouseDrag:
                    HandleMouseDrag(rect, evt);
                    break;
                case EventType.MouseUp:
                    HandleMouseUp(evt);
                    break;
                case EventType.ScrollWheel:
                    HandleScroll(rect, evt);
                    break;
                case EventType.KeyDown:
                    HandleKeyDown(evt);
                    break;
            }
        }

        private void HandleMouseDown(Rect rect, Event evt)
        {
            _lastMousePos = evt.mousePosition;

            if (evt.button == 2 || (evt.button == 0 && evt.alt))
            {
                _isPanning = true;
                evt.Use();
                return;
            }

            if (evt.button == 0)
            {
                var hit = FindVertexAt(evt.mousePosition, rect);
                if (hit.ring >= 0)
                {
                    _selectedRing = hit.ring;
                    _selectedVertex = hit.vertex;
                    _isDraggingVertex = true;
                    evt.Use();
                    Repaint();
                }
                else
                {
                    _selectedVertex = -1;
                    evt.Use();
                    Repaint();
                }
            }

            if (evt.button == 1 && Data != null)
            {
                var hit = FindVertexAt(evt.mousePosition, rect);
                if (hit.ring >= 0)
                {
                    ShowVertexContextMenu(hit.ring, hit.vertex);
                }
                else if (_selectedRing >= 0 && _selectedRing < Data.rings.Count)
                {
                    ShowCanvasContextMenu(rect, evt.mousePosition);
                }
                evt.Use();
            }
        }

        private void HandleMouseDrag(Rect rect, Event evt)
        {
            if (_isPanning)
            {
                var delta = evt.mousePosition - _lastMousePos;
                _viewCenter.x -= delta.x / _pixelsPerUnit;
                _viewCenter.y += delta.y / _pixelsPerUnit;
                _lastMousePos = evt.mousePosition;
                evt.Use();
                Repaint();
                return;
            }

            if (_isDraggingVertex && _selectedRing >= 0 && _selectedVertex >= 0)
            {
                var norm = GuiToNorm(evt.mousePosition, rect);
                if (evt.control)
                {
                    norm = SnapToGrid(norm);
                }

                // weight プレビュー中はマウス座標が "解決済み位置" を指している扱いになる。
                // 基本位置 = マウス座標 - weight オフセット で書き戻すと視覚位置がマウスに追従する。
                var basePos = norm - GetWeightOffset(_selectedRing, _selectedVertex);

                RecordUndo("Move Vertex");
                Data.rings[_selectedRing].vertices[_selectedVertex].position = basePos;
                MarkDirty();
                evt.Use();
                Repaint();
            }
        }

        private void HandleMouseUp(Event evt)
        {
            if (_isPanning)
            {
                _isPanning = false;
                evt.Use();
            }
            if (_isDraggingVertex)
            {
                _isDraggingVertex = false;
                evt.Use();
            }
        }

        private void HandleScroll(Rect rect, Event evt)
        {
            var norm = GuiToNorm(evt.mousePosition, rect);
            var factor = evt.delta.y > 0 ? 0.9f : 1.1f;
            _pixelsPerUnit = Mathf.Clamp(_pixelsPerUnit * factor, MinPixelsPerUnit, MaxPixelsPerUnit);
            var newNorm = GuiToNorm(evt.mousePosition, rect);
            _viewCenter.x -= newNorm.x - norm.x;
            _viewCenter.y -= newNorm.y - norm.y;
            evt.Use();
            Repaint();
        }

        private void HandleKeyDown(Event evt)
        {
            if (evt.keyCode == KeyCode.Delete && _selectedRing >= 0 && _selectedVertex >= 0 && Data != null)
            {
                DeleteSelectedVertex();
                evt.Use();
                Repaint();
            }
        }

        private (int ring, int vertex) FindVertexAt(Vector2 guiPos, Rect rect)
        {
            if (Data == null) return (-1, -1);

            var bestDist = SelectDistSq;
            var bestR = -1;
            var bestV = -1;
            for (var ri = 0; ri < Data.rings.Count; ri++)
            {
                var ring = Data.rings[ri];
                for (var vi = 0; vi < ring.vertices.Count; vi++)
                {
                    var pos = GetDisplayPos(ri, vi);
                    var gui = NormToGui(pos.x, pos.y, rect);
                    var d = (gui - guiPos).sqrMagnitude;
                    if (d < bestDist)
                    {
                        bestDist = d;
                        bestR = ri;
                        bestV = vi;
                    }
                }
            }
            return (bestR, bestV);
        }

        private void ShowVertexContextMenu(int ri, int vi)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Delete Vertex"), false, () =>
            {
                _selectedRing = ri;
                _selectedVertex = vi;
                DeleteSelectedVertex();
                Repaint();
            });
            menu.ShowAsContext();
        }

        private void ShowCanvasContextMenu(Rect rect, Vector2 guiPos)
        {
            var norm = GuiToNorm(guiPos, rect);
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Add Vertex Here"), false, () =>
            {
                AddVertexToSelectedRing(norm);
                Repaint();
            });
            menu.ShowAsContext();
        }

        // =====================================================================
        // サイドバー
        // =====================================================================

        private void DrawSidebar(Rect rect)
        {
            GUILayout.BeginArea(rect);
            _sidebarScroll = EditorGUILayout.BeginScrollView(_sidebarScroll);

            if (Data != null)
            {
                DrawRingsSection();
                EditorGUILayout.Space(8);
                DrawVertexGroupsSection();
            }

            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawRingsSection()
        {
            EditorGUILayout.LabelField("Rings", EditorStyles.boldLabel);

            for (var ri = 0; ri < Data.rings.Count; ri++)
            {
                var ring = Data.rings[ri];
                var key = $"ring_{ri}";
                var isCCW = IsCCWRing(ring);
                var windLabel = ring.vertices.Count < 3 ? "" : (isCCW ? " [CCW]" : " [CW]");
                var headerColor = _selectedRing == ri ? ">> " : "";

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                if (Foldout(key, $"{headerColor}{ring.label}{windLabel}"))
                {
                    EditorGUI.indentLevel++;

                    EditorGUI.BeginChangeCheck();
                    var label = EditorGUILayout.TextField("Label", ring.label);
                    if (EditorGUI.EndChangeCheck())
                    {
                        RecordUndo("Rename Ring");
                        ring.label = label;
                        MarkDirty();
                    }

                    if (_selectedRing != ri)
                    {
                        if (GUILayout.Button("Select This Ring", GUILayout.Height(20)))
                        {
                            _selectedRing = ri;
                            _selectedVertex = -1;
                            Repaint();
                        }
                    }

                    for (var vi = 0; vi < ring.vertices.Count; vi++)
                    {
                        DrawVertexRow(ri, vi, ring.vertices[vi]);
                    }

                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("+ Vertex", GUILayout.Height(18)))
                    {
                        _selectedRing = ri;
                        AddVertexToSelectedRing(DefaultVertexPosition(ring));
                    }
                    if (ring.vertices.Count >= 3 && GUILayout.Button("Reverse", GUILayout.Height(18)))
                    {
                        RecordUndo("Reverse Ring");
                        ring.vertices.Reverse();
                        ring.EnsureEdgeCount();
                        MarkDirty();
                        Repaint();
                    }
                    EditorGUILayout.EndHorizontal();

                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+ Triangle"))
            {
                AddPresetRing(PresetTriangle());
            }
            if (GUILayout.Button("+ Rectangle"))
            {
                AddPresetRing(PresetRectangle());
            }
            if (GUILayout.Button("+ Hole (CW Rect)"))
            {
                AddPresetRing(PresetHole());
            }
            EditorGUILayout.EndHorizontal();

            if (Data.rings.Count > 0 && _selectedRing >= 0 && _selectedRing < Data.rings.Count)
            {
                if (GUILayout.Button("Remove Selected Ring"))
                {
                    RecordUndo("Remove Ring");
                    Data.rings.RemoveAt(_selectedRing);
                    _selectedRing = -1;
                    _selectedVertex = -1;
                    MarkDirty();
                    Repaint();
                }
            }
        }

        private void DrawVertexRow(int ri, int vi, PolygonVertex vert)
        {
            var isSelected = ri == _selectedRing && vi == _selectedVertex;
            if (isSelected)
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
            }

            EditorGUI.BeginChangeCheck();
            var name = EditorGUILayout.TextField(vert.name, GUILayout.Width(60));
            var x = EditorGUILayout.FloatField(vert.position.x, GUILayout.Width(50));
            var y = EditorGUILayout.FloatField(vert.position.y, GUILayout.Width(50));
            if (EditorGUI.EndChangeCheck())
            {
                RecordUndo("Edit Vertex");
                vert.name = name;
                vert.position = new Vector2(x, y);
                MarkDirty();
                Repaint();
            }

            if (GUILayout.Button("Sel", GUILayout.Width(30), GUILayout.Height(18)))
            {
                _selectedRing = ri;
                _selectedVertex = vi;
                Repaint();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawVertexGroupsSection()
        {
            EditorGUILayout.LabelField("Vertex Groups", EditorStyles.boldLabel);

            for (var gi = 0; gi < Data.vertexGroups.Count; gi++)
            {
                var group = Data.vertexGroups[gi];
                var key = $"vg_{gi}";

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                if (Foldout(key, group.name))
                {
                    EditorGUI.indentLevel++;

                    EditorGUI.BeginChangeCheck();
                    var name = EditorGUILayout.TextField("Name", group.name);
                    if (EditorGUI.EndChangeCheck())
                    {
                        RecordUndo("Rename Vertex Group");
                        group.name = name;
                        MarkDirty();
                    }

                    // プレビュー用 weight スライダー。
                    if (!_previewWeights.TryGetValue(group.name, out var pw))
                    {
                        pw = 0f;
                    }
                    var newPw = EditorGUILayout.Slider("Preview Weight", pw, 0f, 1f);
                    if (!Mathf.Approximately(newPw, pw))
                    {
                        _previewWeights[group.name] = newPw;
                        Repaint();
                    }

                    var vertexNames = CollectAllVertexNames();
                    for (var di = 0; di < group.deltas.Count; di++)
                    {
                        DrawDeltaRow(gi, di, group.deltas[di], vertexNames);
                    }

                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("+ Delta", GUILayout.Height(18)))
                    {
                        RecordUndo("Add Delta");
                        group.deltas.Add(new VertexDelta("", Vector2.zero));
                        MarkDirty();
                    }
                    if (GUILayout.Button("Remove Group", GUILayout.Height(18)))
                    {
                        RecordUndo("Remove Vertex Group");
                        Data.vertexGroups.RemoveAt(gi);
                        MarkDirty();
                        GUIUtility.ExitGUI();
                    }
                    EditorGUILayout.EndHorizontal();

                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.EndVertical();
            }

            if (GUILayout.Button("+ Add Vertex Group"))
            {
                RecordUndo("Add Vertex Group");
                Data.vertexGroups.Add(new VertexGroup { name = $"Group{Data.vertexGroups.Count}" });
                MarkDirty();
            }
        }

        private void DrawDeltaRow(int gi, int di, VertexDelta delta, string[] names)
        {
            EditorGUILayout.BeginHorizontal();

            var idx = System.Array.IndexOf(names, delta.vertexName);
            if (idx < 0) idx = 0;

            EditorGUI.BeginChangeCheck();
            var newIdx = EditorGUILayout.Popup(idx, names, GUILayout.Width(70));
            var dx = EditorGUILayout.FloatField(delta.delta.x, GUILayout.Width(50));
            var dy = EditorGUILayout.FloatField(delta.delta.y, GUILayout.Width(50));
            if (EditorGUI.EndChangeCheck())
            {
                RecordUndo("Edit Delta");
                if (newIdx >= 0 && newIdx < names.Length)
                {
                    delta.vertexName = names[newIdx];
                }
                delta.delta = new Vector2(dx, dy);
                MarkDirty();
                Repaint();
            }

            if (GUILayout.Button("✕", GUILayout.Width(20), GUILayout.Height(18)))
            {
                RecordUndo("Remove Delta");
                Data.vertexGroups[gi].deltas.RemoveAt(di);
                MarkDirty();
                GUIUtility.ExitGUI();
            }

            EditorGUILayout.EndHorizontal();
        }

        // =====================================================================
        // 分割バー
        // =====================================================================

        private void HandleResizeSplitter(Rect rect)
        {
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.ResizeHorizontal);
            EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.12f));
            var evt = Event.current;

            if (evt.type == EventType.MouseDown && rect.Contains(evt.mousePosition))
            {
                _isResizingSidebar = true;
                evt.Use();
            }
            if (_isResizingSidebar)
            {
                if (evt.type == EventType.MouseDrag)
                {
                    _sidebarWidth = position.width - evt.mousePosition.x;
                    _sidebarWidth = Mathf.Clamp(_sidebarWidth, 200, position.width - 300);
                    evt.Use();
                    Repaint();
                }
                if (evt.type == EventType.MouseUp)
                {
                    _isResizingSidebar = false;
                    evt.Use();
                }
            }
        }

        // =====================================================================
        // 頂点 / リング管理
        // =====================================================================

        private void AddVertexToSelectedRing(Vector2 normPos)
        {
            if (Data == null || _selectedRing < 0 || _selectedRing >= Data.rings.Count) return;

            RecordUndo("Add Vertex");
            var ring = Data.rings[_selectedRing];
            var name = GenerateVertexName();
            var insertIdx = _selectedVertex >= 0 ? _selectedVertex + 1 : ring.vertices.Count;
            ring.vertices.Insert(insertIdx, new PolygonVertex(name, normPos));
            ring.EnsureEdgeCount();
            _selectedVertex = insertIdx;
            MarkDirty();
            Repaint();
        }

        private void DeleteSelectedVertex()
        {
            if (Data == null || _selectedRing < 0 || _selectedVertex < 0) return;
            var ring = Data.rings[_selectedRing];
            if (_selectedVertex >= ring.vertices.Count) return;

            RecordUndo("Delete Vertex");
            ring.vertices.RemoveAt(_selectedVertex);
            ring.EnsureEdgeCount();
            if (_selectedVertex >= ring.vertices.Count)
            {
                _selectedVertex = ring.vertices.Count - 1;
            }
            MarkDirty();
        }

        private string GenerateVertexName()
        {
            var existing = new HashSet<string>();
            if (Data != null)
            {
                foreach (var ring in Data.rings)
                {
                    foreach (var v in ring.vertices)
                    {
                        if (!string.IsNullOrEmpty(v.name))
                        {
                            existing.Add(v.name);
                        }
                    }
                }
            }
            for (var i = 0; ; i++)
            {
                var name = $"v{i}";
                if (!existing.Contains(name)) return name;
            }
        }

        private string[] CollectAllVertexNames()
        {
            var names = new List<string>();
            if (Data != null)
            {
                foreach (var ring in Data.rings)
                {
                    foreach (var v in ring.vertices)
                    {
                        if (!string.IsNullOrEmpty(v.name) && !names.Contains(v.name))
                        {
                            names.Add(v.name);
                        }
                    }
                }
            }
            if (names.Count == 0) names.Add("(none)");
            return names.ToArray();
        }

        private Vector2 DefaultVertexPosition(PolygonRing ring)
        {
            if (ring.vertices.Count == 0) return new Vector2(0f, 0.5f);
            var sum = Vector2.zero;
            foreach (var v in ring.vertices) sum += v.position;
            return sum / ring.vertices.Count;
        }

        private bool IsCCWRing(PolygonRing ring)
        {
            if (ring.vertices.Count < 3) return true;
            var pts = new Vector2[ring.vertices.Count];
            for (var i = 0; i < pts.Length; i++) pts[i] = ring.vertices[i].position;
            return PolygonMath.IsCCW(pts);
        }

        // =====================================================================
        // プリセット形状
        // =====================================================================

        private PolygonRing PresetTriangle()
        {
            var ring = new PolygonRing { label = "Triangle" };
            ring.vertices.Add(new PolygonVertex("v" + NextVertexId(), new Vector2(0f, 0f)));
            ring.vertices.Add(new PolygonVertex("v" + NextVertexId(), new Vector2(1f, 1f)));
            ring.vertices.Add(new PolygonVertex("v" + NextVertexId(), new Vector2(-1f, 1f)));
            ring.EnsureEdgeCount();
            return ring;
        }

        private PolygonRing PresetRectangle()
        {
            var ring = new PolygonRing { label = "Rectangle" };
            ring.vertices.Add(new PolygonVertex("v" + NextVertexId(), new Vector2(-1f, 0f)));
            ring.vertices.Add(new PolygonVertex("v" + NextVertexId(), new Vector2(1f, 0f)));
            ring.vertices.Add(new PolygonVertex("v" + NextVertexId(), new Vector2(1f, 1f)));
            ring.vertices.Add(new PolygonVertex("v" + NextVertexId(), new Vector2(-1f, 1f)));
            ring.EnsureEdgeCount();
            return ring;
        }

        private PolygonRing PresetHole()
        {
            var ring = new PolygonRing { label = "Hole" };
            // 穴として扱うため、時計回りにする。
            ring.vertices.Add(new PolygonVertex("v" + NextVertexId(), new Vector2(-0.5f, 0.25f)));
            ring.vertices.Add(new PolygonVertex("v" + NextVertexId(), new Vector2(-0.5f, 0.75f)));
            ring.vertices.Add(new PolygonVertex("v" + NextVertexId(), new Vector2(0.5f, 0.75f)));
            ring.vertices.Add(new PolygonVertex("v" + NextVertexId(), new Vector2(0.5f, 0.25f)));
            ring.EnsureEdgeCount();
            return ring;
        }

        private int _nextVertexId;

        private int NextVertexId()
        {
            var existing = new HashSet<string>();
            if (Data != null)
            {
                foreach (var ring in Data.rings)
                {
                    foreach (var v in ring.vertices)
                    {
                        if (!string.IsNullOrEmpty(v.name)) existing.Add(v.name);
                    }
                }
            }
            while (existing.Contains("v" + _nextVertexId)) _nextVertexId++;
            return _nextVertexId++;
        }

        private void AddPresetRing(PolygonRing ring)
        {
            RecordUndo("Add Ring");
            Data.rings.Add(ring);
            _selectedRing = Data.rings.Count - 1;
            _selectedVertex = -1;
            MarkDirty();
            Repaint();
        }

        // =====================================================================
        // アセット管理
        // =====================================================================

        private void CreateNewAsset()
        {
            var path = EditorUtility.SaveFilePanelInProject(
                "Create Polygon Shape", "PolygonShape", "asset", "Save polygon shape asset");
            if (string.IsNullOrEmpty(path)) return;

            var asset = CreateInstance<PolygonDataAsset>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            currentAsset = asset;
            _selectedRing = -1;
            _selectedVertex = -1;
            Repaint();
        }

        private void FitView()
        {
            if (Data == null || Data.rings.Count == 0)
            {
                _viewCenter = new Vector2(0f, 0.5f);
                _pixelsPerUnit = 200f;
                return;
            }

            // weight プレビュー中は変形後の形状に合わせる。
            var resolved = Data.Resolve(_hasActiveWeights ? _previewWeights : null);
            var uRange = Mathf.Max(0.1f, resolved.maxU - resolved.minU);
            var vRange = Mathf.Max(0.1f, resolved.maxV - resolved.minV);
            _viewCenter = new Vector2(
                (resolved.minU + resolved.maxU) * 0.5f,
                (resolved.minV + resolved.maxV) * 0.5f);

            var canvasW = position.width - _sidebarWidth - 4;
            var canvasH = position.height - 30;
            _pixelsPerUnit = Mathf.Min(
                canvasW * 0.8f / uRange,
                canvasH * 0.8f / vRange);
            _pixelsPerUnit = Mathf.Clamp(_pixelsPerUnit, MinPixelsPerUnit, MaxPixelsPerUnit);
            Repaint();
        }

        // =====================================================================
        // ヘルパー
        // =====================================================================

        private static Vector3 V3(Vector2 v) => new Vector3(v.x, v.y, 0f);

        private static Vector2 SnapToGrid(Vector2 pos)
        {
            const float snap = 0.05f;
            return new Vector2(
                Mathf.Round(pos.x / snap) * snap,
                Mathf.Round(pos.y / snap) * snap);
        }
    }
}
#endif

using System;
using System.Collections.Generic;
using UnityEngine;

namespace MitarashiDango.RoadAssetGenerator
{
    public enum PolygonEdgeType { Linear }

    [Serializable]
    public class PolygonVertex
    {
        public string name;
        public Vector2 position;

        public PolygonVertex() { }

        public PolygonVertex(string name, Vector2 position)
        {
            this.name = name;
            this.position = position;
        }

        public PolygonVertex Clone()
        {
            return new PolygonVertex(name, position);
        }
    }

    [Serializable]
    public class PolygonEdge
    {
        public PolygonEdgeType type = PolygonEdgeType.Linear;

        public PolygonEdge Clone()
        {
            return new PolygonEdge { type = type };
        }
    }

    /// <summary>
    /// 閉じた多角形リング。頂点リストと辺リストで構成される。
    /// 辺数 == 頂点数（最後の辺は最終頂点→先頭頂点を接続）。
    /// CCW (反時計回り) = 外周、CW (時計回り) = 穴。
    /// </summary>
    [Serializable]
    public class PolygonRing
    {
        public string label = "Ring";
        public List<PolygonVertex> vertices = new List<PolygonVertex>();
        public List<PolygonEdge> edges = new List<PolygonEdge>();

        public void EnsureEdgeCount()
        {
            while (edges.Count < vertices.Count)
            {
                edges.Add(new PolygonEdge());
            }
            while (edges.Count > vertices.Count)
            {
                edges.RemoveAt(edges.Count - 1);
            }
        }

        public PolygonRing Clone()
        {
            var clone = new PolygonRing { label = label };
            foreach (var v in vertices)
            {
                clone.vertices.Add(v.Clone());
            }
            foreach (var e in edges)
            {
                clone.edges.Add(e.Clone());
            }
            return clone;
        }
    }

    [Serializable]
    public class VertexDelta
    {
        public string vertexName;
        public Vector2 delta;

        public VertexDelta() { }

        public VertexDelta(string vertexName, Vector2 delta)
        {
            this.vertexName = vertexName;
            this.delta = delta;
        }

        public VertexDelta Clone()
        {
            return new VertexDelta(vertexName, delta);
        }
    }

    /// <summary>
    /// BlendShape ライクな頂点グループ。名前付きデルタ群を持ち、
    /// weight (0〜1) を掛けて基本位置に加算することで頂点を変形する。
    /// </summary>
    [Serializable]
    public class VertexGroup
    {
        public string name = "Group";
        public List<VertexDelta> deltas = new List<VertexDelta>();

        public VertexGroup Clone()
        {
            var clone = new VertexGroup { name = name };
            foreach (var d in deltas)
            {
                clone.deltas.Add(d.Clone());
            }
            return clone;
        }
    }

    [Serializable]
    public class PolygonData
    {
        public List<PolygonRing> rings = new List<PolygonRing>();
        public List<VertexGroup> vertexGroups = new List<VertexGroup>();

        public PolygonData Clone()
        {
            var clone = new PolygonData();
            foreach (var r in rings)
            {
                clone.rings.Add(r.Clone());
            }
            foreach (var g in vertexGroups)
            {
                clone.vertexGroups.Add(g.Clone());
            }
            return clone;
        }

        /// <summary>
        /// 頂点グループの weight を適用して <see cref="ResolvedPolygon"/> を生成する。
        /// weights が null または空の場合は基本位置のみを使用する。
        /// </summary>
        public ResolvedPolygon Resolve(Dictionary<string, float> weights = null)
        {
            var deltaAccum = new Dictionary<string, Vector2>();
            if (weights != null)
            {
                foreach (var group in vertexGroups)
                {
                    if (!weights.TryGetValue(group.name, out var w) || Mathf.Approximately(w, 0f))
                    {
                        continue;
                    }
                    foreach (var d in group.deltas)
                    {
                        if (string.IsNullOrEmpty(d.vertexName))
                        {
                            continue;
                        }
                        if (deltaAccum.TryGetValue(d.vertexName, out var existing))
                        {
                            deltaAccum[d.vertexName] = existing + d.delta * w;
                        }
                        else
                        {
                            deltaAccum[d.vertexName] = d.delta * w;
                        }
                    }
                }
            }

            var resolvedRings = new Vector2[rings.Count][];
            for (var i = 0; i < rings.Count; i++)
            {
                var ring = rings[i];
                var positions = new Vector2[ring.vertices.Count];
                for (var j = 0; j < ring.vertices.Count; j++)
                {
                    var pos = ring.vertices[j].position;
                    var vName = ring.vertices[j].name;
                    if (!string.IsNullOrEmpty(vName) && deltaAccum.TryGetValue(vName, out var delta))
                    {
                        pos += delta;
                    }
                    positions[j] = pos;
                }
                resolvedRings[i] = positions;
            }

            return new ResolvedPolygon(resolvedRings);
        }
    }

    /// <summary>
    /// 頂点グループ適用済みのポリゴンデータ。<see cref="PolygonPrimitive"/> の
    /// コンストラクタに渡して使用する。AABB をキャッシュし、高速な事前棄却を可能にする。
    /// </summary>
    public sealed class ResolvedPolygon
    {
        public readonly Vector2[][] rings;
        public readonly float minU;
        public readonly float maxU;
        public readonly float minV;
        public readonly float maxV;

        public ResolvedPolygon(Vector2[][] rings)
        {
            this.rings = rings;
            PolygonMath.ComputeAABB(rings, out minU, out maxU, out minV, out maxV);
        }
    }
}

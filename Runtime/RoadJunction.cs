using System;
using System.Collections.Generic;
using UnityEngine;

namespace MitarashiDango.RoadAssetGenerator
{
    public enum SegmentEnd { Start, End }
    public enum ConnectorJunctionKind { Merge, Split, Ramp }

    /// <summary>道路接続部の抽象基底。MVP ではデータ保持のみ行う。</summary>
    public abstract class RoadJunction : RoadNetworkElement
    {
        public JunctionPort[] ports = Array.Empty<JunctionPort>();
        public GameObject generatedRoot;
        public List<GameObject> generatedObjects = new List<GameObject>();
    }

    /// <summary>交差点用の接続部。MVP では生成処理を持たない。</summary>
    [DisallowMultipleComponent]
    public class IntersectionJunction : RoadJunction
    {
        [Min(0f)] public float cornerRadiusMeters = 3f;
    }

    /// <summary>合流、分岐、ランプ用の接続部。MVP では生成処理を持たない。</summary>
    [DisallowMultipleComponent]
    public class ConnectorJunction : RoadJunction
    {
        public ConnectorJunctionKind kind = ConnectorJunctionKind.Merge;
    }

    /// <summary>ジャンクション上の道路区間接続口。</summary>
    [Serializable]
    public class JunctionPort
    {
        public RoadSegment segment;
        public SegmentEnd segmentEnd = SegmentEnd.Start;
        public Vector3 localDirection = Vector3.forward;
        public LaneConnection[] laneConnections = Array.Empty<LaneConnection>();
    }

    /// <summary>将来の交差点生成で利用するレーン間接続。</summary>
    [Serializable]
    public class LaneConnection
    {
        public int fromLaneIndex;
        public int toLaneIndex;
    }
}

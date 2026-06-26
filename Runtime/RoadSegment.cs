using System;
using System.Collections.Generic;
using UnityEngine;

namespace MitarashiDango.RoadAssetGenerator
{
    /// <summary>スプラインで定義される道路区間。</summary>
    [DisallowMultipleComponent]
    public class RoadSegment : RoadNetworkElement
    {
        public SplinePoint[] controlPoints =
        {
            new SplinePoint(new Vector3(0f, 0f, 0f)),
            new SplinePoint(new Vector3(0f, 0f, 20f)),
        };

        public RoadProfileKey[] profileKeys =
        {
            new RoadProfileKey(),
        };

        public bool useSurfaceStyle;
        public RoadSurfaceStyle surfaceStyle = RoadSurfaceStyle.CreateDefault();

        public RoadSegmentConnection startConnection = new RoadSegmentConnection();
        public RoadSegmentConnection endConnection = new RoadSegmentConnection();

        public bool overrideSurfaceSamplingSettings;
        [Min(0.25f)] public float maxSurfaceSampleLengthMeters = 1f;
        [Range(1f, 45f)] public float maxSurfaceSampleAngleDegrees = 4f;
        public bool overrideGeneratedSurfaceLayer;
        [Range(0, 31)] public int generatedSurfaceLayer;
        public bool overrideGeneratedMarkingLayer;
        [Range(0, 31)] public int generatedMarkingLayer;

        public GameObject surfacesRoot;
        public GameObject markingsRoot;
        public List<GameObject> generatedSurfaceObjects = new List<GameObject>();
        public List<GameObject> generatedMarkingObjects = new List<GameObject>();

        /// <summary>制御点が 2 点以上ある場合にスプラインを作成する。</summary>
        public bool TryCreateSpline(out CatmullRomSpline spline)
        {
            if (controlPoints == null || controlPoints.Length < 2)
            {
                spline = null;
                return false;
            }

            spline = new CatmullRomSpline(controlPoints);
            return spline.IsValid;
        }

        /// <summary>MVP 生成で利用する先頭プロファイルを取得する。</summary>
        public RoadProfile GetActiveProfile()
        {
            if (profileKeys == null || profileKeys.Length == 0 || profileKeys[0] == null)
            {
                return null;
            }

            return profileKeys[0].profile;
        }
    }

    /// <summary>道路区間端点の接続先キャッシュ。接続口側の情報を正本とし、この値は検索補助として扱う。</summary>
    [Serializable]
    public class RoadSegmentConnection
    {
        public RoadJunction junction;
        public int portIndex = -1;

        public bool IsConnected => junction != null && portIndex >= 0;
    }
}

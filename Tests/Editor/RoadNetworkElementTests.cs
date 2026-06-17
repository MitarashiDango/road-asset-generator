using NUnit.Framework;
using UnityEngine;

namespace MitarashiDango.RoadAssetGenerator.Tests
{
    public class RoadNetworkElementTests
    {
        [Test]
        public void NetworkCacheRefreshesWhenParentChanges()
        {
            var firstNetworkObject = new GameObject("RoadNetwork_First");
            var secondNetworkObject = new GameObject("RoadNetwork_Second");
            var segmentObject = new GameObject("RoadSegment_Cache_Test");
            try
            {
                var firstNetwork = firstNetworkObject.AddComponent<RoadNetwork>();
                var secondNetwork = secondNetworkObject.AddComponent<RoadNetwork>();
                var segment = segmentObject.AddComponent<RoadSegment>();

                segmentObject.transform.SetParent(firstNetworkObject.transform);
                Assert.That(segment.Network, Is.EqualTo(firstNetwork));

                segmentObject.transform.SetParent(secondNetworkObject.transform);
                Assert.That(segment.Network, Is.EqualTo(secondNetwork));
            }
            finally
            {
                Object.DestroyImmediate(segmentObject);
                Object.DestroyImmediate(firstNetworkObject);
                Object.DestroyImmediate(secondNetworkObject);
            }
        }
    }
}

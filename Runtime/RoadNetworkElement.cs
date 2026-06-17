using UnityEngine;

namespace MitarashiDango.RoadAssetGenerator
{
    /// <summary>道路ネットワーク構成要素の共通基底。</summary>
    public abstract class RoadNetworkElement : MonoBehaviour
    {
        [SerializeField] private RoadNetwork roadNetwork;

        /// <summary>親階層から見つけた道路ネットワークのキャッシュ。</summary>
        public RoadNetwork Network
        {
            get
            {
                if (roadNetwork == null || !transform.IsChildOf(roadNetwork.transform))
                {
                    RefreshNetworkCache();
                }
                return roadNetwork;
            }
        }

        /// <summary>親階層の <see cref="RoadNetwork"/> を再検索してキャッシュする。</summary>
        public void RefreshNetworkCache()
        {
            roadNetwork = GetComponentInParent<RoadNetwork>();
        }

        protected virtual void Reset()
        {
            RefreshNetworkCache();
        }

        protected virtual void OnValidate()
        {
            RefreshNetworkCache();
        }

        protected virtual void OnTransformParentChanged()
        {
            RefreshNetworkCache();
        }
    }
}

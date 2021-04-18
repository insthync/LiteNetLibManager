using System.Collections.Generic;
using UnityEngine;

namespace LiteNetLibManager
{
    public class DefaultInterestManager : BaseInterestManager
    {
        [Tooltip("Default visible range will be used when Identity's visible range is <= 0f")]
        public float defaultVisibleRange = 30f;
        [Tooltip("Update every ? seconds")]
        public float updateInterval = 1f;

        private float countDown = 0f;

        private void Update()
        {
            if (!IsServer)
            {
                // Update at server only
                return;
            }
            countDown -= Time.unscaledDeltaTime;
            if (countDown <= 0)
            {
                countDown = updateInterval;
                HashSet<uint> subscribings = new HashSet<uint>();
                foreach (LiteNetLibPlayer player in Manager.GetPlayers())
                {
                    if (!player.IsReady)
                    {
                        // Don't subscribe if player not ready
                        continue;
                    }
                    foreach (LiteNetLibIdentity playerObject in player.GetSpawnedObjects())
                    {
                        // Update subscribing list, it will unsubscribe objects which is not in this list
                        subscribings.Clear();
                        foreach (LiteNetLibIdentity spawnedObject in Manager.Assets.GetSpawnedObjects())
                        {
                            if (ShouldSubscribe(playerObject, spawnedObject))
                                subscribings.Add(spawnedObject.ObjectId);
                        }
                        playerObject.UpdateSubscribings(subscribings);
                    }
                }
            }
        }

        public override bool Subscribe(LiteNetLibIdentity subscriber, LiteNetLibIdentity target)
        {
            if (ShouldSubscribe(subscriber, target))
            {
                subscriber.AddSubscribing(target.ObjectId);
                return true;
            }
            return false;
        }

        public bool ShouldSubscribe(LiteNetLibIdentity subscriber, LiteNetLibIdentity target)
        {
            float range = defaultVisibleRange;
            if (target.VisibleRange > 0f)
                range = target.VisibleRange;
            return subscriber.ConnectionId != target.ConnectionId && (target.AlwaysVisible || Vector3.Distance(subscriber.transform.position, target.transform.position) <= range);
        }
    }
}

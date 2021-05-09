using System.Collections.Generic;
using UnityEngine;

namespace LiteNetLibManager
{
    public class DefaultInterestManager : BaseInterestManager
    {
        [Tooltip("Update every ? seconds")]
        public float updateInterval = 1f;

        private float updateCountDown = 0f;

        private void Update()
        {
            if (!IsServer)
            {
                // Update at server only
                return;
            }
            updateCountDown -= Time.unscaledDeltaTime;
            if (updateCountDown <= 0f)
            {
                updateCountDown = updateInterval;
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
    }
}

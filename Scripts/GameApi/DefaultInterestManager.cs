using System.Collections.Generic;
using UnityEngine;

namespace LiteNetLibManager
{
    public class DefaultInterestManager : BaseInterestManager
    {
        [Tooltip("Update every ? seconds")]
        public float updateInterval = 1f;

        private float _updateCountDown;

        public override void Setup(LiteNetLibGameManager manager)
        {
            base.Setup(manager);
            _updateCountDown = updateInterval;
        }

        public override void UpdateInterestManagementImmediate()
        {
            _updateCountDown = 0f;
            UpdateInterestManagement(0f);
        }

        public override void UpdateInterestManagement(float deltaTime)
        {
            _updateCountDown -= deltaTime;
            if (_updateCountDown > 0)
                return;
            _updateCountDown = updateInterval;
            HashSet<uint> subscribings = new HashSet<uint>();
            foreach (KeyValuePair<long, LiteNetLibPlayer> playerKvp in Manager.Players)
            {
                LiteNetLibPlayer player = playerKvp.Value;
                if (!player.IsReady)
                {
                    // Don't subscribe if player not ready
                    continue;
                }
                foreach (KeyValuePair<uint, LiteNetLibIdentity> playerObjKvp in player.SpawnedObjects)
                {
                    LiteNetLibIdentity playerObj = playerObjKvp.Value;
                    // Update subscribing list, it will unsubscribe objects which is not in this list
                    subscribings.Clear();
                    foreach (KeyValuePair<uint, LiteNetLibIdentity> spawnedKvp in Manager.Assets.SpawnedObjects)
                    {
                        LiteNetLibIdentity spawnedObj = spawnedKvp.Value;
                        if (ShouldSubscribe(playerObj, spawnedObj))
                            subscribings.Add(spawnedObj.ObjectId);
                    }
                    playerObj.UpdateSubscribings(subscribings);
                }
            }
        }
    }
}

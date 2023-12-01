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

        public override void UpdateInterestManagement(float deltaTime)
        {
            _updateCountDown -= deltaTime;
            if (_updateCountDown > 0)
                return;
            _updateCountDown = updateInterval;
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

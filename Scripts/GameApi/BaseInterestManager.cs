using System.Collections.Generic;
using UnityEngine;

namespace LiteNetLibManager
{
    [DisallowMultipleComponent]
    public abstract class BaseInterestManager : MonoBehaviour
    {
        [Tooltip("Default visible range will be used when Identity's visible range is <= 0f")]
        public float defaultVisibleRange = 80f;
        public LiteNetLibGameManager Manager { get; protected set; }
        public bool IsServer { get { return Manager.IsServer; } }

        public virtual void Setup(LiteNetLibGameManager manager)
        {
            Manager = manager;
        }

        public abstract void UpdateInterestManagementImmediate();
        public abstract void UpdateInterestManagement(float deltaTime);

        public void NotifyNewObject(LiteNetLibIdentity newObject)
        {
            if (!IsServer)
            {
                // Notifies by server only
                return;
            }
            // Subscribe old objects, if it should
            if (newObject.ConnectionId >= 0 && newObject.Player.IsReady)
            {
                foreach (KeyValuePair<uint, LiteNetLibIdentity> spawnedObjKvp in Manager.Assets.SpawnedObjects)
                {
                    // Subscribe, it may subscribe or may not, up to how subscribe function implemented
                    Subscribe(newObject, spawnedObjKvp.Value);
                }
            }
            // Notify to other players
            foreach (KeyValuePair<long, LiteNetLibPlayer> playerKvp in Manager.Players)
            {
                LiteNetLibPlayer player = playerKvp.Value;
                if (!player.IsReady || player.ConnectionId == newObject.ConnectionId)
                {
                    // Don't subscribe if player not ready or the object is owned by the player
                    continue;
                }
                foreach (KeyValuePair<uint, LiteNetLibIdentity> playerObjKvp in player.SpawnedObjects)
                {
                    LiteNetLibIdentity playerObj = playerObjKvp.Value;
                    // Subscribe, it may subscribe or may not, up to how subscribe function implemented
                    Subscribe(playerObj, newObject);
                }
            }
        }

        public float GetVisibleRange(LiteNetLibIdentity identity)
        {
            return identity.AlwaysVisible ? float.MaxValue : (identity.VisibleRange > 0f ? identity.VisibleRange : defaultVisibleRange);
        }

        public virtual bool ShouldSubscribe(LiteNetLibIdentity subscriber, LiteNetLibIdentity target, bool checkRange = true)
        {
            if (subscriber == null || target == null || subscriber.ConnectionId < 0)
                return false;
            if (subscriber.ConnectionId == target.ConnectionId)
                return true;
            return !target.IsHideFrom(subscriber) && (!checkRange || target.AlwaysVisible || Vector3.Distance(subscriber.transform.position, target.transform.position) <= GetVisibleRange(target));
        }

        public virtual bool Subscribe(LiteNetLibIdentity subscriber, LiteNetLibIdentity target)
        {
            if (ShouldSubscribe(subscriber, target))
            {
                subscriber.AddSubscribing(target.ObjectId);
                return true;
            }
            return false;
        }
    }
}

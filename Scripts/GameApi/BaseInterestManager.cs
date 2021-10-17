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

        protected virtual void Awake()
        {
            Manager = GetComponent<LiteNetLibGameManager>();
        }

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
                foreach (LiteNetLibIdentity target in Manager.Assets.GetSpawnedObjects())
                {
                    // Subscribe, it may subscribe or may not, up to how subscribe function implemented
                    Subscribe(newObject, target);
                }
            }
            // Notify to other players
            foreach (LiteNetLibPlayer player in Manager.GetPlayers())
            {
                if (!player.IsReady || player.ConnectionId == newObject.ConnectionId)
                {
                    // Don't subscribe if player not ready or the object is owned by the player
                    continue;
                }
                foreach (LiteNetLibIdentity subscriber in player.GetSpawnedObjects())
                {
                    // Subscribe, it may subscribe or may not, up to how subscribe function implemented
                    Subscribe(subscriber, newObject);
                }
            }
        }

        public float GetVisibleRange(LiteNetLibIdentity identity)
        {
            return identity.VisibleRange > 0f ? identity.VisibleRange : defaultVisibleRange;
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

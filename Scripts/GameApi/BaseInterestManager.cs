using UnityEngine;

namespace LiteNetLibManager
{
    [DisallowMultipleComponent]
    public abstract class BaseInterestManager : MonoBehaviour
    {
        [Tooltip("Default visible range will be used when Identity's visible range is <= 0f")]
        public float defaultVisibleRange = 30f;
        public LiteNetLibGameManager Manager { get; protected set; }
        public bool IsServer { get { return Manager.IsServer; } }

        private void Awake()
        {
            Manager = GetComponent<LiteNetLibGameManager>();
        }

        public abstract bool Subscribe(LiteNetLibIdentity subscriber, LiteNetLibIdentity target);

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
    }
}

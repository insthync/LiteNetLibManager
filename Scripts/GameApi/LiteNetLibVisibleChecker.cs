using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LiteNetLibManager
{
    public class LiteNetLibVisibleChecker : LiteNetLibBehaviour
    {
        public enum CheckMethod
        {
            Physics3D,
            Physics2D
        };
        public int range = 10;
        public float updateInterval = 1.0f;
        public CheckMethod checkMethod = CheckMethod.Physics3D;
        public LayerMask layerMask = -1;

        private float tempUpdateTime;
        private float lastUpdateTime;
        private LiteNetLibIdentity tempIdentity;
        private int tempLoopCounter;

        void Start()
        {
            tempUpdateTime = Time.unscaledTime;
            lastUpdateTime = tempUpdateTime + Random.value;
        }

        void Update()
        {
            if (!IsServer)
                return;

            tempUpdateTime = Time.unscaledTime;

            if (tempUpdateTime - lastUpdateTime > updateInterval)
            {
                lastUpdateTime = tempUpdateTime;
                // Request identity to rebuild subscribers
                Identity.RebuildSubscribers(false);
            }
        }

        public override bool ShouldAddSubscriber(LiteNetLibPlayer subscriber)
        {
            if (subscriber == null)
                return false;

            if (subscriber.ConnectionId == ConnectionId)
                return true;

            foreach (LiteNetLibIdentity spawnedObject in subscriber.SpawnedObjects.Values)
            {
                Vector3 pos = spawnedObject.transform.position;
                if ((pos - transform.position).magnitude < range)
                    return true;
            }
            return false;
        }

        public override bool OnRebuildSubscribers(HashSet<LiteNetLibPlayer> subscribers, bool initialize)
        {
            // find players within range
            switch (checkMethod)
            {
                case CheckMethod.Physics3D:
                    {
                        Collider[] hits = Physics.OverlapSphere(transform.position, range, layerMask.value);
                        for (tempLoopCounter = 0; tempLoopCounter < hits.Length; ++tempLoopCounter)
                        {
                            tempIdentity = hits[tempLoopCounter].GetComponent<LiteNetLibIdentity>();
                            if (tempIdentity != null && tempIdentity.Player != null)
                                subscribers.Add(tempIdentity.Player);
                        }
                        return true;
                    }

                case CheckMethod.Physics2D:
                    {
                        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, range, layerMask.value);
                        for (tempLoopCounter = 0; tempLoopCounter < hits.Length; ++tempLoopCounter)
                        {
                            tempIdentity = hits[tempLoopCounter].GetComponent<LiteNetLibIdentity>();
                            if (tempIdentity != null && tempIdentity.Player != null)
                                subscribers.Add(tempIdentity.Player);
                        }
                        return true;
                    }
            }
            return false;
        }

        public override void OnServerSubscribingAdded()
        {
            base.OnServerSubscribingAdded();
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            for (tempLoopCounter = 0; tempLoopCounter < renderers.Length; ++tempLoopCounter)
            {
                renderers[tempLoopCounter].enabled = true;
            }
        }

        public override void OnServerSubscribingRemoved()
        {
            base.OnServerSubscribingRemoved();
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            for (tempLoopCounter = 0; tempLoopCounter < renderers.Length; ++tempLoopCounter)
            {
                renderers[tempLoopCounter].enabled = false;
            }
        }
    }
}

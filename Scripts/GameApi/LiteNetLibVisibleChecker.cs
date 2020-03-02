using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

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

        private Collider[] colliders = new Collider[5000];
        private Collider2D[] colliders2D = new Collider2D[5000];
        private int colliderLength;
        private float tempUpdateTime;
        private float lastUpdateTime;
        private LiteNetLibIdentity tempIdentity;

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

            Vector3 pos;
            foreach (LiteNetLibIdentity spawnedObject in subscriber.SpawnedObjects.Values)
            {
                pos = spawnedObject.transform.position;
                if ((pos - transform.position).sqrMagnitude < range * range)
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
                        colliderLength = Physics.OverlapSphereNonAlloc(transform.position, range, colliders, layerMask.value);
                        for (int i = 0; i < colliderLength; ++i)
                        {
                            tempIdentity = colliders[i].GetComponent<LiteNetLibIdentity>();
                            if (tempIdentity != null && tempIdentity.Player != null)
                                subscribers.Add(tempIdentity.Player);
                        }
                        return true;
                    }

                case CheckMethod.Physics2D:
                    {
                        colliderLength = Physics2D.OverlapCircleNonAlloc(transform.position, range, colliders2D, layerMask.value);
                        for (int i = 0; i < colliderLength; ++i)
                        {
                            tempIdentity = colliders2D[i].GetComponent<LiteNetLibIdentity>();
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
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                return;
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            for (int i = 0; i < renderers.Length; ++i)
            {
                renderers[i].enabled = true;
            }
        }

        public override void OnServerSubscribingRemoved()
        {
            base.OnServerSubscribingRemoved();
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                return;
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            for (int i = 0; i < renderers.Length; ++i)
            {
                renderers[i].enabled = false;
            }
        }
    }
}

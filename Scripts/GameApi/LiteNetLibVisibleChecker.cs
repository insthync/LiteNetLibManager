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

        private float lastUpdateTime;

        void Start()
        {
            lastUpdateTime = Time.unscaledTime + Random.value;
        }

        void Update()
        {
            if (!IsServer)
                return;

            if (Time.unscaledTime - lastUpdateTime > updateInterval)
            {
                Identity.RebuildSubscribers(false);
                lastUpdateTime = Time.unscaledTime;
            }
        }

        public override bool ShouldAddSubscriber(LiteNetLibPlayer subscriber)
        {
            if (subscriber == null)
                return false;

            if (subscriber.ConnectId == ConnectId)
                return true;

            var spawnedObjects = subscriber.SpawnedObjects.Values;
            foreach (var spawnedObject in spawnedObjects)
            {
                var pos = spawnedObject.transform.position;
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
                        var hits = Physics.OverlapSphere(transform.position, range, layerMask.value);
                        foreach (var hit in hits)
                        {
                            var identity = hit.GetComponent<LiteNetLibIdentity>();
                            if (identity != null && identity.Player != null)
                                subscribers.Add(identity.Player);
                        }
                        return true;
                    }

                case CheckMethod.Physics2D:
                    {
                        var hits = Physics2D.OverlapCircleAll(transform.position, range, layerMask.value);
                        foreach (var hit in hits)
                        {
                            var identity = hit.GetComponent<LiteNetLibIdentity>();
                            if (identity != null && identity.Player != null)
                                subscribers.Add(identity.Player);
                        }
                        return true;
                    }
            }
            return false;
        }

        public override void OnServerSubscribingAdded()
        {
            base.OnServerSubscribingAdded();
            var renderers = GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                renderer.enabled = true;
            }
        }

        public override void OnServerSubscribingRemoved()
        {
            base.OnServerSubscribingRemoved();
            var renderers = GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                renderer.enabled = false;
            }
        }
    }
}

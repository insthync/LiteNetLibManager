using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LiteNetLibHighLevel
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

        private float lastUpdateTime;

        void Update()
        {
            if (!IsServer)
                return;

            if (Time.realtimeSinceStartup - lastUpdateTime > updateInterval)
            {
                Identity.RebuildSubscribers(false);
                lastUpdateTime = Time.realtimeSinceStartup;
            }
        }

        public override bool ShouldAddSubscriber(LiteNetLibPlayer subscriber)
        {
            var spawnedObjects = subscriber.SpawnedObjects.Values;
            foreach (var spawnedObject in spawnedObjects)
            {
                var pos = spawnedObject.transform.position;
                if ((pos - transform.position).magnitude < range)
                    return true;
            }
            return false;
        }

        public override bool OnRebuildSubscribers(HashSet<LiteNetLibPlayer> observers, bool initial)
        {
            // find players within range
            switch (checkMethod)
            {
                case CheckMethod.Physics3D:
                    {
                        var hits = Physics.OverlapSphere(transform.position, range);
                        foreach (var hit in hits)
                        {
                            var identity = hit.GetComponent<LiteNetLibIdentity>();
                            if (identity != null && identity.Player != null)
                                observers.Add(identity.Player);
                        }
                        return true;
                    }

                case CheckMethod.Physics2D:
                    {
                        var hits = Physics2D.OverlapCircleAll(transform.position, range);
                        foreach (var hit in hits)
                        {
                            var identity = hit.GetComponent<LiteNetLibIdentity>();
                            if (identity != null && identity.Player != null)
                                observers.Add(identity.Player);
                        }
                        return true;
                    }
            }
            return false;
        }
    }
}

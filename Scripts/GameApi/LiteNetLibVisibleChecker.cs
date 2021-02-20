using System.Collections.Generic;
using UnityEngine;

namespace LiteNetLibManager
{
    public class LiteNetLibVisibleChecker : BaseLiteNetLibVisibleChecker
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
        private readonly HashSet<uint> subscribings = new HashSet<uint>();
        private float updateCountDown;

        void Start()
        {
            updateCountDown = 0;
        }

        void Update()
        {
            if (!IsServer || ConnectionId < 0)
                return;

            updateCountDown -= Time.unscaledDeltaTime;

            if (updateCountDown <= 0f)
            {
                updateCountDown = updateInterval;
                FindObjectsToSubscribe();
                UpdateSubscribings(subscribings);
            }
        }

        private void FindObjectsToSubscribe()
        {
            subscribings.Clear();
            // find players within range
            switch (checkMethod)
            {
                case CheckMethod.Physics3D:
                    {
                        LiteNetLibIdentity tempIdentity;
                        int colliderLength = Physics.OverlapSphereNonAlloc(transform.position, range, colliders, layerMask.value);
                        for (int i = 0; i < colliderLength; ++i)
                        {
                            tempIdentity = colliders[i].GetComponent<LiteNetLibIdentity>();
                            if (tempIdentity != null && tempIdentity.IsSpawned)
                                subscribings.Add(tempIdentity.ObjectId);
                        }
                        return;
                    }

                case CheckMethod.Physics2D:
                    {
                        LiteNetLibIdentity tempIdentity;
                        int colliderLength = Physics2D.OverlapCircleNonAlloc(transform.position, range, colliders2D, layerMask.value);
                        for (int i = 0; i < colliderLength; ++i)
                        {
                            tempIdentity = colliders2D[i].GetComponent<LiteNetLibIdentity>();
                            if (tempIdentity != null && tempIdentity.IsSpawned)
                                subscribings.Add(tempIdentity.ObjectId);
                        }
                        return;
                    }
            }
        }

        public override bool ShouldSubscribe(LiteNetLibIdentity identity)
        {
            // Objects that have no colliders should be subscribed
            if (checkMethod == CheckMethod.Physics3D && !identity.GetComponent<Collider>())
                return true;
            if (checkMethod == CheckMethod.Physics2D && !identity.GetComponent<Collider2D>())
                return true;
            return (identity.transform.position - transform.position).sqrMagnitude < range * range;
        }

        public override bool ShouldUnsubscribe(LiteNetLibIdentity identity)
        {
            // Objects that have colliders should be unsubscribed
            if (checkMethod == CheckMethod.Physics3D && identity.GetComponent<Collider>())
                return true;
            if (checkMethod == CheckMethod.Physics2D && identity.GetComponent<Collider2D>())
                return true;
            return identity.IsDestroyed;
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LiteNetLibManager
{
    public class LiteNetLibSpawnPoint : MonoBehaviour
    {
        private Transform cacheTransform;
        public Transform CacheTransform
        {
            get
            {
                if (cacheTransform == null)
                    cacheTransform = GetComponent<Transform>();
                return cacheTransform;
            }
        }

        public Vector3 Position
        {
            get { return CacheTransform.position; }
        }
    }
}

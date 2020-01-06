using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LiteNetLibManager
{
    public class LiteNetLibSpawnPoint : MonoBehaviour
    {
        public Transform CacheTransform { get; private set; }

        public Vector3 Position
        {
            get { return CacheTransform.position; }
        }

        private void Awake()
        {
            CacheTransform = transform;
        }
    }
}

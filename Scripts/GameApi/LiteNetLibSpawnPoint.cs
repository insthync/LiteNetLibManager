using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LiteNetLibManager
{
    public class LiteNetLibSpawnPoint : MonoBehaviour
    {
        [SerializeField]
        private float radius;

        public Transform CacheTransform { get; private set; }

        public Vector3 Position
        {
            get { return CacheTransform.position; }
        }

        private void Awake()
        {
            CacheTransform = transform;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Color color = Color.green;
            Gizmos.color = color;
            Gizmos.DrawWireSphere(transform.position, radius);
            color.a = 0.5f;
            Gizmos.color = color;
            Gizmos.DrawSphere(transform.position, radius);
        }
#endif

        public Vector3 GetRandomPosition()
        {
            Vector3 offsets = Random.insideUnitSphere * radius;
            offsets.y = 0;
            return Position + offsets;
        }
    }
}

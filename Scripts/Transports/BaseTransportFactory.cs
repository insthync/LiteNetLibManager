using UnityEngine;

namespace LiteNetLibManager
{
    public abstract class BaseTransportFactory : MonoBehaviour
    {
        public abstract ITransport Build();
    }
}

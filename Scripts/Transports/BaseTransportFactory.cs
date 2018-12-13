using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LiteNetLibManager
{
    public abstract class BaseTransportFactory : MonoBehaviour
    {
        public abstract ITransport Build();
    }
}

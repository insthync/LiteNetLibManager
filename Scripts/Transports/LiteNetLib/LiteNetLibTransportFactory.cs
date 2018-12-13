using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LiteNetLibManager
{
    public class LiteNetLibTransportFactory : BaseTransportFactory
    {
        public override ITransport Build()
        {
            return new LiteNetLibTransport();
        }
    }
}

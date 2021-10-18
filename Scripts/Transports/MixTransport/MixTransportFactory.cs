﻿using UnityEngine;

namespace LiteNetLibManager
{
    public class MixTransportFactory : BaseTransportFactory
    {
        public override bool CanUseWithWebGL { get { return true; } }
        public string connectKey = "SampleConnectKey";
        public int webSocketPortOffset = 100;
        public bool webSocketSecure = false;
        public string webSocketCertificateFilePath = string.Empty;
        public string webSocketCertificatePassword = string.Empty;
        [Range(1, 64)]
        public byte clientDataChannelsCount = 16;
        [Range(1, 64)]
        public byte serverDataChannelsCount = 16;

        public override ITransport Build()
        {
            return new MixTransport(connectKey, webSocketPortOffset, webSocketSecure, webSocketCertificateFilePath, webSocketCertificatePassword, clientDataChannelsCount, serverDataChannelsCount);
        }
    }
}

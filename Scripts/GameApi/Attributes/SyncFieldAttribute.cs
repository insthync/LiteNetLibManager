using LiteNetLib;
using System;

namespace LiteNetLibManager
{
    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public class SyncFieldAttribute : Attribute
    {
        /// <summary>
        /// Sending data channel from server to clients
        /// </summary>
        public byte dataChannel = 0;
        /// <summary>
        /// Sending method type from server to clients
        /// </summary>
        public DeliveryMethod deliveryMethod = DeliveryMethod.ReliableOrdered;
        /// <summary>
        /// Sending data channel from clients to server (`syncMode` is `ClientMulticast` only)
        /// </summary>
        public byte clientDataChannel = 0;
        /// <summary>
        /// Sending method type from clients to server (`syncMode` is `ClientMulticast` only)
        /// </summary>
        public DeliveryMethod clientDeliveryMethod = DeliveryMethod.ReliableOrdered;
        /// <summary>
        /// Interval to send network data (0.01f to 2f)
        /// </summary>
        public float sendInterval = 0.1f;
        /// <summary>
        /// If this is `TRUE` it will syncing although no changes
        /// </summary>
        public bool alwaysSync = false;
        /// <summary>
        /// If this is `TRUE` it will not sync initial data immdediately with spawn message (it will sync later)
        /// </summary>
        public bool doNotSyncInitialDataImmediately = false;
        /// <summary>
        /// How data changes handle and sync
        /// </summary>
        public LiteNetLibSyncField.SyncMode syncMode = LiteNetLibSyncField.SyncMode.ServerToClients;
        /// <summary>
        /// Method name which will be invoked when data changed
        /// </summary>
        public string onChangeMethodName = string.Empty;
        /// <summary>
        /// Method name which will be invoked when data sent
        /// </summary>
        public string onUpdateMethodName = string.Empty;
    }
}

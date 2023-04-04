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
        /// How it will sync data. If always sync, it will sync data although it has no changes. If sync initial data immediately, it will sync when spawn networked object, If sync only initial data it will sync only when spawn networked object
        /// </summary>
        public LiteNetLibSyncField.SyncBehaviour syncBehaviour = LiteNetLibSyncField.SyncBehaviour.Default;
        /// <summary>
        /// Who can sync data and sync to whom
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

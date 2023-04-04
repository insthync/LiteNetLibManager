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
        /// Sending data channel from clients to server (while `syncMode` is `ClientMulticast` only)
        /// </summary>
        public byte clientDataChannel = 0;
        /// <summary>
        /// Sending method type from clients to server (while `syncMode` is `ClientMulticast` only)
        /// </summary>
        public DeliveryMethod clientDeliveryMethod = DeliveryMethod.ReliableOrdered;
        /// <summary>
        /// Interval to send network data
        /// </summary>
        public float sendInterval = 0.1f;
        /// <summary>
        /// How it will sync data. If always sync, it will sync data although it has no changes. If don't sync initial data immediately, it will not sync initial data immdediately with networked object spawning message, it will sync later after spanwed. If don't sync update, it will not sync when data has changes.
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

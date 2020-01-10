using LiteNetLib;
using System;

namespace LiteNetLibManager
{
    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public class SyncFieldAttribute : Attribute
    {
        /// <summary>
        /// Sending method type
        /// </summary>
        public DeliveryMethod deliveryMethod = DeliveryMethod.ReliableOrdered;
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
        /// Function name which will be invoked when data changed
        /// </summary>
        public string hook = string.Empty;
    }
}

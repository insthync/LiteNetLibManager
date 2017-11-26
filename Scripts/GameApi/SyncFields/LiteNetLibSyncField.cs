using UnityEngine;
using LiteNetLib;
using LiteNetLib.Utils;

namespace LiteNetLibHighLevel
{
    public abstract class LiteNetLibSyncField : LiteNetLibElement
    {
        public SendOptions sendOptions;
        public bool forOwnerOnly;

        public abstract void SendUpdate();
        public abstract void SendUpdate(NetPeer peer);
        public abstract void DeserializeValue(NetDataReader reader);
        public abstract void SerializeValue(NetDataWriter writer);
    }

    public abstract class LiteNetLibSyncField<TField, TFieldType> : LiteNetLibSyncField
        where TField : LiteNetLibNetField<TFieldType>, new()
    {
        protected TField field;
        public TField Field
        {
            get
            {
                if (field == null)
                    field = new TField();
                return field;
            }
        }

        [ReadOnly, SerializeField]
        protected TFieldType value;
        public TFieldType Value
        {
            get { return value; }
            set
            {
                if (Behaviour == null)
                {
                    Debug.LogError("Sync field error while set value, behaviour is empty");
                    return;
                }
                if (!Behaviour.IsServer)
                {
                    Debug.LogError("Sync field error while set value, not the server");
                    return;
                }
                if (IsValueChanged(value))
                {
                    this.value = value;
                    SendUpdate();
                }
            }
        }

        public static implicit operator TFieldType(LiteNetLibSyncField<TField, TFieldType> field)
        {
            return field.Value;
        }

        public override sealed void SendUpdate()
        {
            var manager = Manager;
            if (!manager.IsServer)
                return;

            var peers = manager.Peers;
            if (forOwnerOnly)
            {
                var connectId = Behaviour.ConnectId;
                if (peers.ContainsKey(connectId))
                    SendUpdate(peers[connectId]);
            }
            else
            {
                foreach (var peer in peers.Values)
                {
                    SendUpdate(peer);
                }
            }
        }

        public override sealed void SendUpdate(NetPeer peer)
        {
            var manager = Manager;
            if (!manager.IsServer)
                return;

            manager.SendPacket(sendOptions, peer, LiteNetLibGameManager.GameMsgTypes.ServerUpdateSyncField, SerializeForSend);
        }

        protected void SerializeForSend(NetDataWriter writer)
        {
            var syncFieldInfo = GetInfo();
            writer.Put(syncFieldInfo.objectId);
            writer.Put(syncFieldInfo.behaviourIndex);
            writer.Put(syncFieldInfo.elementId);
            SerializeValue(writer);
        }

        public override sealed void DeserializeValue(NetDataReader reader)
        {
            Field.Deserialize(reader);
            value = Field.Value;
        }

        public override sealed void SerializeValue(NetDataWriter writer)
        {
            Field.Value = value;
            Field.Serialize(writer);
        }

        public abstract bool IsValueChanged(TFieldType newValue);
    }
}

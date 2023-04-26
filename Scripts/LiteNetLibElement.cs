using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public struct LiteNetLibElementInfo
    {
        public uint objectId;
        public int elementId;
        public LiteNetLibElementInfo(uint objectId, int elementId)
        {
            this.objectId = objectId;
            this.elementId = elementId;
        }

        public static void SerializeInfo(LiteNetLibElementInfo info, NetDataWriter writer)
        {
            writer.PutPackedUInt(info.objectId);
            writer.PutPackedUInt((uint)info.elementId);
        }

        public static LiteNetLibElementInfo DeserializeInfo(NetDataReader reader)
        {
            return new LiteNetLibElementInfo(reader.GetPackedUInt(), (int)reader.GetPackedUInt());
        }
    }

    public abstract class LiteNetLibElement
    {
        [ReadOnly, SerializeField]
        protected LiteNetLibBehaviour behaviour;
        public LiteNetLibBehaviour Behaviour
        {
            get { return behaviour; }
        }

        public LiteNetLibIdentity Identity
        {
            get { return Behaviour.Identity; }
        }

        public long ConnectionId
        {
            get { return !IsSetup ? -1 : Behaviour.ConnectionId; }
        }

        public uint ObjectId
        {
            get { return !IsSetup ? 0 : Behaviour.ObjectId; }
        }

        public LiteNetLibGameManager Manager
        {
            get { return Behaviour.Manager; }
        }

        public virtual string LogTag
        {
            get { return (!IsSetup ? "(No Behaviour)" : Behaviour.LogTag) + "->" + ToString(); }
        }

        public bool IsServer
        {
            get { return IsSetup && Behaviour.IsServer; }
        }

        public bool IsClient
        {
            get { return IsSetup && Behaviour.IsClient; }
        }

        public bool IsOwnerClient
        {
            get { return IsSetup && Behaviour.IsOwnerClient; }
        }

        public bool IsSetup { get; private set; }

        [ReadOnly, SerializeField]
        protected int elementId;
        public int ElementId
        {
            get { return elementId; }
        }

        public LiteNetLibElementInfo GetInfo()
        {
            return new LiteNetLibElementInfo(Behaviour.ObjectId, ElementId);
        }

        internal virtual void Setup(LiteNetLibBehaviour behaviour, int elementId)
        {
            this.behaviour = behaviour;
            this.elementId = elementId;
            IsSetup = true;
        }

        protected virtual bool CanSync()
        {
            return IsSetup;
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LiteNetLib.Utils;

namespace LiteNetLibHighLevel
{
    public struct LiteNetLibElementInfo
    {
        public uint objectId;
        public int behaviourIndex;
        public ushort elementId;
        public LiteNetLibElementInfo(uint objectId, int behaviourIndex, ushort elementId)
        {
            this.objectId = objectId;
            this.behaviourIndex = behaviourIndex;
            this.elementId = elementId;
        }

        public static void SerializeInfo(LiteNetLibElementInfo info, NetDataWriter writer)
        {
            writer.Put(info.objectId);
            writer.Put(info.behaviourIndex);
            writer.Put(info.elementId);
        }

        public static LiteNetLibElementInfo DeserializeInfo(NetDataReader reader)
        {
            return new LiteNetLibElementInfo(reader.GetUInt(), reader.GetInt(), reader.GetUShort());
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

        [ReadOnly, SerializeField]
        protected ushort elementId;
        public ushort ElementId
        {
            get { return elementId; }
        }

        public LiteNetLibGameManager Manager
        {
            get { return behaviour.Manager; }
        }

        public LiteNetLibElementInfo GetInfo()
        {
            return new LiteNetLibElementInfo(Behaviour.ObjectId, Behaviour.BehaviourIndex, ElementId);
        }

        internal virtual void Setup(LiteNetLibBehaviour behaviour, ushort elementId)
        {
            this.behaviour = behaviour;
            this.elementId = elementId;
        }

        protected virtual bool ValidateBeforeAccess()
        {
            if (Behaviour == null)
            {
                Debug.LogError("[LiteNetLibElement] Error while set value, behaviour is empty");
                return false;
            }
            return true;
        }

        protected bool IsSubscribedOrOwning(long connectId)
        {
            return Behaviour.Identity.Subscribers.ContainsKey(connectId) || connectId == Behaviour.ConnectId;
        }
    }
}

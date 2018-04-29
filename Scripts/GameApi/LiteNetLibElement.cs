using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LiteNetLib.Utils;

namespace LiteNetLibHighLevel
{
    public struct LiteNetLibElementInfo
    {
        public uint objectId;
        public byte behaviourIndex;
        public byte elementId;
        public LiteNetLibElementInfo(uint objectId, byte behaviourIndex, byte elementId)
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
            return new LiteNetLibElementInfo(reader.GetUInt(), reader.GetByte(), reader.GetByte());
        }
    }

    public abstract class LiteNetLibElement
    {
        [LiteNetLibReadOnlyAttribute, SerializeField]
        protected LiteNetLibBehaviour behaviour;
        public LiteNetLibBehaviour Behaviour
        {
            get { return behaviour; }
        }

        [LiteNetLibReadOnlyAttribute, SerializeField]
        protected byte elementId;
        public byte ElementId
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

        internal virtual void Setup(LiteNetLibBehaviour behaviour, byte elementId)
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
    }
}

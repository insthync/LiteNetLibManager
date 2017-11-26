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

        public virtual void Setup(LiteNetLibBehaviour behaviour, ushort elementId)
        {
            this.behaviour = behaviour;
            this.elementId = elementId;
        }
    }
}

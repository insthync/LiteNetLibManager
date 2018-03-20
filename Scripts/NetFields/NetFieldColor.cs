using LiteNetLib.Utils;
using UnityEngine;

namespace LiteNetLibHighLevel
{
    public class NetFieldColor : LiteNetLibNetField<Color>
    {
        public override void Deserialize(NetDataReader reader)
        {
            var r = reader.GetShort() * 0.01f;
            var g = reader.GetShort() * 0.01f;
            var b = reader.GetShort() * 0.01f;
            var a = reader.GetShort() * 0.01f;
            Value = new Color(r, g, b, a);
        }

        public override void Serialize(NetDataWriter writer)
        {
            var r = (short)(Value.r * 100f);
            var g = (short)(Value.g * 100f);
            var b = (short)(Value.b * 100f);
            var a = (short)(Value.a * 100f);
            writer.Put(r);
            writer.Put(g);
            writer.Put(b);
            writer.Put(a);
        }

        public override bool IsValueChanged(Color newValue)
        {
            return !newValue.Equals(Value);
        }
    }
}

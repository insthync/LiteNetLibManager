using LiteNetLib.Utils;

namespace LiteNetLibHighLevel
{
    public abstract class LiteNetLibNetField
    {
        public abstract void SetValue(object value);
        public abstract void Deserialize(NetDataReader reader);
        public abstract void Serialize(NetDataWriter writer);
    }

    public abstract class LiteNetLibNetField<T> : LiteNetLibNetField
    {
        public T Value { get; set; }
        public static implicit operator T(LiteNetLibNetField<T> parameter)
        {
            return parameter.Value;
        }

        public override void SetValue(object value)
        {
            Value = (T)value;
        }

        public abstract bool IsValueChanged(T newValue);
    }
}

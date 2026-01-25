using System;

namespace LiteNetLibManager.Serialization
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class WriterRegisterAttribute : Attribute
    {
        public Type Type { get; private set; }
        public WriterRegisterAttribute(Type type)
        {
            Type = type;
        }
    }
}

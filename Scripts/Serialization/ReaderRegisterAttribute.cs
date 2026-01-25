using System;

namespace LiteNetLibManager.Serialization
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class ReaderRegisterAttribute : Attribute
    {
        public Type Type { get; private set; }
        public ReaderRegisterAttribute(Type type)
        {
            Type = type;
        }
    }
}

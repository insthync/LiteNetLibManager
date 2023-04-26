using System;

namespace LiteNetLibManager
{
    [AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public abstract class RpcAttribute : Attribute
    {
        /// <summary>
        /// If is true non-owner client can call the object's net function
        /// </summary>
        public bool canCallByEveryone = false;
    }
}

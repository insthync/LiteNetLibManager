using System;
using System.Reflection;
using LiteNetLib;
using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public enum RPCReceivers : byte
    {
        Target,
        All,
        Server,
    }

    public class LiteNetLibRPC : LiteNetLibElement
    {
        protected readonly static NetDataWriter s_ServerWriter = new NetDataWriter();
        protected readonly static NetDataWriter s_ClientWriter = new NetDataWriter();

        public readonly Type[] ParameterTypes;
        public readonly object[] Parameters;
        public bool CanCallByEveryone { get; set; }

        private RPCDelegate _callback;

        protected LiteNetLibRPC() : this(0)
        {
        }

        protected LiteNetLibRPC(int parameterCount)
        {
            Parameters = new object[parameterCount];
            ParameterTypes = new Type[parameterCount];
        }

        protected LiteNetLibRPC(Type[] parameterTypes)
        {
            Parameters = new object[parameterTypes.Length];
            ParameterTypes = parameterTypes;
        }

        public LiteNetLibRPC(RPCDelegate callback) : this()
        {
            _callback = callback;
        }

        protected bool CanBeCalled()
        {
            return Behaviour != null && (IsServer || IsOwnerClient || CanCallByEveryone);
        }

        internal virtual void HookCallback()
        {
            _callback.Invoke();
        }

        protected void SendCall(byte dataChannel, DeliveryMethod deliveryMethod, RPCReceivers receivers, long targetConnectionId)
        {
            LiteNetLibGameManager manager = Manager;
            LiteNetLibServer server = manager.Server;
            LiteNetLibClient client = manager.Client;
            if (manager.IsServer)
            {
                switch (receivers)
                {
                    case RPCReceivers.Target:
                        if (Identity.HasSubscriberOrIsOwning(targetConnectionId) && manager.ContainsConnectionId(targetConnectionId))
                        {
                            // Prepare packet
                            TransportHandler.WritePacket(s_ServerWriter, GameMsgTypes.RPC);
                            SerializeForSend(s_ServerWriter);
                            // Send function call message from server to target client by target connection Id
                            server.SendMessage(targetConnectionId, dataChannel, deliveryMethod, s_ServerWriter);
                        }
                        break;
                    case RPCReceivers.All:
                        // Send to all connections
                        foreach (long connectionId in manager.GetConnectionIds())
                        {
                            if (manager.ClientConnectionId == connectionId)
                            {
                                // This is host's networking oject, so hook callback immediately
                                // Don't have to send message to the client, because it is currently run as both server and client
                                HookCallback();
                            }
                            else if (Identity.HasSubscriberOrIsOwning(connectionId))
                            {
                                // Prepare packet
                                TransportHandler.WritePacket(s_ServerWriter, GameMsgTypes.RPC);
                                SerializeForSend(s_ServerWriter);
                                // Send message to subscribing clients
                                server.SendMessage(connectionId, dataChannel, deliveryMethod, s_ServerWriter);
                            }
                        }
                        if (!manager.IsClientConnected)
                        {
                            // It's not a host(client+host), it's just a server
                            // So hook callback immediately to do the function at server
                            HookCallback();
                        }
                        break;
                    case RPCReceivers.Server:
                        // Call server function at server
                        // So hook callback immediately to do the function at server
                        HookCallback();
                        break;
                }
            }
            else if (manager.IsClientConnected)
            {
                // Prepare packet
                TransportHandler.WritePacket(s_ClientWriter, GameMsgTypes.RPC);
                SerializeForClientSend(s_ClientWriter, receivers, targetConnectionId);
                // Client send net function call to server
                // Then the server will hook callback or forward message to other clients
                client.SendMessage(dataChannel, deliveryMethod, s_ClientWriter);
            }
        }

        internal void SetParameters(params object[] parameterValues)
        {
            for (int i = 0; i < Parameters.Length; ++i)
            {
                if (i >= parameterValues.Length)
                    break;
                Parameters[i] = parameterValues[i];
            }
        }

        internal void Call(byte dataChannel, DeliveryMethod deliveryMethod, RPCReceivers receivers, params object[] parameterValues)
        {
            if (!CanBeCalled())
                return;

            SetParameters(parameterValues);
            SendCall(dataChannel, deliveryMethod, receivers, ConnectionId);
        }

        internal void Call(byte dataChannel, DeliveryMethod deliveryMethod, long connectionId, params object[] parameterValues)
        {
            if (!CanBeCalled())
                return;

            SetParameters(parameterValues);
            SendCall(dataChannel, deliveryMethod, RPCReceivers.Target, connectionId);
        }

        internal void CallWithoutParametersSet(RPCReceivers receivers)
        {
            if (!CanBeCalled())
                return;

            SendCall(0, DeliveryMethod.ReliableOrdered, receivers, ConnectionId);
        }

        internal void CallWithoutParametersSet(long connectionId)
        {
            if (!CanBeCalled())
                return;

            SendCall(0, DeliveryMethod.ReliableOrdered, RPCReceivers.Target, connectionId);
        }

        private void SerializeForClientSend(NetDataWriter writer, RPCReceivers receivers, long connectionId)
        {
            writer.Put((byte)receivers);
            if (receivers == RPCReceivers.Target)
                writer.PutPackedLong(connectionId);
            SerializeForSend(writer);
        }

        private void SerializeForSend(NetDataWriter writer)
        {
            LiteNetLibElementInfo.SerializeInfo(GetInfo(), writer);
            SerializeParameters(writer);
        }

        public void DeserializeParameters(NetDataReader reader)
        {
            if (Parameters == null || Parameters.Length == 0)
                return;
            for (int i = 0; i < Parameters.Length; ++i)
            {
                Type type = ParameterTypes[i];
                if (type.IsArray)
                    Parameters[i] = reader.GetArrayObject(type.GetElementType());
                else
                    Parameters[i] = reader.GetValue(type);
            }
        }

        public void SerializeParameters(NetDataWriter writer)
        {
            if (Parameters == null || Parameters.Length == 0)
                return;
            for (int i = 0; i < Parameters.Length; ++i)
            {
                Type type = ParameterTypes[i];
                if (type.IsArray)
                    writer.PutArrayObject(type.GetElementType(), Parameters[i]);
                else
                    writer.PutValue(type, Parameters[i]);
            }
        }
    }

    #region Implement for multiple parameter usages
    public class LiteNetLibRPCDynamic : LiteNetLibRPC
    {
        /// <summary>
        /// The class which contain the function
        /// </summary>
        private object _instance;

        private MethodInfo _callback;

        public LiteNetLibRPCDynamic(Type[] parameterTypes, object instance, MethodInfo callback) : base(parameterTypes)
        {
            _instance = instance;
            _callback = callback;
        }

        internal override sealed void HookCallback()
        {
            _callback.Invoke(_instance, Parameters);
        }
    }

    public class LiteNetLibRPC<T1> : LiteNetLibRPC
    {
        private RPCDelegate<T1> _callback;

        protected LiteNetLibRPC() : base(1)
        {
            ParameterTypes[0] = typeof(T1);
        }

        public LiteNetLibRPC(RPCDelegate<T1> callback) : this()
        {
            _callback = callback;
        }

        internal override sealed void HookCallback()
        {
            _callback((T1)Parameters[0]);
        }
    }

    public class LiteNetLibRPC<T1, T2> : LiteNetLibRPC
    {
        private RPCDelegate<T1, T2> _callback;

        protected LiteNetLibRPC() : base(2)
        {
            ParameterTypes[0] = typeof(T1);
            ParameterTypes[1] = typeof(T2);
        }

        public LiteNetLibRPC(RPCDelegate<T1, T2> callback) : this()
        {
            _callback = callback;
        }

        internal override sealed void HookCallback()
        {
            _callback((T1)Parameters[0], (T2)Parameters[1]);
        }
    }

    public class LiteNetLibRPC<T1, T2, T3> : LiteNetLibRPC
    {
        private RPCDelegate<T1, T2, T3> _callback;

        protected LiteNetLibRPC() : base(3)
        {
            ParameterTypes[0] = typeof(T1);
            ParameterTypes[1] = typeof(T2);
            ParameterTypes[2] = typeof(T3);
        }

        public LiteNetLibRPC(RPCDelegate<T1, T2, T3> callback) : this()
        {
            _callback = callback;
        }

        internal override sealed void HookCallback()
        {
            _callback((T1)Parameters[0], (T2)Parameters[1], (T3)Parameters[2]);
        }
    }

    public class LiteNetLibRPC<T1, T2, T3, T4> : LiteNetLibRPC
    {
        private RPCDelegate<T1, T2, T3, T4> _callback;

        protected LiteNetLibRPC() : base(4)
        {
            ParameterTypes[0] = typeof(T1);
            ParameterTypes[1] = typeof(T2);
            ParameterTypes[2] = typeof(T3);
            ParameterTypes[3] = typeof(T4);
        }

        public LiteNetLibRPC(RPCDelegate<T1, T2, T3, T4> callback) : this()
        {
            _callback = callback;
        }

        internal override sealed void HookCallback()
        {
            _callback((T1)Parameters[0], (T2)Parameters[1], (T3)Parameters[2], (T4)Parameters[3]);
        }
    }

    public class LiteNetLibRPC<T1, T2, T3, T4, T5> : LiteNetLibRPC
    {
        private RPCDelegate<T1, T2, T3, T4, T5> _callback;

        protected LiteNetLibRPC() : base(5)
        {
            ParameterTypes[0] = typeof(T1);
            ParameterTypes[1] = typeof(T2);
            ParameterTypes[2] = typeof(T3);
            ParameterTypes[3] = typeof(T4);
            ParameterTypes[4] = typeof(T5);
        }

        public LiteNetLibRPC(RPCDelegate<T1, T2, T3, T4, T5> callback) : this()
        {
            _callback = callback;
        }

        internal override sealed void HookCallback()
        {
            _callback((T1)Parameters[0], (T2)Parameters[1], (T3)Parameters[2], (T4)Parameters[3], (T5)Parameters[4]);
        }
    }

    public class LiteNetLibRPC<T1, T2, T3, T4, T5, T6> : LiteNetLibRPC
    {
        private RPCDelegate<T1, T2, T3, T4, T5, T6> _callback;

        protected LiteNetLibRPC() : base(6)
        {
            ParameterTypes[0] = typeof(T1);
            ParameterTypes[1] = typeof(T2);
            ParameterTypes[2] = typeof(T3);
            ParameterTypes[3] = typeof(T4);
            ParameterTypes[4] = typeof(T5);
            ParameterTypes[5] = typeof(T6);
        }

        public LiteNetLibRPC(RPCDelegate<T1, T2, T3, T4, T5, T6> callback) : this()
        {
            _callback = callback;
        }

        internal override sealed void HookCallback()
        {
            _callback((T1)Parameters[0], (T2)Parameters[1], (T3)Parameters[2], (T4)Parameters[3], (T5)Parameters[4], (T6)Parameters[5]);
        }
    }

    public class LiteNetLibRPC<T1, T2, T3, T4, T5, T6, T7> : LiteNetLibRPC
    {
        private RPCDelegate<T1, T2, T3, T4, T5, T6, T7> _callback;

        protected LiteNetLibRPC() : base(7)
        {
            ParameterTypes[0] = typeof(T1);
            ParameterTypes[1] = typeof(T2);
            ParameterTypes[2] = typeof(T3);
            ParameterTypes[3] = typeof(T4);
            ParameterTypes[4] = typeof(T5);
            ParameterTypes[5] = typeof(T6);
            ParameterTypes[6] = typeof(T7);
        }

        public LiteNetLibRPC(RPCDelegate<T1, T2, T3, T4, T5, T6, T7> callback) : this()
        {
            _callback = callback;
        }

        internal override sealed void HookCallback()
        {
            _callback((T1)Parameters[0], (T2)Parameters[1], (T3)Parameters[2], (T4)Parameters[3], (T5)Parameters[4], (T6)Parameters[5], (T7)Parameters[6]);
        }
    }

    public class LiteNetLibRPC<T1, T2, T3, T4, T5, T6, T7, T8> : LiteNetLibRPC
    {
        private RPCDelegate<T1, T2, T3, T4, T5, T6, T7, T8> _callback;

        protected LiteNetLibRPC() : base(8)
        {
            ParameterTypes[0] = typeof(T1);
            ParameterTypes[1] = typeof(T2);
            ParameterTypes[2] = typeof(T3);
            ParameterTypes[3] = typeof(T4);
            ParameterTypes[4] = typeof(T5);
            ParameterTypes[5] = typeof(T6);
            ParameterTypes[6] = typeof(T7);
            ParameterTypes[7] = typeof(T8);
        }

        public LiteNetLibRPC(RPCDelegate<T1, T2, T3, T4, T5, T6, T7, T8> callback) : this()
        {
            _callback = callback;
        }

        internal override sealed void HookCallback()
        {
            _callback((T1)Parameters[0], (T2)Parameters[1], (T3)Parameters[2], (T4)Parameters[3], (T5)Parameters[4], (T6)Parameters[5], (T7)Parameters[6], (T8)Parameters[7]);
        }
    }

    public class LiteNetLibRPC<T1, T2, T3, T4, T5, T6, T7, T8, T9> : LiteNetLibRPC
    {
        private RPCDelegate<T1, T2, T3, T4, T5, T6, T7, T8, T9> _callback;

        protected LiteNetLibRPC() : base(9)
        {
            ParameterTypes[0] = typeof(T1);
            ParameterTypes[1] = typeof(T2);
            ParameterTypes[2] = typeof(T3);
            ParameterTypes[3] = typeof(T4);
            ParameterTypes[4] = typeof(T5);
            ParameterTypes[5] = typeof(T6);
            ParameterTypes[6] = typeof(T7);
            ParameterTypes[7] = typeof(T8);
            ParameterTypes[8] = typeof(T9);
        }

        public LiteNetLibRPC(RPCDelegate<T1, T2, T3, T4, T5, T6, T7, T8, T9> callback) : this()
        {
            _callback = callback;
        }

        internal override sealed void HookCallback()
        {
            _callback((T1)Parameters[0], (T2)Parameters[1], (T3)Parameters[2], (T4)Parameters[3], (T5)Parameters[4], (T6)Parameters[5], (T7)Parameters[6], (T8)Parameters[7], (T9)Parameters[8]);
        }
    }

    public class LiteNetLibRPC<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> : LiteNetLibRPC
    {
        private RPCDelegate<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> _callback;

        protected LiteNetLibRPC() : base(10)
        {
            ParameterTypes[0] = typeof(T1);
            ParameterTypes[1] = typeof(T2);
            ParameterTypes[2] = typeof(T3);
            ParameterTypes[3] = typeof(T4);
            ParameterTypes[4] = typeof(T5);
            ParameterTypes[5] = typeof(T6);
            ParameterTypes[6] = typeof(T7);
            ParameterTypes[7] = typeof(T8);
            ParameterTypes[8] = typeof(T9);
            ParameterTypes[9] = typeof(T10);
        }

        public LiteNetLibRPC(RPCDelegate<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> callback) : this()
        {
            _callback = callback;
        }

        internal override sealed void HookCallback()
        {
            _callback((T1)Parameters[0], (T2)Parameters[1], (T3)Parameters[2], (T4)Parameters[3], (T5)Parameters[4], (T6)Parameters[5], (T7)Parameters[6], (T8)Parameters[7], (T9)Parameters[8], (T10)Parameters[9]);
        }
    }
    #endregion
}

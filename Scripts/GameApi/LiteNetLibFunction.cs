using System;
using LiteNetLib;
using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public enum FunctionReceivers : byte
    {
        Target,
        All,
        Server,
    }

    public class LiteNetLibFunction : LiteNetLibElement
    {
        private NetFunctionDelegate callback;
        
        public Type[] ParameterTypes { get; protected set; }
        public object[] Parameters { get; protected set; }

        public LiteNetLibFunction()
        {
        }

        public LiteNetLibFunction(NetFunctionDelegate callback)
        {
            this.callback = callback;
        }

        public virtual void HookCallback()
        {
            callback();
        }

        protected void ServerSendCall(long connectionId, FunctionReceivers receivers, long targetConnectionId)
        {
            Manager.ServerSendPacket(connectionId, DeliveryMethod.ReliableOrdered, LiteNetLibGameManager.GameMsgTypes.ServerCallFunction, (writer) => SerializeForSend(writer));
        }

        protected void ClientSendCall(FunctionReceivers receivers, long targetConnectionId)
        {
            Manager.ClientSendPacket(DeliveryMethod.ReliableOrdered, LiteNetLibGameManager.GameMsgTypes.ClientCallFunction, (writer) => SerializeForClient(writer, receivers, targetConnectionId));
        }

        protected void SendCall(FunctionReceivers receivers, long targetConnectionId)
        {
            LiteNetLibGameManager manager = Manager;

            if (manager.IsServer)
            {
                switch (receivers)
                {
                    case FunctionReceivers.Target:
                        if (Behaviour.Identity.IsSubscribedOrOwning(targetConnectionId) && manager.ContainsConnectionId(targetConnectionId))
                            ServerSendCall(targetConnectionId, receivers, targetConnectionId);
                        break;
                    case FunctionReceivers.All:
                        foreach (long connectionId in manager.GetConnectionIds())
                        {
                            if (Behaviour.Identity.IsSubscribedOrOwning(connectionId))
                                ServerSendCall(connectionId, receivers, targetConnectionId);
                        }
                        if (!Manager.IsClientConnected)
                            HookCallback();
                        break;
                    case FunctionReceivers.Server:
                        HookCallback();
                        break;
                }
            }
            else if (manager.IsClientConnected)
                ClientSendCall(receivers, targetConnectionId);
        }

        public void SetParameters(params object[] parameterValues)
        {
            Parameters = parameterValues;
        }

        public void Call(FunctionReceivers receivers, params object[] parameterValues)
        {
            if (!ValidateBeforeAccess())
                return;

            SetParameters(parameterValues);
            SendCall(receivers, Behaviour.ConnectionId);
        }

        public void Call(long connectionId, params object[] parameterValues)
        {
            if (!ValidateBeforeAccess())
                return;

            SetParameters(parameterValues);
            SendCall(FunctionReceivers.Target, connectionId);
        }
        
        protected void SerializeForClient(NetDataWriter writer, FunctionReceivers receivers, long connectionId)
        {
            writer.Put((byte)receivers);
            if (receivers == FunctionReceivers.Target)
                writer.PutPackedULong((ulong)connectionId);
            SerializeForSend(writer);
        }

        protected void SerializeForSend(NetDataWriter writer)
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
                Parameters[i] = reader.GetValue(ParameterTypes[i]);
            }
        }

        public void SerializeParameters(NetDataWriter writer)
        {
            if (Parameters == null || Parameters.Length == 0)
                return;
            for (int i = 0; i < Parameters.Length; ++i)
            {
                writer.PutValue(ParameterTypes[i], Parameters[i]);
            }
        }
    }

    #region Implement for multiple parameter usages
    public class LiteNetLibFunction<T1> : LiteNetLibFunction
    {
        private NetFunctionDelegate<T1> callback;

        public LiteNetLibFunction()
        {
            Parameters = new object[1];
            ParameterTypes = new Type[1];
            ParameterTypes[0] = typeof(T1);
        }

        public LiteNetLibFunction(NetFunctionDelegate<T1> callback) : this()
        {
            this.callback = callback;
        }

        public override void HookCallback()
        {
            callback((T1)Parameters[0]);
        }
    }

    public class LiteNetLibFunction<T1, T2> : LiteNetLibFunction
    {
        private NetFunctionDelegate<T1, T2> callback;

        public LiteNetLibFunction()
        {
            Parameters = new object[2];
            ParameterTypes = new Type[2];
            ParameterTypes[0] = typeof(T1);
            ParameterTypes[1] = typeof(T2);
        }

        public LiteNetLibFunction(NetFunctionDelegate<T1, T2> callback) : this()
        {
            this.callback = callback;
        }

        public override void HookCallback()
        {
            callback((T1)Parameters[0], (T2)Parameters[1]);
        }
    }
    
    public class LiteNetLibFunction<T1, T2, T3> : LiteNetLibFunction
    {
        private NetFunctionDelegate<T1, T2, T3> callback;

        public LiteNetLibFunction()
        {
            Parameters = new object[3];
            ParameterTypes = new Type[3];
            ParameterTypes[0] = typeof(T1);
            ParameterTypes[1] = typeof(T2);
            ParameterTypes[2] = typeof(T3);
        }

        public LiteNetLibFunction(NetFunctionDelegate<T1, T2, T3> callback) : this()
        {
            this.callback = callback;
        }

        public override void HookCallback()
        {
            callback((T1)Parameters[0], (T2)Parameters[1], (T3)Parameters[2]);
        }
    }

    public class LiteNetLibFunction<T1, T2, T3, T4> : LiteNetLibFunction
    {
        private NetFunctionDelegate<T1, T2, T3, T4> callback;

        public LiteNetLibFunction()
        {
            Parameters = new object[4];
            ParameterTypes = new Type[4];
            ParameterTypes[0] = typeof(T1);
            ParameterTypes[1] = typeof(T2);
            ParameterTypes[2] = typeof(T3);
            ParameterTypes[3] = typeof(T4);
        }

        public LiteNetLibFunction(NetFunctionDelegate<T1, T2, T3, T4> callback) : this()
        {
            this.callback = callback;
        }

        public override void HookCallback()
        {
            callback((T1)Parameters[0], (T2)Parameters[1], (T3)Parameters[2], (T4)Parameters[3]);
        }
    }

    public class LiteNetLibFunction<T1, T2, T3, T4, T5> : LiteNetLibFunction
    {
        private NetFunctionDelegate<T1, T2, T3, T4, T5> callback;

        public LiteNetLibFunction()
        {
            Parameters = new object[5];
            ParameterTypes = new Type[5];
            ParameterTypes[0] = typeof(T1);
            ParameterTypes[1] = typeof(T2);
            ParameterTypes[2] = typeof(T3);
            ParameterTypes[3] = typeof(T4);
            ParameterTypes[4] = typeof(T5);
        }

        public LiteNetLibFunction(NetFunctionDelegate<T1, T2, T3, T4, T5> callback) : this()
        {
            this.callback = callback;
        }

        public override void HookCallback()
        {
            callback((T1)Parameters[0], (T2)Parameters[1], (T3)Parameters[2], (T4)Parameters[3], (T5)Parameters[4]);
        }
    }

    public class LiteNetLibFunction<T1, T2, T3, T4, T5, T6> : LiteNetLibFunction
    {
        private NetFunctionDelegate<T1, T2, T3, T4, T5, T6> callback;

        public LiteNetLibFunction()
        {
            Parameters = new object[6];
            ParameterTypes = new Type[6];
            ParameterTypes[0] = typeof(T1);
            ParameterTypes[1] = typeof(T2);
            ParameterTypes[2] = typeof(T3);
            ParameterTypes[3] = typeof(T4);
            ParameterTypes[4] = typeof(T5);
            ParameterTypes[5] = typeof(T6);
        }

        public LiteNetLibFunction(NetFunctionDelegate<T1, T2, T3, T4, T5, T6> callback) : this()
        {
            this.callback = callback;
        }

        public override void HookCallback()
        {
            callback((T1)Parameters[0], (T2)Parameters[1], (T3)Parameters[2], (T4)Parameters[3], (T5)Parameters[4], (T6)Parameters[5]);
        }
    }

    public class LiteNetLibFunction<T1, T2, T3, T4, T5, T6, T7> : LiteNetLibFunction
    {
        private NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7> callback;

        public LiteNetLibFunction()
        {
            Parameters = new object[7];
            ParameterTypes = new Type[7];
            ParameterTypes[0] = typeof(T1);
            ParameterTypes[1] = typeof(T2);
            ParameterTypes[2] = typeof(T3);
            ParameterTypes[3] = typeof(T4);
            ParameterTypes[4] = typeof(T5);
            ParameterTypes[5] = typeof(T6);
            ParameterTypes[6] = typeof(T7);
        }

        public LiteNetLibFunction(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7> callback) : this()
        {
            this.callback = callback;
        }

        public override void HookCallback()
        {
            callback((T1)Parameters[0], (T2)Parameters[1], (T3)Parameters[2], (T4)Parameters[3], (T5)Parameters[4], (T6)Parameters[5], (T7)Parameters[6]);
        }
    }

    public class LiteNetLibFunction<T1, T2, T3, T4, T5, T6, T7, T8> : LiteNetLibFunction
    {
        private NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8> callback;

        public LiteNetLibFunction()
        {
            Parameters = new object[8];
            ParameterTypes = new Type[8];
            ParameterTypes[0] = typeof(T1);
            ParameterTypes[1] = typeof(T2);
            ParameterTypes[2] = typeof(T3);
            ParameterTypes[3] = typeof(T4);
            ParameterTypes[4] = typeof(T5);
            ParameterTypes[5] = typeof(T6);
            ParameterTypes[6] = typeof(T7);
            ParameterTypes[7] = typeof(T8);
        }

        public LiteNetLibFunction(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8> callback) : this()
        {
            this.callback = callback;
        }

        public override void HookCallback()
        {
            callback((T1)Parameters[0], (T2)Parameters[1], (T3)Parameters[2], (T4)Parameters[3], (T5)Parameters[4], (T6)Parameters[5], (T7)Parameters[6], (T8)Parameters[7]);
        }
    }

    public class LiteNetLibFunction<T1, T2, T3, T4, T5, T6, T7, T8, T9> : LiteNetLibFunction
    {
        private NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8, T9> callback;

        public LiteNetLibFunction()
        {
            Parameters = new object[9];
            ParameterTypes = new Type[9];
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

        public LiteNetLibFunction(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8, T9> callback) : this()
        {
            this.callback = callback;
        }

        public override void HookCallback()
        {
            callback((T1)Parameters[0], (T2)Parameters[1], (T3)Parameters[2], (T4)Parameters[3], (T5)Parameters[4], (T6)Parameters[5], (T7)Parameters[6], (T8)Parameters[7], (T9)Parameters[8]);
        }
    }

    public class LiteNetLibFunction<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> : LiteNetLibFunction
    {
        private NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> callback;

        public LiteNetLibFunction()
        {
            Parameters = new object[10];
            ParameterTypes = new Type[10];
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

        public LiteNetLibFunction(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> callback) : this()
        {
            this.callback = callback;
        }

        public override void HookCallback()
        {
            callback((T1)Parameters[0], (T2)Parameters[1], (T3)Parameters[2], (T4)Parameters[3], (T5)Parameters[4], (T6)Parameters[5], (T7)Parameters[6], (T8)Parameters[7], (T9)Parameters[8], (T10)Parameters[9]);
        }
    }
    #endregion
}

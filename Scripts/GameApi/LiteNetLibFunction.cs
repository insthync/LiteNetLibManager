using UnityEngine;
using LiteNetLib;
using LiteNetLib.Utils;

namespace LiteNetLibHighLevel
{
    public struct NetFunctionInfo
    {
        public uint objectId;
        public int behaviourIndex;
        public ushort functionId;
        public NetFunctionInfo(uint objectId, int behaviourIndex, ushort functionId)
        {
            this.objectId = objectId;
            this.behaviourIndex = behaviourIndex;
            this.functionId = functionId;
        }
    }

    public enum FunctionReceivers : byte
    {
        Target,
        All,
        Server,
    }

    public class LiteNetLibFunction
    {
        private NetFunctionDelegate callback;

        public SendOptions sendOptions;
        [ReadOnly, SerializeField]
        protected LiteNetLibBehaviour behaviour;
        public LiteNetLibBehaviour Behaviour
        {
            get { return behaviour; }
        }

        [ReadOnly, SerializeField]
        protected ushort functionId;
        public ushort FunctionId
        {
            get { return functionId; }
        }

        [ReadOnly, SerializeField]
        protected LiteNetLibNetField[] parameters;
        public LiteNetLibNetField[] Parameters
        {
            get { return parameters; }
        }

        public LiteNetLibFunction()
        {
        }

        public LiteNetLibFunction(NetFunctionDelegate callback)
        {
            this.callback = callback;
        }

        public LiteNetLibGameManager Manager
        {
            get { return behaviour.Manager; }
        }

        public virtual void OnRegister(LiteNetLibBehaviour behaviour, ushort functionId)
        {
            this.behaviour = behaviour;
            this.functionId = functionId;
        }

        public NetFunctionInfo GetNetFunctionInfo()
        {
            return new NetFunctionInfo(Behaviour.ObjectId, Behaviour.BehaviourIndex, FunctionId);
        }

        public void Deserialize(NetDataReader reader)
        {
            if (Parameters == null || Parameters.Length == 0)
                return;
            for (var i = 0; i < Parameters.Length; ++i)
            {
                Parameters[i].Deserialize(reader);
            }
        }

        public void Serialize(NetDataWriter writer)
        {
            if (Parameters == null || Parameters.Length == 0)
                return;
            for (var i = 0; i < Parameters.Length; ++i)
            {
                Parameters[i].Serialize(writer);
            }
        }

        public virtual void HookCallback()
        {
            callback();
        }

        public void Call(FunctionReceivers receivers, params object[] parameterValues)
        {
            if (Parameters != null)
            {
                for (var i = 0; i < parameterValues.Length; ++i)
                {
                    Parameters[i].SetValue(parameterValues[i]);
                    if (i + 1 >= Parameters.Length)
                        break;
                }
            }
            Manager.CallNetFunction(receivers, this);
        }

        public void Call(long connectId, params object[] parameterValues)
        {
            if (Parameters != null)
            {
                for (var i = 0; i < parameterValues.Length; ++i)
                {
                    Parameters[i].SetValue(parameterValues[i]);
                    if (i + 1 >= Parameters.Length)
                        break;
                }
            }
            Manager.CallNetFunction(connectId, this);
        }
    }

    public class LiteNetLibFunction<T1> : LiteNetLibFunction
        where T1 : LiteNetLibNetField, new()
    {
        private NetFunctionDelegate<T1> callback;

        public LiteNetLibFunction()
        {
            parameters = new LiteNetLibNetField[1];
            parameters[0] = new T1();
        }

        public LiteNetLibFunction(NetFunctionDelegate<T1> callback) : this()
        {
            this.callback = callback;
        }

        public override void HookCallback()
        {
            callback(Parameters[0] as T1);
        }
    }

    public class LiteNetLibFunction<T1, T2> : LiteNetLibFunction
        where T1 : LiteNetLibNetField, new()
        where T2 : LiteNetLibNetField, new()
    {
        private NetFunctionDelegate<T1, T2> callback;

        public LiteNetLibFunction()
        {
            parameters = new LiteNetLibNetField[1];
            parameters[0] = new T1();
            parameters[1] = new T2();
        }

        public LiteNetLibFunction(NetFunctionDelegate<T1, T2> callback) : this()
        {
            this.callback = callback;
        }

        public override void HookCallback()
        {
            callback(
                Parameters[0] as T1, 
                Parameters[1] as T2);
        }
    }
    
    public class LiteNetLibFunction<T1, T2, T3> : LiteNetLibFunction
        where T1 : LiteNetLibNetField, new()
        where T2 : LiteNetLibNetField, new()
        where T3 : LiteNetLibNetField, new()
    {
        private NetFunctionDelegate<T1, T2, T3> callback;

        public LiteNetLibFunction()
        {
            parameters = new LiteNetLibNetField[1];
            parameters[0] = new T1();
            parameters[1] = new T2();
            parameters[2] = new T3();
        }

        public LiteNetLibFunction(NetFunctionDelegate<T1, T2, T3> callback) : this()
        {
            this.callback = callback;
        }

        public override void HookCallback()
        {
            callback(
                Parameters[0] as T1, 
                Parameters[1] as T2, 
                Parameters[2] as T3);
        }
    }

    public class LiteNetLibFunction<T1, T2, T3, T4> : LiteNetLibFunction
        where T1 : LiteNetLibNetField, new()
        where T2 : LiteNetLibNetField, new()
        where T3 : LiteNetLibNetField, new()
        where T4 : LiteNetLibNetField, new()
    {
        private NetFunctionDelegate<T1, T2, T3, T4> callback;

        public LiteNetLibFunction()
        {
            parameters = new LiteNetLibNetField[1];
            parameters[0] = new T1();
            parameters[1] = new T2();
            parameters[2] = new T3();
            parameters[3] = new T4();
        }

        public LiteNetLibFunction(NetFunctionDelegate<T1, T2, T3, T4> callback) : this()
        {
            this.callback = callback;
        }

        public override void HookCallback()
        {
            callback(
                Parameters[0] as T1, 
                Parameters[1] as T2, 
                Parameters[2] as T3, 
                Parameters[3] as T4);
        }
    }

    public class LiteNetLibFunction<T1, T2, T3, T4, T5> : LiteNetLibFunction
        where T1 : LiteNetLibNetField, new()
        where T2 : LiteNetLibNetField, new()
        where T3 : LiteNetLibNetField, new()
        where T4 : LiteNetLibNetField, new()
        where T5 : LiteNetLibNetField, new()
    {
        private NetFunctionDelegate<T1, T2, T3, T4, T5> callback;

        public LiteNetLibFunction()
        {
            parameters = new LiteNetLibNetField[1];
            parameters[0] = new T1();
            parameters[1] = new T2();
            parameters[2] = new T3();
            parameters[3] = new T4();
            parameters[4] = new T5();
        }

        public LiteNetLibFunction(NetFunctionDelegate<T1, T2, T3, T4, T5> callback) : this()
        {
            this.callback = callback;
        }

        public override void HookCallback()
        {
            callback(
                Parameters[0] as T1, 
                Parameters[1] as T2, 
                Parameters[2] as T3, 
                Parameters[3] as T4, 
                Parameters[4] as T5);
        }
    }

    public class LiteNetLibFunction<T1, T2, T3, T4, T5, T6> : LiteNetLibFunction
        where T1 : LiteNetLibNetField, new()
        where T2 : LiteNetLibNetField, new()
        where T3 : LiteNetLibNetField, new()
        where T4 : LiteNetLibNetField, new()
        where T5 : LiteNetLibNetField, new()
        where T6 : LiteNetLibNetField, new()
    {
        private NetFunctionDelegate<T1, T2, T3, T4, T5, T6> callback;

        public LiteNetLibFunction()
        {
            parameters = new LiteNetLibNetField[1];
            parameters[0] = new T1();
            parameters[1] = new T2();
            parameters[2] = new T3();
            parameters[3] = new T4();
            parameters[4] = new T5();
            parameters[5] = new T6();
        }

        public LiteNetLibFunction(NetFunctionDelegate<T1, T2, T3, T4, T5, T6> callback) : this()
        {
            this.callback = callback;
        }

        public override void HookCallback()
        {
            callback(
                Parameters[0] as T1,
                Parameters[1] as T2,
                Parameters[2] as T3,
                Parameters[3] as T4,
                Parameters[4] as T5,
                Parameters[5] as T6);
        }
    }

    public class LiteNetLibFunction<T1, T2, T3, T4, T5, T6, T7> : LiteNetLibFunction
        where T1 : LiteNetLibNetField, new()
        where T2 : LiteNetLibNetField, new()
        where T3 : LiteNetLibNetField, new()
        where T4 : LiteNetLibNetField, new()
        where T5 : LiteNetLibNetField, new()
        where T6 : LiteNetLibNetField, new()
        where T7 : LiteNetLibNetField, new()
    {
        private NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7> callback;

        public LiteNetLibFunction()
        {
            parameters = new LiteNetLibNetField[1];
            parameters[0] = new T1();
            parameters[1] = new T2();
            parameters[2] = new T3();
            parameters[3] = new T4();
            parameters[4] = new T5();
            parameters[5] = new T6();
            parameters[6] = new T7();
        }

        public LiteNetLibFunction(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7> callback) : this()
        {
            this.callback = callback;
        }

        public override void HookCallback()
        {
            callback(
                Parameters[0] as T1,
                Parameters[1] as T2,
                Parameters[2] as T3,
                Parameters[3] as T4,
                Parameters[4] as T5,
                Parameters[5] as T6,
                Parameters[6] as T7);
        }
    }

    public class LiteNetLibFunction<T1, T2, T3, T4, T5, T6, T7, T8> : LiteNetLibFunction
        where T1 : LiteNetLibNetField, new()
        where T2 : LiteNetLibNetField, new()
        where T3 : LiteNetLibNetField, new()
        where T4 : LiteNetLibNetField, new()
        where T5 : LiteNetLibNetField, new()
        where T6 : LiteNetLibNetField, new()
        where T7 : LiteNetLibNetField, new()
        where T8 : LiteNetLibNetField, new()
    {
        private NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8> callback;

        public LiteNetLibFunction()
        {
            parameters = new LiteNetLibNetField[1];
            parameters[0] = new T1();
            parameters[1] = new T2();
            parameters[2] = new T3();
            parameters[3] = new T4();
            parameters[4] = new T5();
            parameters[5] = new T6();
            parameters[6] = new T7();
            parameters[7] = new T8();
        }

        public LiteNetLibFunction(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8> callback) : this()
        {
            this.callback = callback;
        }

        public override void HookCallback()
        {
            callback(
                Parameters[0] as T1,
                Parameters[1] as T2,
                Parameters[2] as T3,
                Parameters[3] as T4,
                Parameters[4] as T5,
                Parameters[5] as T6,
                Parameters[6] as T7,
                Parameters[7] as T8);
        }
    }

    public class LiteNetLibFunction<T1, T2, T3, T4, T5, T6, T7, T8, T9> : LiteNetLibFunction
        where T1 : LiteNetLibNetField, new()
        where T2 : LiteNetLibNetField, new()
        where T3 : LiteNetLibNetField, new()
        where T4 : LiteNetLibNetField, new()
        where T5 : LiteNetLibNetField, new()
        where T6 : LiteNetLibNetField, new()
        where T7 : LiteNetLibNetField, new()
        where T8 : LiteNetLibNetField, new()
        where T9 : LiteNetLibNetField, new()
    {
        private NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8, T9> callback;

        public LiteNetLibFunction()
        {
            parameters = new LiteNetLibNetField[1];
            parameters[0] = new T1();
            parameters[1] = new T2();
            parameters[2] = new T3();
            parameters[3] = new T4();
            parameters[4] = new T5();
            parameters[5] = new T6();
            parameters[6] = new T7();
            parameters[7] = new T8();
            parameters[8] = new T9();
        }

        public LiteNetLibFunction(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8, T9> callback) : this()
        {
            this.callback = callback;
        }

        public override void HookCallback()
        {
            callback(
                Parameters[0] as T1,
                Parameters[1] as T2,
                Parameters[2] as T3,
                Parameters[3] as T4,
                Parameters[4] as T5,
                Parameters[5] as T6,
                Parameters[6] as T7,
                Parameters[7] as T8,
                Parameters[8] as T9);
        }
    }

    public class LiteNetLibFunction<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> : LiteNetLibFunction
        where T1 : LiteNetLibNetField, new()
        where T2 : LiteNetLibNetField, new()
        where T3 : LiteNetLibNetField, new()
        where T4 : LiteNetLibNetField, new()
        where T5 : LiteNetLibNetField, new()
        where T6 : LiteNetLibNetField, new()
        where T7 : LiteNetLibNetField, new()
        where T8 : LiteNetLibNetField, new()
        where T9 : LiteNetLibNetField, new()
        where T10 : LiteNetLibNetField, new()
    {
        private NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> callback;

        public LiteNetLibFunction()
        {
            parameters = new LiteNetLibNetField[1];
            parameters[0] = new T1();
            parameters[1] = new T2();
            parameters[2] = new T3();
            parameters[3] = new T4();
            parameters[4] = new T5();
            parameters[5] = new T6();
            parameters[6] = new T7();
            parameters[7] = new T8();
            parameters[8] = new T9();
            parameters[9] = new T10();
        }

        public LiteNetLibFunction(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> callback) : this()
        {
            this.callback = callback;
        }

        public override void HookCallback()
        {
            callback(
                Parameters[0] as T1,
                Parameters[1] as T2,
                Parameters[2] as T3,
                Parameters[3] as T4,
                Parameters[4] as T5,
                Parameters[5] as T6,
                Parameters[6] as T7,
                Parameters[7] as T8,
                Parameters[8] as T9,
                Parameters[9] as T10);
        }
    }
}

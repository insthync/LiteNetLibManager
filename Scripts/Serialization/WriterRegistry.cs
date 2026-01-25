using LiteNetLib.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace LiteNetLibManager.Serialization
{
    public static partial class WriterRegistry
    {
        private static readonly Dictionary<Type, Action<NetDataWriter, object>> _writers = new Dictionary<Type, Action<NetDataWriter, object>>();

        static WriterRegistry()
        {
            // Register built-in types
            RegisterWriter(typeof(bool), WriteBool);
            RegisterWriter(typeof(byte), WriteByte);
            RegisterWriter(typeof(sbyte), WriteSByte);
            RegisterWriter(typeof(char), WriteChar);
            RegisterWriter(typeof(double), WriteDouble);
            RegisterWriter(typeof(float), WriteSingle);
            RegisterWriter(typeof(short), WriteInt16);
            RegisterWriter(typeof(ushort), WriteUInt16);
            RegisterWriter(typeof(int), WriteInt32);
            RegisterWriter(typeof(uint), WriteUInt32);
            RegisterWriter(typeof(long), WriteInt64);
            RegisterWriter(typeof(ulong), WriteUInt64);
            RegisterWriter(typeof(string), WriteString);
            RegisterWriter(typeof(Color), WriteColor);
            RegisterWriter(typeof(Quaternion), WriteQuaternion);
            RegisterWriter(typeof(Vector2), WriteVector2);
            RegisterWriter(typeof(Vector2Int), WriteVector2Int);
            RegisterWriter(typeof(Vector3), WriteVector3);
            RegisterWriter(typeof(Vector3Int), WriteVector3Int);
            RegisterWriter(typeof(Vector4), WriteVector4);
            RegisterWritersViaReflection();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
        }

        private static void RegisterWritersViaReflection()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic &&
                           !a.FullName.StartsWith("System") &&
                           !a.FullName.StartsWith("Unity") &&
                           !a.FullName.StartsWith("mscorlib"));

            foreach (Assembly assembly in assemblies)
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    // Handle assemblies that can't be fully loaded
                    types = ex.Types.Where(t => t != null).ToArray();
                }
                catch (Exception)
                {
                    continue; // Skip assemblies that can't be loaded
                }

                foreach (Type type in types)
                {
                    MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

                    foreach (MethodInfo method in methods)
                    {
                        // Look for WriterRegisterAttribute
                        var attribute = method.GetCustomAttribute<WriterRegisterAttribute>();
                        if (attribute == null)
                            continue;

                        if (!ValidateWriterMethod(method, out string error))
                        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                            Debug.LogWarning($"Invalid writer method {type.Name}.{method.Name}: {error}");
#endif
                            continue;
                        }

                        try
                        {
                            var writerAction = (Action<NetDataWriter, object>)Delegate.CreateDelegate(
                                typeof(Action<NetDataWriter, object>),
                                method,
                                throwOnBindFailure: false);

                            if (writerAction != null)
                                RegisterWriter(attribute.Type, writerAction);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"Failed to register writer for {attribute.Type}: {ex.Message}");
                        }
                    }
                }
            }
        }

        private static bool ValidateWriterMethod(MethodInfo method, out string error)
        {
            error = null;

            // Check return type
            if (method.ReturnType != typeof(void))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                error = $"Method must return void, but returns {method.ReturnType.Name}";
#endif
                return false;
            }

            // Check parameter count
            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length != 2)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                error = $"Method must have exactly 2 parameters, but has {parameters.Length}";
#endif
                return false;
            }

            // Check first parameter type
            if (parameters[0].ParameterType != typeof(NetDataWriter))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                error = $"First parameter must be NetDataWriter, but is {parameters[0].ParameterType.Name}";
#endif
                return false;
            }

            // Check second parameter type
            if (parameters[1].ParameterType != typeof(object))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                error = $"Second parameter must be object, but is {parameters[1].ParameterType.Name}";
#endif
                return false;
            }

            return true;
        }

        public static void RegisterWriter(Type type, Action<NetDataWriter, object> func)
        {
            _writers[type] = func;
        }

        public static bool TryGetWriter(Type type, out Action<NetDataWriter, object> func)
        {
            return _writers.TryGetValue(type, out func);
        }

        public static void WriteBool(NetDataWriter writer, object value) => writer.Put((bool)value);
        public static void WriteByte(NetDataWriter writer, object value) => writer.Put((byte)value);
        public static void WriteSByte(NetDataWriter writer, object value) => writer.Put((sbyte)value);
        public static void WriteChar(NetDataWriter writer, object value) => writer.Put((char)value);
        public static void WriteDouble(NetDataWriter writer, object value) => writer.Put((double)value);
        public static void WriteSingle(NetDataWriter writer, object value) => writer.Put((bool)value);
        public static void WriteInt16(NetDataWriter writer, object value) => writer.PutPackedShort((short)value);
        public static void WriteUInt16(NetDataWriter writer, object value) => writer.PutPackedUShort((ushort)value);
        public static void WriteInt32(NetDataWriter writer, object value) => writer.PutPackedInt((int)value);
        public static void WriteUInt32(NetDataWriter writer, object value) => writer.PutPackedUInt((uint)value);
        public static void WriteInt64(NetDataWriter writer, object value) => writer.PutPackedLong((long)value);
        public static void WriteUInt64(NetDataWriter writer, object value) => writer.PutPackedULong((ulong)value);
        public static void WriteString(NetDataWriter writer, object value) => writer.Put((string)value);
        public static void WriteColor(NetDataWriter writer, object value) => writer.PutColor((Color)value);
        public static void WriteQuaternion(NetDataWriter writer, object value) => writer.PutQuaternion((Quaternion)value);
        public static void WriteVector2(NetDataWriter writer, object value) => writer.PutVector2((Vector2)value);
        public static void WriteVector2Int(NetDataWriter writer, object value) => writer.PutVector2Int((Vector2Int)value);
        public static void WriteVector3(NetDataWriter writer, object value) => writer.PutVector3((Vector3)value);
        public static void WriteVector3Int(NetDataWriter writer, object value) => writer.PutVector3Int((Vector3Int)value);
        public static void WriteVector4(NetDataWriter writer, object value) => writer.PutVector4((Vector4)value);
    }
}
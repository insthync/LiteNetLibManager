using LiteNetLib.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace LiteNetLibManager.Serialization
{
    public static partial class ReaderRegistry
    {
        private static readonly Dictionary<Type, Func<NetDataReader, object>> _readers = new Dictionary<Type, Func<NetDataReader, object>>();

        static ReaderRegistry()
        {
            // Register built-in types
            RegisterReader(typeof(bool), ReadBool);
            RegisterReader(typeof(byte), ReadByte);
            RegisterReader(typeof(sbyte), ReadSByte);
            RegisterReader(typeof(char), ReadChar);
            RegisterReader(typeof(double), ReadDouble);
            RegisterReader(typeof(float), ReadSingle);
            RegisterReader(typeof(short), ReadInt16);
            RegisterReader(typeof(ushort), ReadUInt16);
            RegisterReader(typeof(int), ReadInt32);
            RegisterReader(typeof(uint), ReadUInt32);
            RegisterReader(typeof(long), ReadInt64);
            RegisterReader(typeof(ulong), ReadUInt64);
            RegisterReader(typeof(string), ReadString);
            RegisterReader(typeof(Color), ReadColor);
            RegisterReader(typeof(Quaternion), ReadQuaternion);
            RegisterReader(typeof(Vector2), ReadVector2);
            RegisterReader(typeof(Vector2Int), ReadVector2Int);
            RegisterReader(typeof(Vector3), ReadVector3);
            RegisterReader(typeof(Vector3Int), ReadVector3Int);
            RegisterReader(typeof(Vector4), ReadVector4);
            RegisterReadersViaReflection();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
        }

        private static void RegisterReadersViaReflection()
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
                        var attribute = method.GetCustomAttribute<ReaderRegisterAttribute>(false);
                        if (attribute == null)
                            continue;

                        if (!ValidateReaderMethod(method, out string error))
                        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                            Debug.LogWarning($"Invalid reader method {type.Name}.{method.Name}: {error}");
#endif
                            continue;
                        }

                        try
                        {
                            var readerFunc = (Func<NetDataReader, object>)Delegate.CreateDelegate(
                                typeof(Func<NetDataReader, object>),
                                method,
                                throwOnBindFailure: false);

                            if (readerFunc != null)
                                RegisterReader(attribute.Type, readerFunc);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"Failed to register reader for {attribute.Type.Name}: {ex.Message}");
                        }
                    }
                }
            }
        }

        private static bool ValidateReaderMethod(MethodInfo method, out string error)
        {
            error = null;

            if (method.ReturnType != typeof(object))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                error = $"Method must return object, but returns {method.ReturnType.Name}";
#endif
                return false;
            }

            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length != 1)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                error = $"Method must have exactly 1 parameter, but has {parameters.Length}";
#endif
                return false;
            }

            if (parameters[0].ParameterType != typeof(NetDataReader))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                error = $"First parameter must be NetDataReader, but is {parameters[0].ParameterType.Name}";
#endif
                return false;
            }

            return true;
        }

        public static void RegisterReader(Type type, Func<NetDataReader, object> func)
        {
            _readers[type] = func;
        }

        public static bool TryGetReader(Type type, out Func<NetDataReader, object> func)
        {
            return _readers.TryGetValue(type, out func);
        }

        public static object ReadBool(NetDataReader reader) => reader.GetBool();
        public static object ReadByte(NetDataReader reader) => reader.GetByte();
        public static object ReadSByte(NetDataReader reader) => reader.GetSByte();
        public static object ReadChar(NetDataReader reader) => reader.GetChar();
        public static object ReadDouble(NetDataReader reader) => reader.GetDouble();
        public static object ReadSingle(NetDataReader reader) => reader.GetFloat();
        public static object ReadInt16(NetDataReader reader) => reader.GetPackedShort();
        public static object ReadUInt16(NetDataReader reader) => reader.GetPackedUShort();
        public static object ReadInt32(NetDataReader reader) => reader.GetPackedInt();
        public static object ReadUInt32(NetDataReader reader) => reader.GetPackedUInt();
        public static object ReadInt64(NetDataReader reader) => reader.GetPackedLong();
        public static object ReadUInt64(NetDataReader reader) => reader.GetPackedULong();
        public static object ReadString(NetDataReader reader) => reader.GetString();
        public static object ReadColor(NetDataReader reader) => reader.GetColor();
        public static object ReadQuaternion(NetDataReader reader) => reader.GetQuaternion();
        public static object ReadVector2(NetDataReader reader) => reader.GetVector2();
        public static object ReadVector2Int(NetDataReader reader) => reader.GetVector2Int();
        public static object ReadVector3(NetDataReader reader) => reader.GetVector3();
        public static object ReadVector3Int(NetDataReader reader) => reader.GetVector3Int();
        public static object ReadVector4(NetDataReader reader) => reader.GetVector4();
    }
}
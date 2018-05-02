using LiteNetLib.Utils;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;

namespace LiteNetLibManager
{
    public class NetFieldStruct<T> : LiteNetLibNetField<T>
        where T : struct
    {
        public override void Deserialize(NetDataReader reader)
        {
            using (MemoryStream memoryStream = new MemoryStream(reader.GetBytesWithLength()))
            {
                var binaryFormatter = new BinaryFormatter();
                // Setup Unity's structs serialization surrogates
                var surrogateSelector = new SurrogateSelector();
                surrogateSelector.AddAllUnitySurrogate();
                binaryFormatter.SurrogateSelector = surrogateSelector;
                // Deserialize
                var data = binaryFormatter.Deserialize(memoryStream);
                Value = (T)data;
            }
        }

        public override void Serialize(NetDataWriter writer)
        {
            using (var memoryStream = new MemoryStream())
            {
                var binaryFormatter = new BinaryFormatter();
                // Setup Unity's structs serialization surrogates
                var surrogateSelector = new SurrogateSelector();
                surrogateSelector.AddAllUnitySurrogate();
                binaryFormatter.SurrogateSelector = surrogateSelector;
                // Serialize and put to packet
                binaryFormatter.Serialize(memoryStream, Value);
                memoryStream.Flush();
                memoryStream.Seek(0, SeekOrigin.Begin);
                var bytes = memoryStream.ToArray();
                writer.PutBytesWithLength(bytes);
            }
        }

        public override bool IsValueChanged(T newValue)
        {
            return !newValue.Equals(Value);
        }
    }
}

using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using UGF.JsonNet.Runtime;

namespace UGF.JsonNet.Bson.Runtime
{
    public static class JsonNetBsonUtility
    {
        public static byte[] ToBson<T>(T target)
        {
            return ToBson(target, typeof(T), JsonNetUtility.DefaultSettings);
        }

        public static byte[] ToBson<T>(T target, JsonSerializerSettings settings)
        {
            return ToBson(target, typeof(T), settings);
        }

        public static byte[] ToBson(object target, Type targetType)
        {
            return ToBson(target, targetType, JsonNetUtility.DefaultSettings);
        }

        public static byte[] ToBson(object target, Type targetType, JsonSerializerSettings settings)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (targetType == null) throw new ArgumentNullException(nameof(targetType));
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            using var stream = new MemoryStream();
            using var writer = new BsonDataWriter(stream);
            var serializer = JsonSerializer.Create(settings);

            serializer.Serialize(writer, target, targetType);

            return stream.ToArray();
        }

        public static T FromBson<T>(byte[] bytes)
        {
            return (T)FromBson(bytes, typeof(T), JsonNetUtility.DefaultSettings);
        }

        public static T FromBson<T>(byte[] bytes, JsonSerializerSettings settings)
        {
            return (T)FromBson(bytes, typeof(T), settings);
        }

        public static object FromBson(byte[] bytes, Type targetType)
        {
            return FromBson(bytes, targetType, JsonNetUtility.DefaultSettings);
        }

        public static object FromBson(byte[] bytes, Type targetType, JsonSerializerSettings settings)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            if (targetType == null) throw new ArgumentNullException(nameof(targetType));
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            using var stream = new MemoryStream(bytes);
            using var reader = new BsonDataReader(stream);
            var serializer = JsonSerializer.Create(settings);

            object target = serializer.Deserialize(reader, targetType);

            return target;
        }
    }
}

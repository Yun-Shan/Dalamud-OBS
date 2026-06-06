using System;
using System.Linq;
using Newtonsoft.Json;

namespace OBSPlugin.Services
{
    public class BitSet
    {
        private long[] _bits;
        private int _size;

        public BitSet(int capacity = 0)
        {
            _size = 0;
            _bits = capacity > 0 ? new long[(capacity + 63) / 64] : Array.Empty<long>();
        }

        public int Size => _size;

        public void Set(int index, bool value)
        {
            if (index >= _size)
            {
                // 自动扩容
                int newSize = index + 1;
                int newArraySize = (newSize + 63) / 64;
                Array.Resize(ref _bits, newArraySize);
                _size = newSize;
            }

            int arrayIndex = index / 64;
            int bitIndex = index % 64;

            if (value)
                _bits[arrayIndex] |= (1L << bitIndex);
            else
                _bits[arrayIndex] &= ~(1L << bitIndex);
        }

        public bool Get(int index)
        {
            if (index >= _size) return false;
            int arrayIndex = index / 64;
            int bitIndex = index % 64;
            return (_bits[arrayIndex] & (1L << bitIndex)) != 0;
        }

        public void SetRange(int startIndex, int count, bool value)
        {
            for (int i = 0; i < count; i++)
            {
                Set(startIndex + i, value);
            }
        }

        public int CountRange(int startIndex, int count)
        {
            int count_ = 0;
            for (int i = 0; i < count; i++)
            {
                if (Get(startIndex + i)) count_++;
            }
            return count_;
        }

        public long[] ToArray() => _bits;

        public void LoadFrom(long[] array)
        {
            _bits = array ?? Array.Empty<long>();
            _size = _bits.Length * 64;
        }
    }

    public class BitSetConverter : JsonConverter<BitSet>
    {
        public override BitSet ReadJson(JsonReader reader, Type objectType, BitSet existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return null;

            string base64 = reader.Value as string;
            if (string.IsNullOrEmpty(base64))
            {
                return new BitSet(0);
            }

            byte[] bytes = Convert.FromBase64String(base64);
            int longCount = bytes.Length / 8;
            long[] longs = new long[longCount];
            Buffer.BlockCopy(bytes, 0, longs, 0, bytes.Length);

            var bitSet = new BitSet(0);
            bitSet.LoadFrom(longs);
            return bitSet;
        }

        public override void WriteJson(JsonWriter writer, BitSet value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            long[] bits = value.ToArray();
            int byteCount = bits.Length * 8;
            byte[] bytes = new byte[byteCount];
            Buffer.BlockCopy(bits, 0, bytes, 0, byteCount);

            string base64 = Convert.ToBase64String(bytes);
            writer.WriteValue(base64);
        }
    }
}
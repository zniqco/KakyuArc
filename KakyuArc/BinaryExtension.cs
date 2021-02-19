using System;
using System.IO;
using System.Text;

namespace KakyuArc
{
    public static class BinaryExtension
    {
        public static string ReadStringEncrypted(this BinaryReader reader, int length)
        {
            var data = reader.ReadBytes(length);

            for (var i = 0; i < data.Length; ++i)
            {
                data[i] = Decrypt(data[i]);

                if (data[i] == 0)
                    return Encoding.ASCII.GetString(data, 0, i);
            }

            return Encoding.ASCII.GetString(data, 0, data.Length);
        }

        public static ushort ReadUInt16Encrypted(this BinaryReader reader)
        {
            var byte1 = Decrypt(reader.ReadByte());
            var byte2 = Decrypt(reader.ReadByte());

            return (ushort)((byte2 << 8) | byte1);
        }

        public static void WriteStringEncrypted(this BinaryWriter writer, string text, int length)
        {
            var data = new byte[length];
            var bytes = Encoding.ASCII.GetBytes(text);

            Array.Copy(bytes, data, bytes.Length);

            for (var i = 0; i < data.Length; ++i)
                data[i] = Encrypt(data[i]);

            writer.Write(data);
        }

        public static void WriteUInt16Encrypted(this BinaryWriter writer, ushort value)
        {
            var byte1 = Encrypt((byte)(value & 0xFF));
            var byte2 = Encrypt((byte)((value >> 8) & 0xFF));

            writer.Write(byte1);
            writer.Write(byte2);
        }

        private static byte Decrypt(byte value)
        {
            // rotl 1, xor 0x55
            return (byte)(((value << 1) | (value >> 7)) ^ 0x55);
        }

        private static byte Encrypt(byte value)
        {
            // rotr 1, xor 0xAA
            return (byte)(((value >> 1) | (value << 7)) ^ 0xAA);
        }
    }
}

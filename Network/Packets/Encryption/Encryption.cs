//By AlSch092 @ Github
﻿using System;
using System.Runtime.InteropServices;

namespace Mir4ClientEmulator.Network.Packets.Encryption
{
    internal class Encryption
    {
        [DllImport("Mir4Encryption.dll", EntryPoint = "Encrypt", CallingConvention = CallingConvention.Cdecl)]
        private static extern int NativeEncode([In] byte[] data, [Out] byte[] outData, int length);

        public static int Encode(byte[] data, byte[] outData, int length)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (outData == null) throw new ArgumentNullException(nameof(outData));
            if (length < 12 || length > 8187 || length > data.Length)
                throw new ArgumentOutOfRangeException(nameof(length));
            int needed = ((length + 19) & 0x00fffff0) + 1;
            if (outData.Length < needed) throw new ArgumentException("Output buffer is too small.", nameof(outData));
            int encoded = NativeEncode(data, outData, length);
            if (encoded <= 0 || encoded > outData.Length)
                throw new InvalidOperationException("Native encryption returned an invalid packet length.");
            return encoded;
        }
    }
}

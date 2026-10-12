using System;
namespace Mir4ClientEmulator.Network.Packets.Encryption
{
    public sealed class CurrentPacketCipher
    {
        private readonly byte[] substitution;
        private readonly byte[] roundConstants;

        // Experimental codec: parameters must come from a verified client version.
        // It is deliberately not selected by the live Session send path.
        public CurrentPacketCipher(byte[] substitution, byte[] roundConstants)
        {
            Require(substitution, 256, nameof(substitution));
            if (roundConstants == null || roundConstants.Length < 11)
                throw new ArgumentException("At least eleven round constants are required.", nameof(roundConstants));
            this.substitution = (byte[])substitution.Clone();
            this.roundConstants = (byte[])roundConstants.Clone();
        }

        private static void Require(byte[] bytes, int length, string name)
        {
            if (bytes == null || bytes.Length != length)
                throw new ArgumentException("Expected " + length + " bytes.", name);
        }

        public byte[] ExpandKey(byte[] key)
        {
            Require(key, 16, nameof(key));
            byte[] expanded = new byte[176];
            Array.Copy(key, expanded, 16);
            for (int word = 4; word < 44; word++)
            {
                byte[] t = new byte[4];
                Array.Copy(expanded, (word - 1) * 4, t, 0, 4);
                if (word % 4 == 0)
                {
                    byte first = t[0];
                    t[0] = (byte)(substitution[t[1]] ^ roundConstants[word / 4]);
                    t[1] = substitution[t[2]];
                    t[2] = substitution[t[3]];
                    t[3] = substitution[first];
                }
                for (int j = 0; j < 4; j++)
                    expanded[word * 4 + j] = (byte)(expanded[(word - 4) * 4 + j] ^ t[j]);
            }
            return expanded;
        }

        private static byte MultiplyByTwo(byte value)
        {
            return (byte)((value << 1) ^ ((value & 128) != 0 ? 0x1b : 0));
        }

        public byte[] EncryptBlock(byte[] block, byte[] expandedKey)
        {
            Require(block, 16, nameof(block));
            Require(expandedKey, 176, nameof(expandedKey));
            byte[] state = (byte[])block.Clone();
            for (int j = 0; j < 16; j++) state[j] ^= expandedKey[j];
            for (int round = 1; round <= 10; round++)
            {
                for (int j = 0; j < 16; j++) state[j] = substitution[state[j]];
                byte[] beforeShift = (byte[])state.Clone();
                for (int row = 0; row < 4; row++)
                    for (int column = 0; column < 4; column++)
                        state[column * 4 + row] = beforeShift[((column + row) % 4) * 4 + row];
                if (round != 10)
                    for (int column = 0; column < 4; column++)
                    {
                        int offset = column * 4;
                        byte[] b = new byte[4];
                        Array.Copy(state, offset, b, 0, 4);
                        byte total = (byte)(b[0] ^ b[1] ^ b[2] ^ b[3]);
                        for (int j = 0; j < 4; j++)
                            state[offset + j] = (byte)(b[j] ^ total ^ MultiplyByTwo((byte)(b[j] ^ b[(j + 1) % 4])));
                    }
                for (int j = 0; j < 16; j++) state[j] ^= expandedKey[round * 16 + j];
            }
            return state;
        }

        public byte[] EncryptPreparedFrame(byte[] frame, byte[] expandedKey, int variant)
        {
            if (frame == null || frame.Length < 17 || frame.Length > 1024 * 1024)
                throw new ArgumentException("A prepared frame must contain 17 to 1048576 bytes.", nameof(frame));
            Require(expandedKey, 176, nameof(expandedKey));
            if (variant != 0 && variant != 1) throw new ArgumentOutOfRangeException(nameof(variant));
            // The caller supplies a fully prepared header, including its verified timestamp.
            int size = ((frame.Length + 14) & 0x00fffff0) + 1;
            byte[] output = new byte[size];
            Array.Copy(frame, output, frame.Length);
            for (int i = frame.Length; i < size; i++) output[i] = 4;
            byte[] block = new byte[16];
            Array.Copy(output, block, 16);
            Array.Copy(EncryptBlock(block, expandedKey), output, 16);
            int checksum = output[15];
            foreach (int i in new[] { 12, 9, 6, 3 }) checksum -= output[i];
            foreach (int i in new[] { 14, 13, 11, 10, 8, 7, 5, 4, 2, 1, 0 }) checksum += output[i];
            checksum = (checksum + (variant == 1 ? 0x49 : 0)) & 255;
            output[16] = (byte)(checksum == 0 ? variant + 1 : checksum);
            int previous = 0;
            for (int offset = 17; offset < size; offset += 16)
            {
                for (int i = 0; i < 16; i++) block[i] = (byte)(output[offset + i] ^ output[previous + i]);
                Array.Copy(EncryptBlock(block, expandedKey), 0, output, offset, 16);
                previous = offset;
            }
            return output;
        }
    }
}

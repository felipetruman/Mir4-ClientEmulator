using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Runtime.InteropServices;
using Mir4ClientEmulator.Network.Packets.Encryption;

using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "vectors.json")));
byte[] Bytes(JsonElement e, string field) => Convert.FromHexString(e.GetProperty(field).GetString());
var cipher = new CurrentPacketCipher(Bytes(doc.RootElement, "sbox"), Bytes(doc.RootElement, "rcon"));
int passed = 0;
foreach (var v in doc.RootElement.GetProperty("vectors").EnumerateArray())
{
    var expanded = cipher.ExpandKey(Bytes(v, "key"));
    if (!expanded.SequenceEqual(Bytes(v, "expanded"))) throw new Exception("Native key expansion mismatch");
    passed++;
    if (!cipher.EncryptBlock(Bytes(v, "plain"), expanded).SequenceEqual(Bytes(v, "encrypted")))
        throw new Exception("Native block encryption mismatch");
    passed++;
}
Console.WriteLine($"PASS: {passed} native-reference cipher checks");
foreach (var v in doc.RootElement.GetProperty("frames").EnumerateArray())
{
    var encrypted = cipher.EncryptPreparedFrame(Bytes(v, "frame"), cipher.ExpandKey(Bytes(v, "key")), v.GetProperty("variant").GetInt32());
    if (!encrypted.SequenceEqual(Bytes(v, "encrypted"))) throw new Exception("Native packet-tail mismatch");
    passed++;
}
Console.WriteLine($"PASS: {passed} total native-reference checks");
void Reject(Action call)
{
    try { call(); } catch (ArgumentException) { passed++; return; }
    throw new Exception("Unsafe native import input was not rejected");
}
Reject(() => Encryption.Encode(null, new byte[64], 12));
Reject(() => Encryption.Encode(new byte[12], new byte[16], 12));
Reject(() => Encryption.Encode(new byte[12], new byte[64], 13));
Reject(() => Encryption.Encode(new byte[11], new byte[64], 11));
Console.WriteLine($"PASS: {passed} total checks including managed/native boundary validation");
if (args.Contains("--managed-only"))
{
    Console.WriteLine("Native integration explicitly skipped; a runnable build has not been verified.");
    return;
}
foreach (var v in doc.RootElement.GetProperty("vectors").EnumerateArray())
{
    var expanded = new byte[176];
    if (Native.Expand(Bytes(v, "key"), expanded) != 176 || !expanded.SequenceEqual(Bytes(v, "expanded")))
        throw new Exception("DLL key expansion mismatch");
    passed++;
    var output = new byte[16];
    if (Native.Block(Bytes(v, "plain"), output, expanded) != 16 || !output.SequenceEqual(Bytes(v, "encrypted")))
        throw new Exception("DLL block encryption mismatch");
    passed++;
}
foreach (var v in doc.RootElement.GetProperty("frames").EnumerateArray())
{
    var frame = Bytes(v, "frame"); var output = new byte[frame.Length + 30];
    int length = Native.Prepared(frame, output, frame.Length, output.Length,
        cipher.ExpandKey(Bytes(v, "key")), v.GetProperty("variant").GetInt32());
    if (!output.Take(length).SequenceEqual(Bytes(v, "encrypted"))) throw new Exception("DLL prepared frame mismatch");
    passed++;
}
using var profile = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "current-profile.json")));
if (profile.RootElement.GetProperty("client_sha256").GetString() != doc.RootElement.GetProperty("client_sha256").GetString())
    throw new Exception("Native profile and reference vectors target different client builds");
var currentKey = cipher.ExpandKey(Bytes(profile.RootElement, "key_variant0"));
foreach (int size in new[] { 12, 13, 27, 28, 29, 43, 44, 45, 1019 })
    foreach (var clock in new[] { (0UL, 0UL), (17916804001234567UL, 0UL), (17916804001234567UL, 123UL) })
    {
        byte[] input = Enumerable.Range(0, size).Select(i => (byte)(i * 17 + 3)).ToArray();
        byte[] frame = new byte[size + 5];
        BitConverter.GetBytes(size + 5).CopyTo(frame, 0); input.CopyTo(frame, 5);
        BitConverter.GetBytes(clock.Item1 / 10000000 * 1257787 + clock.Item2).CopyTo(frame, 7);
        var expected = cipher.EncryptPreparedFrame(frame, currentKey, 0); var output = new byte[size + 30];
        int length = Native.Timed(input, output, size, output.Length, clock.Item1, clock.Item2, 0);
        if (!output.Take(length).SequenceEqual(expected)) throw new Exception("DLL current-key timestamp packet mismatch");
        passed++;
    }
var liveOutput = new byte[64];
if (Encryption.Encode(new byte[12], liveOutput, 12) != 17) throw new Exception("Application Encode import failed");
passed++;
Console.WriteLine($"PASS: {passed} total checks including the compiled DLL and application Encode import");

static class Native
{
    [DllImport("Mir4Encryption.dll", EntryPoint="CipherExpandKey", CallingConvention=CallingConvention.Cdecl)]
    internal static extern int Expand(byte[] key, [Out] byte[] expanded);
    [DllImport("Mir4Encryption.dll", EntryPoint="CipherEncryptBlock", CallingConvention=CallingConvention.Cdecl)]
    internal static extern int Block(byte[] plain, [Out] byte[] output, byte[] expanded);
    [DllImport("Mir4Encryption.dll", EntryPoint="CipherEncryptPrepared", CallingConvention=CallingConvention.Cdecl)]
    internal static extern int Prepared(byte[] frame, [Out] byte[] output, int length, int capacity, byte[] expanded, int variant);
    [DllImport("Mir4Encryption.dll", EntryPoint="EncryptWithTicks", CallingConvention=CallingConvention.Cdecl)]
    internal static extern int Timed(byte[] input, [Out] byte[] output, int length, int capacity, ulong ticks, ulong counter, byte flag);
}

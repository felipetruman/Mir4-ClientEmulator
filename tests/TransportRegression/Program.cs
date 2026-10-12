using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Reflection;
using Mir4ClientEmulator.Network;
using Mir4ClientEmulator.Context;
using Mir4ClientEmulator.Network.Packets;

static class Program {
 static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
 static void Feed(Session s, byte[] bytes) {
  var recv=(byte[])typeof(Session).GetField("m_recvBuffer",Flags).GetValue(s);
  Array.Copy(bytes,recv,bytes.Length);
  typeof(Session).GetMethod("Append",Flags).Invoke(s,new object[]{bytes.Length});
  typeof(Session).GetMethod("ManipulateBuffer",Flags).Invoke(s,null);
 }
 static byte[] Packet(int size, byte marker) {
  var b=new byte[size];Array.Copy(BitConverter.GetBytes(size),b,4);b[5]=marker;return b;
 }
 static void Check(bool condition,string message) {if(!condition)throw new Exception(message);}
 static void Main() {
  int fails=0;
  void Test(string name,Action test) {try{test();Console.WriteLine("PASS "+name);}catch(Exception e){fails++;Console.WriteLine("FAIL "+name+": "+(e.InnerException??e).Message);}}
  Test("empty heartbeat delivered immediately",()=>{var s=new Session(new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp));int count=0;s.OnPacket+=(o,p)=>count++;Feed(s,Packet(17,1));Check(count==1,"17-byte packet not delivered");});
  Test("fragmented packet waits and completes",()=>{var s=new Session(new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp));int count=0;s.OnPacket+=(o,p)=>count++;var p=Packet(21,2);Feed(s,p[..8]);Check(count==0,"premature delivery");Feed(s,p[8..]);Check(count==1,"missing delivery");});
  Test("coalesced packets preserve queue contents",()=>{var s=new Session(new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp));s.m_GameContext=new GameContext();var a=Packet(20,42);var b=Packet(20,99);var both=new byte[40];Array.Copy(a,both,20);Array.Copy(b,0,both,20,20);Feed(s,both);Check(s.m_GameContext.Client.RecvPacketQueue.Dequeue().Bytes[0]==42,"queue reads next packet instead of first");});
  Test("32-bit length waits for full packet",()=>{var s=new Session(new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp));int count=0;s.OnPacket+=(o,p)=>count++;var b=Packet(21,4);Array.Copy(BitConverter.GetBytes(65557),b,4);Feed(s,b);Check(count==0,"length truncated to 16 bits");});
  Test("undersized frame rejected",()=>{var s=new Session(new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp));bool disconnected=false;s.OnDisconnected+=(o,p)=>disconnected=true;var b=Packet(21,4);Array.Copy(BitConverter.GetBytes(16),b,4);Feed(s,b);Check(disconnected,"invalid packet accepted");});
  Environment.ExitCode=fails==0?0:1;
 }
}
namespace Mir4ClientEmulator.Network.Packets {
 public class Reader {public byte[] Bytes;public Reader(byte[] b){Bytes=b;} public Reader(byte[] b,int n){Bytes=new byte[n];Array.Copy(b,Bytes,n);}}
 public class Writer:IDisposable {public byte[] ToArray_Raw()=>new byte[20];public void WriteBytes(byte[] b){}public void Dispose(){}}
 public enum Opcodes {Dummy}
}
namespace Mir4ClientEmulator.Context {
 public class Client {public Queue<Reader> RecvPacketQueue=new();public Queue<Reader> SendPacketQueue=new();}
 public class FrontContext {public Client Client=new();}
 public class GameContext {public Client Client=new();}
}
namespace Mir4ClientEmulator.Network.Packets.Encryption {public static class Encryption {public static int Encode(byte[] a,byte[] b,int n){Array.Copy(a,b,n);return n;}}}

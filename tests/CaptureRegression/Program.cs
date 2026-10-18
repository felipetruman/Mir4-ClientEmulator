using System.Text.Json;
using Mir4.Game;
using Mir4ClientEmulator.Network.Packets;
using Mir4ClientEmulator.Network.Packets.Messages;

static class Program
{
    private sealed record Sample(int opcode, string name, string payload_hex);
    private sealed record FixtureFile(string source, Sample[] samples);

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Main()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "fixtures", "current-server-packets.json");
        var fixture = JsonSerializer.Deserialize<FixtureFile>(File.ReadAllText(path))
            ?? throw new InvalidOperationException("Fixture file is empty.");
        Check(fixture.samples.Length == 4, "Expected four captured server packet samples.");

        foreach (var sample in fixture.samples)
        {
            Check(Enum.IsDefined(typeof(Opcodes), sample.opcode), $"Opcode {sample.opcode} is missing from Opcodes.cs.");
            Check(Enum.GetName(typeof(Opcodes), sample.opcode) == sample.name, $"Opcode {sample.opcode} has the wrong name.");
            var payload = Convert.FromHexString(sample.payload_hex);

            switch ((Opcodes)sample.opcode)
            {
                case Opcodes.GAME_CLIENT_CHANGE_INFO:
                    var info = Protobuf.ProtoDeserialize<GAME_CLIENT_CHANGE_INFO>(payload);
                    Check(info.uid == 123456789, "Redacted player UID did not decode.");
                    Check(info.hp > 0 && info.max_hp >= info.hp, "Captured HP fields did not decode consistently.");
                    Check(info.vigor == 100 && info.type == 1, "Captured player state fields did not decode.");
                    Console.WriteLine("PASS current CHANGE_INFO packet maps to the real model");
                    break;

                case Opcodes.GAME_CLIENT_CHANGE_STAT:
                    var stat = Protobuf.ProtoDeserialize<GAME_CLIENT_CHANGE_STAT>(payload);
                    Check(stat.combatpoint == 131112, "Captured combat power did not decode.");
                    var playerStats = new Stats();
                    playerStats.ApplyChangeStat(stat);
                    Check(playerStats.CombatPoint == 131112, "Captured combat power was not stored in player state.");
                    Console.WriteLine("PASS current CHANGE_STAT packet maps to the real model");
                    break;

                case Opcodes.GAME_CLIENT_STRUCK:
                    var struck = Protobuf.ProtoDeserialize<GAME_CLIENT_STRUCK>(payload);
                    Check(struck.uid == 123456789, "Redacted struck-target UID did not decode.");
                    Check(struck.damage >= 0 && struck.hp > 0, "Captured damage or remaining HP did not decode.");
                    Console.WriteLine("PASS current STRUCK packet maps to the real model");
                    break;

                case Opcodes.GAME_CLIENT_CHATMSG:
                    var chat = Protobuf.ProtoDeserialize<GAME_CLIENT_CHATMSG>(payload);
                    Check(chat.chat_header?.uid == 123456789, "Redacted chat-header UID did not decode.");
                    Check(chat.msg == "MIR4_CAPTURE_FIXTURE" && chat.type == 3, "Captured chat packet fields did not decode.");
                    Console.WriteLine("PASS current CHATMSG packet maps to the real model");
                    break;

                default:
                    throw new InvalidOperationException($"Unexpected sample opcode {sample.opcode}.");
            }
        }
    }
}

//By AlSch092 @ Github
﻿using Mir4ClientEmulator.Network;
using Mir4ClientEmulator.Network.Packets.Messages;

namespace Mir4.Game
{
    public class Stats
    { 
        public Client MirClient { get; set; }

        public uint Vigor { get; set; }
        public uint Rage { get; set; }

        public uint MaxHP { get; set; }
        public uint CurrentHP { get; set; }

        public uint MaxMana { get; set; }
        public uint CurrentMana { get; set; }

        public int CombatPoint { get; private set; }

        public uint Experience { get; set; }

        public uint Level { get; set; }

        public _vector3 Position { get; set; }

        public void ApplyChangeStat(GAME_CLIENT_CHANGE_STAT update)
        {
            CombatPoint = update.combatpoint;
        }
    }
}

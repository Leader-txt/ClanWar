using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClanWar
{
    public class LevelUp
    {
        public int Level { get; set; }
        public int Exp { get; set; }
        public string[] RewardCmd { get; set; } = new string[] { "" };
    }
}

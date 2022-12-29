using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClanWar
{
    public class KillReward
    {
        public int Boss { get; set; }
        public string[] Reward { get; set; } = new string[] { "" };
        public bool Finished { get; set; } = false;
    }
}

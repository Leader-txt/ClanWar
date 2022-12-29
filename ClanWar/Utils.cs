using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TShockAPI;

namespace ClanWar
{
    internal class Utils
    {
        public static void ExecuteCmd(int who,string[] cmds)
        {
            var player = TShock.Players[who];
            foreach (var cmd in cmds)
            {
                Commands.HandleCommand(TSPlayer.Server, cmd.Replace("name", "\"" + player.Name + "\""));
            }
        }
        public static void DelItem(int who,int index)
        {
            var player = TShock.Players[who];
            player.TPlayer.inventory[index].netID = 0;
            player.SendData(PacketTypes.PlayerSlot, "", who, index);
        }
        public static void Drop(int who,int index)
        {
            var player=TShock.Players[who];
            var item=player.TPlayer.inventory[index];
            player.GiveItem(item.netID, item.stack, item.prefix);
            DelItem(who,index);
        }
        public static float NextRate()
        {
            return (float)(Math.Abs(ClanWar.Config.GetRateMin - ClanWar.Config.GetRateMax) * ClanWar.Random.NextDouble() + ClanWar.Config.GetRateMin);
        }
    }
}

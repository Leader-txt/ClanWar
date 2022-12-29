using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria;
using TShockAPI.DB;

namespace ClanWar
{
    public class RewardKill
    {
        public string Hunt { get; set; } = "";
        public int RewardExp { get; set; } = 0;
        public string Sender { get; set; } = "";
        public string Receiver { get; set; } = "";
        public List<Item> RewardItem { get; set; } = new List<Item>() { new Item() };
        //保存悬赏任务
        public void Save()
        {
            if(Data.GetRewardKill(Hunt) != null)
            {
                Data.db.Query($"delete from RewardKill where Hunt='{Hunt}' and Sender='{Sender}'");
            }
            Data.db.Query($"insert into RewardKill (Hunt,RewardExp,Sender,RewardItem,Receiver)values('{Hunt}',{RewardExp},'{Sender}','{JsonConvert.SerializeObject(RewardItem)}','{Receiver}')");
        }
        public void Delete()
        {
            Data.db.Query($"delete from RewardKill where Hunt='{Hunt}' and Sender='{Sender}'");
        }
    }
}

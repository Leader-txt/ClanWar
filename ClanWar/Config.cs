using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClanWar
{
    public class Config
    {
        public List<LevelLim> LevelLims { get; set; } = new List<LevelLim>() { new LevelLim() };
        /// <summary>
        /// 已击杀的boss id 列表
        /// </summary>
        public List<int> Killed { get; set; } = new List<int> { 0 };
        public KillReward[] KillRewards { get; set; } = new KillReward[] { new KillReward() };
        public LevelUp[] Levels { get; set; }=new LevelUp[] { new LevelUp() };
        public int[] NoDropIndexs { get; set; } = new int[] { 0 };
        /// <summary>
        /// 最多掉落数量
        /// </summary>
        public int MaxDrop { get; set; }
        /// <summary>
        /// 最少掉落数量
        /// </summary>
        public int MinDrop { get; set; }
        /// <summary>
        /// 夺取经验
        /// </summary>
        public float GetRateMin { get; set; }
        public float GetRateMax { get; set; }
        public int PvpLevel { get; set; } = 10;
        public int KillTimer { get; set; } = 100;
        public int[] RewardBuff { get; set; } = new int[] { 0 };
        public string ProtectArea { get; set; } = "ProtectArea";
        const string path = "tshock/ClanWar.json";
        public void Save()
        {
            using (StreamWriter wr=new StreamWriter (path))
            {
                wr.WriteLine (JsonConvert.SerializeObject(this, Formatting.Indented));
            }
        }
        public static Config GetConfig()
        {
            Config config = new Config ();
            if (!File.Exists(path))
            {
                config.Save();
                return config;
            }
            else
            {
                using (StreamReader sr=new StreamReader(path))
                {
                    config = JsonConvert.DeserializeObject<Config>(sr.ReadToEnd());
                }
                return config;
            }
        }
    }
}

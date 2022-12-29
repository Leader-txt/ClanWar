using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TShockAPI;
using Terraria;
using TerrariaApi.Server;
using System.Timers;
using Microsoft.Xna.Framework;
using System.Diagnostics;
using System.Threading.Tasks;
using OTAPI;
using TShockAPI.DB;

namespace ClanWar
{
    [ApiVersion(2, 1)]
    public class ClanWar : TerrariaPlugin
    {
        /// <summary>
        /// Gets the author(s) of this plugin
        /// </summary>
        public override string Author => "Leader";

        /// <summary>
        /// Gets the description of this plugin.
        /// A short, one lined description that tells people what your plugin does.
        /// </summary>
        public override string Description => "公会战";

        /// <summary>
        /// Gets the name of this plugin.
        /// </summary>
        public override string Name => "ClanWar";

        /// <summary>
        /// Gets the version of this plugin.
        /// </summary>
        public override Version Version => new Version(1, 0, 0, 0);

        #region GlobalValues

        public static Dictionary<int, Stopwatch> KillTimer = new Dictionary<int, Stopwatch>();

        public static List<Clan> clans;

        public static Timer invitebc;

        public static Config Config;

        public static Dictionary<int, List<RewardKill>> RewardKill { get; set; } = new Dictionary<int, List<RewardKill>>();

        public static Random Random = new Random();

        #endregion
        /// <summary>
        /// Initializes a new instance of the ClanWar class.
        /// This is where you set the plugin's order and perfrom other constructor logic
        /// </summary>
        public ClanWar(Main game) : base(game)
        {

        }

        /// <summary>
        /// Handles plugin initialization. 
        /// Fired when the server is started and the plugin is being loaded.
        /// You may register hooks, perform loading procedures etc here.
        /// </summary>
        public override void Initialize()
        {

            clans = new List<Clan>();

            ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
            ServerApi.Hooks.ServerChat.Register(this, onChat);
            GetDataHandlers.TogglePvp.Register(OnPvp);
            GetDataHandlers.PlayerDamage.Register(OnPlayerDamage);
            GetDataHandlers.KillMe.Register(OnKillMe);
            ServerApi.Hooks.NpcKilled.Register(this, OnNPCKilled);
            ServerApi.Hooks.ServerLeave.Register(this, OnServerLeave);
            ServerApi.Hooks.ServerJoin.Register(this, OnServerJoin);
            ServerApi.Hooks.NetGreetPlayer.Register(this, OnNetGreetPlayer);
            GetDataHandlers.PlayerUpdate.Register(OnPlayerUpdate);
        }

        private void OnPlayerUpdate(object sender, GetDataHandlers.PlayerUpdateEventArgs e)
        {
            if(TShock.Regions.InAreaRegionName(e.Player.TileX, e.Player.TileY).Contains(Config.ProtectArea))
            {
                e.Player.SetPvP(false);
            }
            else
            {
                var user = Data.GetClanUser(e.Player.Name);
                if (user != null&&user.Level >=Config.PvpLevel)
                {
                    e.Player.SetPvP(true);
                }
            }
        }

        private void OnNetGreetPlayer(GreetPlayerEventArgs args)
        {
            var user = Data.GetClanUser(TShock.Players[args.Who].Name);
            var player = TShock.Players[args.Who];
            if(player != null && player.Active)
            {
                if (user == null)
                {
                    user = new ClanUser() { Name = player.Name, ID = player.Account.ID };
                    user.Save();
                }
                if (user != null && user.Level >= Config.PvpLevel)
                {
                    TShock.Players[args.Who].SetPvP(true);
                }
            }
        }

        private void OnServerJoin(JoinEventArgs args)
        {
            var user = Data.GetClanUser(TShock.Players[args.Who].Name);
            var player = TShock.Players[args.Who];
            if(user != null&&user.Level >=Config.PvpLevel)
            {
                TShock.Players[args.Who].SetPvP(true);
            }
            if(!RewardKill.ContainsKey(args.Who))
            {
                var list = Data.GetRewardKills(player.Name);
                if (list.Count() > 0)
                {
                    RewardKill.Add(args.Who, list.ToList());
                }
            }
        }

        private void OnServerLeave(LeaveEventArgs args)
        {
            if (RewardKill.ContainsKey(args.Who))
            {
                RewardKill.Remove(args.Who);
            }
        }

        private void OnNPCKilled(NpcKilledEventArgs args)
        {
            {
                if (!Config.Killed.Contains(args.npc.netID))
                {
                    Config.Killed.Add(args.npc.netID);
                    Config.Save();
                }
            }
            foreach (var i in Config.KillRewards)
            {
                if (i.Boss == args.npc.netID && !i.Finished)
                {
                    var index = findClan(TShock.Players[args.npc.lastInteraction].Account.ID);
                    if(index == -1)
                    {
                        Utils.ExecuteCmd(args.npc.lastInteraction, i.Reward);
                        TShock.Utils.Broadcast($"玩家:{TShock.Players[args.npc.lastInteraction].Name}首次击杀{Lang.GetNPCNameValue(args.npc.netID)}!",Color.Red);
                    }
                    else
                    {
                        foreach (var p in TShock.Players)
                        {
                            if(p!=null && p.Active&&findClan(p.Account.ID) ==index)
                            {
                                Utils.ExecuteCmd(p.Index, i.Reward);
                            }
                        }
                        TShock.Utils.Broadcast($"公会:{clans[index].name}首次击杀{Lang.GetNPCNameValue(args.npc.netID)}!", Color.Red);
                    }
                    i.Finished = true;
                    Config.Save();
                }
            }
        }

        private void OnKillMe(object sender, GetDataHandlers.KillMeEventArgs e)
        {
            //死亡随机掉落
            try
            {
                if(e.Player.Group.Name == TShock.Config.Settings.DefaultGuestGroupName)
                {
                    return;
                }
                List<int> indexs = new List<int>();
                for(int i = 0; i < e.Player.TPlayer.inventory.Count(); i++)
                {
                    if (!Config.NoDropIndexs.Contains(i))
                    {
                        if (!(new int[] { 0, Terraria.ID.ItemID.CopperCoin , Terraria.ID.ItemID.SilverCoin, Terraria.ID.ItemID.GoldCoin, Terraria.ID.ItemID.PlatinumCoin})
                            .Contains( e.Player.TPlayer.inventory[i].netID) && e.Player.TPlayer.inventory[i].stack != 0)
                            indexs.Add(i);
                    }
                }
                for (int i = 0; i < Random.Next(Config.MinDrop, Config.MaxDrop + 1) && indexs.Count > 0; i++)
                {
                    var index = Random.Next(indexs.Count);
                    Utils.Drop(e.Player.Index, indexs[index]);
                    indexs.RemoveAt(index);
                }
            }
            catch (Exception ex)
            {

            }
            if (e.Pvp)
            {
                var hit = TShock.Players[e.PlayerDeathReason._sourcePlayerIndex];

                if (hit.Name == e.Player.Name)
                    return;
                {
                    var area = TShock.Regions.InAreaRegionName(e.Player.TileX, e.Player.TileY);
                    hit.SendData(PacketTypes.PlayerUpdate, "", e.Player.Index);
                    hit.SendData(PacketTypes.PlayerHp, "", e.Player.Index);
                    if (area.Contains(Config.ProtectArea))
                    {
                        e.Handled = true;
                        hit.SendErrorMessage("保护区内禁止pvp！");
                        //e.Player.TPlayer.Spawn_IsAreaAValidWorldSpawn(e.Player.TileX, e.Player.TileY);
                        return;
                    }
                }
                //夺取经验
                try
                {
                    var user = Data.GetClanUser(e.Player.Name);
                    if (user != null&&hit.Name !=e.Player.Name)
                    {
                        var rate = Utils.NextRate();
                        var hut = Data.GetClanUser(hit.Name);
                        if (hut == null)
                        {
                            hut = new ClanUser() { Name = hit.Name, ID = hit.Account.ID, Level = 0, Exp = 0 };
                        }
                        int exp = (int)(user.Exp * rate);
                        hut.Exp += exp;
                        user.Exp -= exp;
                        user.Save();
                        hut.Save();
                        hit.SendSuccessMessage($"击杀{e.Player.Name},获得经验:{exp}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
                {
                    //悬赏
                    if (RewardKill.ContainsKey(hit.Index))
                    {
                        for (int i = 0; i < RewardKill[hit.Index].Count(); i++)
                        {
                            var item = RewardKill[hit.Index][i];
                            if (item.Hunt == e.Player.Name)
                            {
                                hit.SendSuccessMessage($"悬赏任务:击杀{e.Player.Name}已完成！");
                                var user = Data.GetClanUser(hit.Name);
                                if (user != null)
                                {
                                    user.Exp += item.RewardExp;
                                }
                                else
                                {
                                    user = new ClanUser() { Name = hit.Name, ID = hit.Account.ID, Exp = item.RewardExp, Level = 0 };
                                }
                                user.Save();
                                foreach (var ite in item.RewardItem)
                                {
                                    hit.GiveItem(ite.netID, ite.stack, ite.prefix);
                                }
                                item.Delete();
                                RewardKill[hit.Index].RemoveAt(i);
                            }
                        }
                    }
                    //连杀奖励
                    try
                    {
                        if (KillTimer[hit.Index].ElapsedMilliseconds / 1000 < Config.KillTimer)
                        {
                            foreach (var b in Config.RewardBuff)
                            {
                                hit.SetBuff(b);
                            }
                        }
                        KillTimer[hit.Index].Restart();
                    }
                    catch
                    {
                        KillTimer.Add(hit.Index, new Stopwatch());
                        KillTimer[hit.Index].Start();
                    }
                }
            }
            //e.Player.Spawn(PlayerSpawnContext.SpawningIntoWorld);
        }

        private void OnPlayerDamage(object sender, GetDataHandlers.PlayerDamageEventArgs e)
        {
            if (e.PVP)
            {
                try
                {
                    TSPlayer hitted = TShock.Players[e.ID];
                    if (hitted.Name == e.Player.Name)
                        return;
                    //如果是同公会成员则不允许pvp
                    if (findClan(hitted.Account.ID) == findClan(e.Player.Account.ID) && findClan(e.Player.Account.ID) != -1)
                    {
                        e.Handled = true;
                        e.Player.SendErrorMessage("公会成员间禁止pvp！");
                        return;
                    }
                    //保护区内禁止pvp
                    //TShock.Utils.Broadcast($"hit:{hit.Name} hittrd:{hitted.Name} ply:{e.Player.Name}",Color.Red );
                    var area = TShock.Regions.InAreaRegionName(hitted.TileX,hitted.TileY);
                    foreach (var s in area)
                    {
                        e.Player.SendInfoMessage(s);
                    }
                    //e.Player.SendData(PacketTypes.PlayerUpdate, "", e.Player.Index);
                    //e.Player.SendData(PacketTypes.PlayerHp, "", e.Player.Index);
                    if (area.Contains(Config.ProtectArea))
                    {
                        e.Handled = true;
                        e.Player.SendErrorMessage("保护区内禁止pvp！");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    //Console.WriteLine(ex);
                }
            }
        }

        private void OnPvp(object sender, GetDataHandlers.TogglePvpEventArgs e)
        {
            var user = Data.GetClanUser(e.Player.Name);
            if (user == null) return;
            //如果用户没到等级保护则不允许启用
            if (user.Level < Config.PvpLevel)
            {
                e.Handled = true;
                e.Player.SetPvP(false);
                e.Player.SendErrorMessage($"Pvp等级保护lv{Config.PvpLevel}，禁止启用pvp！");
            }
            else if (!e.Pvp)
            {
                e.Handled = true;
                e.Player.SetPvP(true);
                e.Player.SendErrorMessage("禁止关闭pvp！");
            }
            //否则强制启用

        }

        /// <summary>
        /// Handles plugin disposal logic.
        /// *Supposed* to fire when the server shuts down.
        /// You should deregister hooks and free all resources here.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Deregister hooks here
            }
            base.Dispose(disposing);
        }
        #region Hooks
        private void OnInitialize(EventArgs args)
        {
            Config = Config.GetConfig();
            Data.DBConnect();
            Data.loadClans();

            invitebc = new Timer(300000) { AutoReset = true, Enabled = true }; //5 min
            invitebc.Elapsed += onUpdate;


            Commands.ChatCommands.Add(new Command("clanwar.use", ClansMain, "clan"));
            Commands.ChatCommands.Add(new Command("clanwar.use", CChat, "c"));
            Commands.ChatCommands.Add(new Command("clanwar.reload", CReload, "clanreload"));
            Commands.ChatCommands.Add(new Command("clanwar.mod", ClansStaff, "clanstaff", "cs"));
            Commands.ChatCommands.Add(new Command("clanwar.use", rk, "悬赏"));
            Commands.ChatCommands.Add(new Command("clanwar.admin", admin, "magclan"));
            Commands.ChatCommands.Add(new Command("clanwar.use", cw, "clanwar","cw"));

            //Commands.ChatCommands.Add(new Command("", test, "test"));
        }

        private void test(CommandArgs args)
        {
            Utils.ExecuteCmd(args.Player.Index,new string[] { "/give 1 name 1" } );
        }

        private void cw(CommandArgs args)
        {
            if(args.Parameters.Count == 0)
            {
                args.Player.SendInfoMessage("/clanwar 升级");
                args.Player.SendInfoMessage("/clanwar 转账 用户名 经验，将经验转账到指定用户");
                args.Player.SendInfoMessage("/clanwar 查询 [用户名],查询相关信息");
                return;
            }
            switch (args.Parameters[0])
            {
                case "转账":
                    {
                        int exp = int.Parse(args.Parameters[2]);
                        var user = Data.GetClanUser(args.Player.Name);
                        if(user ==null ||user.Exp<exp)
                        {
                            args.Player.SendErrorMessage("转账经验不足");
                            break;
                        }
                        var pay = Data.GetClanUser(args.Parameters[1]);
                        if (pay == null)
                        {
                            pay = new ClanUser() { Name = args.Parameters[0] };
                        }
                        pay.Exp += exp;
                        pay.Save();
                        user.Exp -= exp;
                        user.Save();
                        args.Player.SendSuccessMessage("转账成功！");
                    }
                    break;
                case "查询":
                    {
                        var user = Data.GetClanUser(args.Player.Name);
                        if (user == null)
                        {
                            args.Player.SendErrorMessage("查无此人！");
                            return;
                        }
                        if(args.Parameters.Count == 2)
                        {
                            user = Data.GetClanUser(args.Parameters[1]);
                        }
                        args.Player.SendInfoMessage($"{user.Name} lv{user.Level} Exp:{user.Exp}");
                    }
                    break;
                case "升级":
                    {
                        var user = Data.GetClanUser(args.Player.Name);
                        if(user ==null)
                        {
                            args.Player.SendErrorMessage("经验不足，无法升级！");
                            return;
                        }
                        var levelup = Config.Levels.ToList().Find((LevelUp lp) => lp.Level == user.Level + 1);
                        if (levelup == null)
                        {
                            args.Player.SendSuccessMessage("您已满级，无需升级！");
                            return;
                        }
                        else
                        {
                            if(levelup.Exp >user.Exp)
                            {
                                args.Player.SendErrorMessage($"经验不足！升级至lv{levelup.Level}还需{levelup.Exp - user.Exp }点经验");
                                return;
                            }
                            var levellimit = Config.LevelLims.Find((LevelLim lm) => lm.Level == user.Level + 1 && !Config.Killed.Contains(lm.BossID));
                            if (levellimit != null)
                            {
                                args.Player.SendSuccessMessage("您在此阶段已满级，无需升级！");
                                return;
                            }
                            user.Exp -= levelup.Exp;
                            user.Level++;
                            user.Save();
                            Utils.ExecuteCmd(args.Player.Index, levelup.RewardCmd);
                             if (user.Level >= Config.PvpLevel)
                                args.Player.SetPvP(true);
                            args.Player.SendSuccessMessage($"您已成功升级至lv{user.Level}");
                        }
                    }
                    break;
            }
        }

        private void admin(CommandArgs args)
        {
            if(args.Parameters.Count == 0)
            {
                args.Player.SendInfoMessage("/magclan edit 用户名 等级 经验，修改用户属性");
                args.Player.SendInfoMessage("/magclan addexp 用户名 经验值");
                args.Player.SendInfoMessage("/magclan del 用户名，删除用户");
                args.Player.SendInfoMessage("/magclan init,重置");
                return;
            }
            switch (args.Parameters[0])
            {
                case "addexp":
                    {
                        var user = Data.GetClanUser(args.Parameters[1]);
                        user.Exp += int.Parse(args.Parameters[2]);
                        user.Save();
                        args.Player.SendSuccessMessage("经验值添加成功！");
                    }
                    break;
                case "init":
                    {
                        foreach (var i in Config.KillRewards)
                        {
                            i.Finished = false;
                        }
                        Config.Killed.Clear();
                        Config.Save();
                        Data.db.Query("delete from ClanUser,RewardKill");
                        args.Player.SendSuccessMessage("用户数据和悬赏数据已清空！");
                    }
                    break;
                case "del":
                    {
                        var user = Data.GetClanUser(args.Parameters[1]);
                        user.Del();
                        args.Player.SendSuccessMessage("删除成功！");
                    }
                    break;
                case "edit":
                    {
                        var user = Data.GetClanUser(args.Parameters[1]);
                        user.Level =int.Parse(args.Parameters[2]);
                        user.Exp = int.Parse(args.Parameters[3]);
                        user.Save();
                        args.Player.SendSuccessMessage("用户属性修改成功！");
                    }
                    break;
            }
        }

        private void rk(CommandArgs args)
        {
            if(args.Parameters.Count == 0)
            {
                args.Player.SendInfoMessage("/悬赏 发布 被悬赏者名称 经验 经验数目");
                args.Player.SendInfoMessage("/悬赏 发布 被悬赏者名称 物品 数量");
                args.Player.SendInfoMessage("/悬赏 列表，悬赏列表");
                args.Player.SendInfoMessage("/悬赏 接受 悬赏id（可在列表中获取）");
                args.Player.SendInfoMessage("/悬赏 取消 悬赏id，取消悬赏任务");
                return;
            }
            try
            {
                switch (args.Parameters[0])
                {
                    case "接受":
                        {
                            var rk = Data.GetRewardKill()[int.Parse(args.Parameters[1])];
                            if (rk.Receiver != string.Empty)
                            {
                                args.Player.SendErrorMessage($"该悬赏任务已被{rk.Receiver}接受");
                                return;
                            }
                            try
                            {
                                RewardKill[args.Player.Index].Add(rk);
                            }
                            catch
                            {
                                RewardKill.Add(args.Player.Index, new List<RewardKill> { rk });
                            }
                            rk.Receiver = args.Player.Name;
                            rk.Save();
                            args.Player.SendSuccessMessage("接受悬赏任务成功！");
                        }
                        break;
                    case "取消":
                        {
                            var list = Data.GetRewardKill();
                            var kill = list[int.Parse(args.Parameters[1])];
                            kill.Delete();
                            if (kill.RewardExp > 0)
                            {
                                var user = Data.GetClanUser(args.Player.Name);
                                user.Exp +=kill.RewardExp;
                                user.Save();
                            }
                            foreach (var i in kill.RewardItem)
                            {
                                args.Player.GiveItem(i.netID, i.stack, i.prefix);
                            }
                            args.Player.SendSuccessMessage("取消成功！");
                        }
                        break;
                    case "列表":
                        {
                            var list = Data.GetRewardKill();
                            for (int i = 0; i < list.Count(); i++)
                            {
                                var kill = list[i];
                                args.Player.SendInfoMessage($"ID:{i}\t被悬赏者:{kill.Hunt}\t发起者:{kill.Sender}\t奖励经验:{kill.RewardExp}\t奖励物品：");
                                foreach (var j in kill.RewardItem)
                                {
                                    args.Player.SendInfoMessage($"[i:{j.netID}]x{j.stack}");
                                }
                            }
                        }
                        return;
                    case "发布":
                        {
                            if(Data.GetRewardKill().ToList().Find((RewardKill rk)=>rk.Sender==args.Player.Name &&rk.Hunt == args.Parameters[1]) != null)
                            {
                                args.Player.SendErrorMessage("已有该玩家的悬赏项目！");
                                return;
                            }
                            if (args.Parameters[2] == "经验")
                            {
                                int exp = int.Parse(args.Parameters[3]);
                                var user = Data.GetClanUser(args.Player.Name);
                                if (user == null || (user.Exp < exp))
                                {
                                    args.Player.SendErrorMessage("经验不足！");
                                    return;
                                }
                                user.Exp -= exp;
                                user.Save();
                                new RewardKill() { Hunt = args.Parameters[1], Sender = args.Player.Name, RewardExp = exp, RewardItem = null }.Save();
                                args.Player.SendSuccessMessage("发布成功！");
                            }
                            else if (args.Parameters[2] == "物品")
                            {
                                int count=int.Parse(args.Parameters[3]);
                                args.Player.SendInfoMessage("请将想要添加的物品放至垃圾格");
                                new Task(() =>
                                {
                                    var items = new List<Item>();
                                    while (items.Count < count)//(args.Player.TPlayer.trashItem.netID==0 || args.Player.TPlayer.trashItem.prefix != 255)
                                    {
                                        var item = args.Player.TPlayer.trashItem;
                                        if (item.netID != 0)
                                        {
                                            items.Add(new Item() { netID = item.netID, stack = item.stack, prefix = item.prefix });
                                            args.Player.SendInfoMessage($"{Lang.GetItemName(item.netID)}已添加");
                                            item.netID = 0;
                                            args.Player.SendData(PacketTypes.PlayerSlot, "", args.Player.Index, 179);
                                        }
                                    }
                                    args.Player.SendInfoMessage("物品添加结束");
                                    args.Player.TPlayer.trashItem.netID = 0;
                                    args.Player.SendData(PacketTypes.PlayerSlot, "", args.Player.Index, 179);
                                    new RewardKill() { Hunt = args.Parameters[1], Sender = args.Player.Name, RewardExp = 0, RewardItem = items }.Save();
                                    args.Player.SendSuccessMessage("发布成功！");
                                }).Start();
                            }
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            
        }

        private void onUpdate(object sender, ElapsedEventArgs e)
        {
            foreach (Clan clan in clans)
            {
                if (clan.invited.Count > 0)
                {
                    foreach (int Accountid in clan.invited)
                    {
                        string name = TShock.UserAccounts.GetUserAccountByID(Accountid).Name;
                        List<TSPlayer> matches = TSPlayer.FindByNameOrID(name);
                        if (matches.Count > 0)
                        {
                            foreach (TSPlayer plr in matches)
                            {
                                if (plr.Account.ID == Accountid)
                                {
                                    plr.SendInfoMessage($"You have been invited to the {clan.name} clan! Use '/clan accept' to join the clan or '/clan deny' to reject the invitation.");
                                }
                            }
                        }
                    }
                }
            }
        }

        private void onChat(ServerChatEventArgs args)
        {
            TSPlayer plr = TShock.Players[args.Who];

            if (plr == null || !plr.Active || args.Handled || !plr.IsLoggedIn || args.Text.StartsWith(TShock.Config.Settings.CommandSpecifier) || args.Text.StartsWith(TShock.Config.Settings.CommandSilentSpecifier) || findClan(plr.Account.ID) == -1 || plr.mute)
            {
                return;
            }

            int clanindex = findClan(plr.Account.ID);
            var user = Data.GetClanUser(TShock.Players[args.Who].Name);
            string prefix = (user==null?"":$"[lv{user.Level} Exp:{user.Exp}]")+ clans[clanindex].prefix == "" ? plr.Group.Prefix : "(" + clans[clanindex].prefix + ") " + plr.Group.Prefix;

            TSPlayer.All.SendMessage(string.Format(TShock.Config.Settings.ChatFormat, plr.Group.Name, prefix, plr.Name, plr.Group.Suffix, args.Text), new Color(plr.Group.R, plr.Group.G, plr.Group.B));
            TSPlayer.Server.SendMessage(string.Format(TShock.Config.Settings.ChatFormat, plr.Group.Name, prefix, plr.Name, plr.Group.Suffix, args.Text), new Color(plr.Group.R, plr.Group.G, plr.Group.B));

            args.Handled = true;
        }
        #endregion

        #region Clan Cmds
        private void ClansMain(CommandArgs args)
        {
            int clanindex = findClan(args.Player.Account.ID);
            #region clan help
            if (args.Parameters.Count > 0 && args.Parameters[0].ToLower() == "help")
            {
                List<string> cmds = new List<string>();

                if (clanindex != -1 && clans[clanindex].owner == args.Player.Account.ID)
                {
                    cmds.Add("prefix <chat prefix> - Add or change your clan's chat prefix.");
                    cmds.Add("promote <player name> - Make a clanmember a clan admin.");
                    cmds.Add("demote <player name> - Make a clan admin a regular clanmember.");
                    cmds.Add("private - Toggles your clan privacy; outside players cannot join your clan without an invite if your clan is set to Private.");
                }
                if (clanindex != -1 && (clans[clanindex].admins.Contains(args.Player.Account.ID) || clans[clanindex].owner == args.Player.Account.ID))
                {
                    cmds.Add("kick <player name> - Kick a clanmember from your clan.");
                    cmds.Add("ban <player name> - Prevents a player from joining your clan.");
                    cmds.Add("unban <player name> - Allows a banned player to join your clan.");
                }
                if (clanindex != -1 && (clans[clanindex].members.Contains(args.Player.Account.ID) || clans[clanindex].admins.Contains(args.Player.Account.ID) || clans[clanindex].owner == args.Player.Account.ID))
                {
                    if (!(clans[clanindex].cprivate && clans[clanindex].members.Contains(args.Player.Account.ID)))
                        cmds.Add("invite <player name> - Sends a message to a player inviting them to join your clan.");
                    cmds.Add("members - Lists all clanmembers in your clan.");
                    cmds.Add("leave - Leaves your current clan.");
                }
                if (clanindex == -1)
                {
                    cmds.Add("create <clan name> - Creates a new clan.");
                    cmds.Add("join <clan name> - Joins an existing clan.");
                }

                cmds.Add("list - Lists all existing clans.");
                cmds.Add("check <player name> - Checks to see which clan the specified player is in.");

                int pagenumber;

                if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out pagenumber))
                    return;

                PaginationTools.SendPage(args.Player, pagenumber, cmds, new PaginationTools.Settings
                {
                    HeaderFormat = "Clan Sub-Commands ({0}/{1}):",
                    FooterFormat = "Type {0}clan help {{0}} for more sub-commands.".SFormat(args.Silent ? TShock.Config.Settings.CommandSilentSpecifier : TShock.Config.Settings.CommandSpecifier)
                }
                );

            }
            #endregion
            #region clan leave
            else if (args.Parameters.Count == 1 && args.Parameters[0].ToLower() == "leave")
            {
                //If player is in a Clan
                if (clanindex != -1)
                {
                    //If player is owner of the clan, pass ownership if possible
                    if (clans[clanindex].owner == args.Player.Account.ID)
                    {
                        if (clans[clanindex].admins.Count > 0)
                        {
                            clans[clanindex].owner = clans[clanindex].admins[0];
                            clans[clanindex].admins.RemoveAt(0);
                            Data.changeOwner(args.Player.Account.ID, clans[clanindex]);
                            string newowner = TShock.UserAccounts.GetUserAccountByID(clans[clanindex].owner).Name;
                            args.Player.SendSuccessMessage($"You have left your clan! Ownership of the clan has now passed to {newowner}");
                            var list = TSPlayer.FindByNameOrID(newowner);
                            if (list.Count == 1 && list[0].Account.ID == clans[clanindex].owner)
                                list[0].SendInfoMessage($"You are now owner of the {clans[clanindex].name} clan!");
                            TShock.Log.Info($"{args.Player.Account.Name} left the {clans[clanindex].name} clan.");
                            TShock.Log.Info($"{newowner} is now the owner of the {clans[clanindex].name} clan.");
                        }
                        else if (clans[clanindex].members.Count > 0)
                        {
                            clans[clanindex].owner = clans[clanindex].members[0];
                            clans[clanindex].members.RemoveAt(0);
                            Data.changeOwner(args.Player.Account.ID, clans[clanindex]);
                            string newowner = TShock.UserAccounts.GetUserAccountByID(clans[clanindex].owner).Name;
                            args.Player.SendSuccessMessage($"You have left your clan! Ownership of the clan has now passed to {newowner}");
                            var list = TSPlayer.FindByNameOrID(newowner);
                            if (list.Count == 1 && list[0].Account.ID == clans[clanindex].owner)
                                list[0].SendInfoMessage($"You are now owner of the {clans[clanindex].name} clan!");
                            TShock.Log.Info($"{args.Player.Account.Name} left the {clans[clanindex].name} clan.");
                            TShock.Log.Info($"{newowner} is now the owner of the {clans[clanindex].name} clan.");
                        }
                        else
                        {
                            Data.removeClan(clans[clanindex].owner);
                            TShock.Log.Info($"{args.Player.Account.Name} left the {clans[clanindex].name} clan.");
                            TShock.Log.Info($"The {clans[clanindex].name} clan has been deleted.");
                            clans.RemoveAt(clanindex);
                            args.Player.SendSuccessMessage("You have left your clan! There are no members in it, so it has been removed.");
                        }
                    }
                    //If player is not owner of the clan
                    else
                    {
                        //If player is admin
                        if (clans[clanindex].admins.Contains(args.Player.Account.ID))
                        {
                            clans[clanindex].admins.Remove(args.Player.Account.ID);
                            Data.changeMembers(clans[clanindex].owner, clans[clanindex]);
                            args.Player.SendSuccessMessage("You have left your clan.");
                        }
                        //If player is not admin
                        else
                        {
                            clans[clanindex].members.Remove(args.Player.Account.ID);
                            Data.changeMembers(clans[clanindex].owner, clans[clanindex]);
                            args.Player.SendSuccessMessage("You have left your clan.");
                        }
                        TShock.Log.Info($"{args.Player.Account.Name} left the {clans[clanindex].name} clan.");
                    }
                }
                //If player is not in a clan
                else
                {
                    args.Player.SendErrorMessage("You are not in a clan!");
                }
            }
            #endregion
            #region clan list
            else if (args.Parameters.Count > 0 && args.Parameters[0].ToLower() == "list")
            {
                int pagenumber;
                if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out pagenumber))
                    return;

                Dictionary<string, int> cdict = new Dictionary<string, int>();

                foreach (Clan clan in clans)
                {
                    int count = 1 + clan.admins.Count + clan.members.Count;
                    if (!clan.banned.Contains(args.Player.Account.ID) && !clan.cprivate)
                        cdict.Add(clan.name, count);
                }

                var sorteddict = from entry in cdict orderby entry.Value descending select entry.Key;

                PaginationTools.SendPage(args.Player, pagenumber, sorteddict.ToList(), new PaginationTools.Settings
                {
                    HeaderFormat = "List of Clans ({0}/{1}):",
                    FooterFormat = "Type {0}clan list {{0}} for more clans.".SFormat(args.Silent ? TShock.Config.Settings.CommandSilentSpecifier : TShock.Config.Settings.CommandSpecifier)
                });
            }
            #endregion
            #region clan members
            else if (args.Parameters.Count > 0 && args.Parameters[0].ToLower() == "members")
            {
                if (clanindex == -1)
                {
                    args.Player.SendErrorMessage("You are not in a Clan!");
                }
                else
                {
                    int pagenumber;
                    if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out pagenumber))
                        return;

                    List<string> members = new List<string>();

                    try
                    {
                        members.Add(TShock.UserAccounts.GetUserAccountByID(clans[clanindex].owner).Name + " (Owner)");
                    }
                    catch { }
                    foreach (int Accountid in clans[clanindex].admins)
                        members.Add(TShock.UserAccounts.GetUserAccountByID(Accountid).Name + " (Admin)");
                    foreach (int Accountid in clans[clanindex].members)
                        members.Add(TShock.UserAccounts.GetUserAccountByID(Accountid).Name);
                    foreach (int Accountid in clans[clanindex].invited)
                        members.Add(TShock.UserAccounts.GetUserAccountByID(Accountid).Name + " (Invited)");

                    PaginationTools.SendPage(args.Player, pagenumber, members,
                        new PaginationTools.Settings
                        {
                            HeaderFormat = clans[clanindex].name + " Clan Members ({0}/{1}):",
                            FooterFormat = "Type {0}clan members {{0}} for more clan members.".SFormat(args.Silent ? TShock.Config.Settings.CommandSilentSpecifier : TShock.Config.Settings.CommandSpecifier)
                        }
                    );
                }
            }
            #endregion
            #region clan private
            else if (args.Parameters.Count == 1 && args.Parameters[0].ToLower() == "private")
            {
                if (clanindex == -1)
                {
                    args.Player.SendErrorMessage("You are not in a clan!");
                }
                else if (clans[clanindex].owner != args.Player.Account.ID)
                {
                    args.Player.SendErrorMessage("Only clan owners can set the clan privacy setting!");
                }
                else
                {
                    clans[clanindex].cprivate = !clans[clanindex].cprivate;
                    Data.changePrivate(clans[clanindex].owner, clans[clanindex].cprivate);
                    TShock.Log.Info($"{args.Player.Account.Name} changed the {clans[clanindex].name} clan's privacy setting to " + (clans[clanindex].cprivate ? "private." : "public."));
                    args.Player.SendSuccessMessage("Successfully set your clan to " + (clans[clanindex].cprivate ? "private." : "public."));
                }
            }
            #endregion
            #region clan accept/deny
            else if (args.Parameters.Count == 1 && args.Parameters[0].ToLower() == "accept")
            {
                if (clanindex != -1)
                {
                    args.Player.SendErrorMessage("You are already in a clan!");
                }
                else
                {
                    int iclanindex = getInvite(args.Player.Account.ID);
                    if (iclanindex == -1)
                    {
                        args.Player.SendErrorMessage("You have not been invited to join a clan!");
                    }
                    else
                    {
                        clans[iclanindex].invited.Remove(args.Player.Account.ID);
                        clans[iclanindex].members.Add(args.Player.Account.ID);
                        foreach (TSPlayer plr in TShock.Players)
                        {
                            if (plr != null && plr.Active && plr.IsLoggedIn)
                            {
                                if (findClan(plr.Account.ID) == iclanindex)
                                {
                                    plr.SendInfoMessage($"{args.Player.Name} just joined the {clans[iclanindex].name} clan!");
                                }
                            }
                        }
                        TShock.Log.Info($"{args.Player.Account.Name} accepted the invite to join the {clans[iclanindex].name} clan.");
                        Data.changeMembers(clans[iclanindex].owner, clans[iclanindex]);
                    }
                }
            }
            else if (args.Parameters.Count == 1 && args.Parameters[0].ToLower() == "deny")
            {
                if (clanindex != -1)
                {
                    args.Player.SendErrorMessage("You are already in a clan!");
                }
                else
                {
                    int iclanindex = getInvite(args.Player.Account.ID);
                    if (iclanindex == -1)
                        args.Player.SendErrorMessage("You have not been invited to join a clan!");
                    else
                    {
                        clans[iclanindex].invited.Remove(args.Player.Account.ID);
                        args.Player.SendSuccessMessage("Denied your clan invitation.");
                    }
                }
            }
            #endregion
            else if (args.Parameters.Count > 1)
            {
                string type = args.Parameters[0].ToLower();

                var tempparams = args.Parameters;
                tempparams.RemoveAt(0);

                string input = string.Join(" ", tempparams);
                //Clan Create
                #region clan create
                if (type == "create")
                {
                    if (clanindex != -1)
                    {
                        args.Player.SendErrorMessage("You cannot create a clan while you are in one!");
                        return;
                    }
                    List<int> clanlist = findClanByName(input);
                    if (clanlist.Count > 0)
                    {
                        foreach (int index in clanlist)
                        {
                            if (clans[index].name == input)
                            {
                                args.Player.SendErrorMessage("A clan with this name has already been created!");
                                return;
                            }
                        }
                    }
                    if (input.Contains("[c/") || input.Contains("[i"))
                    {
                        args.Player.SendErrorMessage("You cannot use item/color tags in clan names!");
                        return;
                    }
                    clans.Add(new Clan(input, args.Player.Account.ID));
                    Data.newClan(input, args.Player.Account.ID);
                    args.Player.SendSuccessMessage($"You have created the {input} clan! Use /clan prefix <prefix> to set the chat prefix.");
                    TShock.Log.Info($"{args.Player.Account.Name} created the {input} clan.");
                    return;
                }
                #endregion
                #region clan check
                if (type == "check")
                {
                    var plist = TSPlayer.FindByNameOrID(input);
                    if (plist.Count == 1)
                    {
                        TSPlayer plr = plist[0];

                        int index = findClan(plr.Account.ID);

                        if (findClan(plr.Account.ID) != -1)
                            args.Player.SendInfoMessage($"{plr.Name} is in the {clans[index].name} clan!");
                        else
                            args.Player.SendInfoMessage($"{plr.Name} is not in a clan!");

                        return;
                    }
                    else if (plist.Count > 1)
                    {
                        args.Player.SendErrorMessage("有多个匹配项！");
                        //TShock.Utils.SendMultipleMatchError(args.Player, plist.Select(p => p.Name));
                        return;
                    }
                    else
                    {
                        var plr = TShock.UserAccounts.GetUserAccountByName(input);

                        if (plr != null)
                        {
                            int index = findClan(plr.ID);

                            if (index == -1)
                                args.Player.SendInfoMessage($"{plr.Name} is not in a clan!");
                            else
                                args.Player.SendInfoMessage($"{plr.Name} is in the {clans[index].name} clan!");
                        }
                        else
                        {
                            args.Player.SendErrorMessage($"Player not found: {plr.Name}");
                        }
                    }
                    return;
                }
                #endregion
                #region clan prefix
                //Clan Prefix
                if (type == "prefix")
                {
                    if (clanindex == -1)
                    {
                        args.Player.SendErrorMessage("You are not in a clan!");
                        return;
                    }
                    else if (clans[clanindex].owner != args.Player.Account.ID)
                    {
                        args.Player.SendErrorMessage("Only the clan's creator can change its chat prefix.");
                        return;
                    }

                    else if (input.Contains("[c/") || input.Contains("[i"))
                    {
                        args.Player.SendErrorMessage("You cannot use item/color tags in clan prefixes!");
                        return;
                    }
                    else if (input.Length > 20)
                    {
                        args.Player.SendErrorMessage("Prefix length too long!");
                        return;
                    }

                    clans[clanindex].prefix = input;
                    Data.clanPrefix(args.Player.Account.ID, input);
                    args.Player.SendSuccessMessage($"Successfully changed the clan prefix to \"{input}\"!");
                    TShock.Log.Info($"{args.Player.Account.Name} changed the {clans[clanindex].name} clan's prefix to \"{input}\".");
                    return;
                }
                #endregion
                #region clan invite
                //Clan Invite
                if (type == "invite")
                {
                    if (clanindex == -1)
                    {
                        args.Player.SendErrorMessage("You are not in a clan!");
                        return;
                    }

                    if (clans[clanindex].cprivate && clans[clanindex].members.Contains(args.Player.Account.ID))
                    {
                        args.Player.SendErrorMessage("Only the clan owner/clan admins can invite players to the clan.");
                        return;
                    }

                    var plr = TShock.UserAccounts.GetUserAccountByName(input);

                    if (plr == null)
                    {
                        args.Player.SendErrorMessage($"No players found by the name {input}.");
                        return;
                    }

                    int index = findClan(plr.ID);
                    int invite = getInvite(plr.ID);
                    if (index == clanindex)
                        args.Player.SendErrorMessage("This player is already part of your clan!");
                    else if (index != -1)
                        args.Player.SendErrorMessage("This player is already in a clan!");
                    else if (clans[clanindex].banned.Contains(plr.ID))
                        args.Player.SendErrorMessage("This player is banned from your clan and cannot be invited.");
                    else if (invite != -1 && invite != clanindex)
                        args.Player.SendErrorMessage("This player has already been invited to a different clan and must accept or deny his/her first invitation.");
                    else if (invite != -1 && invite == clanindex)
                        args.Player.SendErrorMessage("This player has already been invited to your clan!");
                    else if (!TShock.Groups.GetGroupByName(plr.Group).HasPermission("clans.use"))
                        args.Player.SendErrorMessage("This player does not have access to the clan commands!");
                    else
                    {
                        clans[clanindex].invited.Add(plr.ID);

                        string name = TShock.UserAccounts.GetUserAccountByID(plr.ID).Name;
                        List<TSPlayer> matches = TSPlayer.FindByNameOrID(name);
                        if (matches.Count > 0)
                            foreach (TSPlayer match in matches)
                                if (match.Account?.ID == plr.ID)
                                    match.SendInfoMessage($"You have been invited to the {clans[clanindex].name} clan! Use '/clan accept' to join the clan or '/clan deny' to reject the invitation.");

                        args.Player.SendSuccessMessage($"{plr.Name} has been invited to join the {clans[clanindex].name} clan!");
                    }
                    return;
                }
                #endregion
                #region clan join
                //Clan Join
                if (type == "join")
                {
                    if (clanindex != -1)
                    {
                        args.Player.SendErrorMessage("You cannot join multiple clans!");
                        return;
                    }

                    List<int> clanindexlist = findClanByName(input);

                    if (clanindexlist.Count == 0)
                    {
                        args.Player.SendErrorMessage($"No clans found by the name {input}.");
                        return;
                    }
                    else if (clanindexlist.Count > 1)
                    {
                        List<string> names = new List<string>();
                        foreach (int num in clanindexlist)
                        {
                            names.Add(clans[num].name);
                        }
                        args.Player.SendErrorMessage($"Multiple matches found: {string.Join(", ", names)}");
                        return;
                    }
                    clanindex = clanindexlist[0];
                    if (clans[clanindex].banned.Contains(args.Player.Account.ID))
                    {
                        args.Player.SendErrorMessage("You have been banned from this clan!");
                        return;
                    }
                    if (clans[clanindex].cprivate && !clans[clanindex].invited.Contains(args.Player.Account.ID))
                    {
                        args.Player.SendErrorMessage("You cannot join a private clan without an invitation!");
                        return;
                    }
                    if (clans[clanindex].invited.Contains(args.Player.Account.ID))
                        clans[clanindex].invited.Remove(args.Player.Account.ID);

                    clans[clanindex].members.Add(args.Player.Account.ID);
                    foreach (TSPlayer plr in TShock.Players)
                    {
                        if (plr != null && plr.Active && plr.IsLoggedIn && plr.Index != args.Player.Index)
                        {
                            int index = findClan(plr.Account.ID);
                            if (index == clanindex)
                                plr.SendInfoMessage($"{args.Player.Name} just joined your clan!");
                        }
                    }
                    Data.changeMembers(clans[clanindex].owner, clans[clanindex]);
                    TShock.Log.Info($"{args.Player.Account.Name} joined the {clans[clanindex].name} clan.");
                    args.Player.SendSuccessMessage($"You have joined the {clans[clanindex].name} clan!");
                    return;
                }
                #endregion
                #region clan kick
                //Clan Kick
                if (type == "kick")
                {
                    if (clanindex == -1)
                    {
                        args.Player.SendErrorMessage("You are not in a clan!");
                        return;
                    }
                    if (!clans[clanindex].admins.Contains(args.Player.Account.ID) && clans[clanindex].owner != args.Player.Account.ID)
                    {
                        args.Player.SendErrorMessage("You cannot kick players out of your clan!");
                        return;
                    }
                    var list = TSPlayer.FindByNameOrID(input);
                    if (list.Count == 0)
                    {
                        var plr2 = TShock.UserAccounts.GetUserAccountByName(input);
                        if (plr2 != null)
                        {
                            int index = findClan(plr2.ID);
                            if (index == -1 || index != clanindex)
                            {
                                args.Player.SendErrorMessage($"{plr2.Name} is not a member of your clan!");
                                return;
                            }
                            else
                            {
                                if (clans[clanindex].owner == plr2.ID)
                                {
                                    args.Player.SendErrorMessage("You cannot kick the owner of your clan!");
                                    return;
                                }
                                else if (clans[clanindex].admins.Contains(plr2.ID))
                                {
                                    args.Player.SendErrorMessage("You cannot kick an admin of your clan!");
                                    return;
                                }
                                clans[clanindex].members.Remove(plr2.ID);
                                Data.changeMembers(clans[clanindex].owner, clans[clanindex]);
                                args.Player.SendSuccessMessage($"You have removed {plr2.Name} from your clan!");
                                TShock.Log.Info($"{args.Player.Account.Name} removed {plr2.Name} from the {clans[clanindex].name} clan.");
                            }
                        }
                        else
                        {
                            args.Player.SendErrorMessage($"No player found by the name {input}.");
                            return;
                        }
                        return;
                    }
                    else if (list.Count > 1 && list[0].Name != input)
                    {
                        args.Player.SendErrorMessage("有多个匹配项！");
                        //TShock.Utils.SendMultipleMatchError(args.Player, list.Select(p => p.Name));
                        return;
                    }

                    TSPlayer plr = list[0];

                    if (clans[clanindex].owner == plr.Account.ID)
                    {
                        args.Player.SendErrorMessage("You cannot kick the owner of your clan!");
                        return;
                    }
                    else if (clans[clanindex].admins.Contains(plr.Account.ID))
                    {
                        args.Player.SendErrorMessage("You cannot kick an admin of your clan!");
                        return;
                    }
                    clans[clanindex].members.Remove(plr.Account.ID);
                    Data.changeMembers(clans[clanindex].owner, clans[clanindex]);
                    args.Player.SendSuccessMessage($"You have removed {plr.Name} from your clan!");
                    plr.SendInfoMessage($"You have been kicked out of {clans[clanindex].name} by {args.Player.Name}!");
                    TShock.Log.Info($"{args.Player.Account.Name} removed {plr.Name} from the {clans[clanindex].name} clan.");
                    return;
                }
                #endregion
                #region clan un/ban
                //Clan Ban
                if (type == "ban")
                {
                    if (clanindex == -1)
                    {
                        args.Player.SendErrorMessage("You are not in a clan!");
                        return;
                    }
                    if (!clans[clanindex].admins.Contains(args.Player.Account.ID) && clans[clanindex].owner != args.Player.Account.ID)
                    {
                        args.Player.SendErrorMessage("You cannot ban players from your clan!");
                        return;
                    }
                    var list = TSPlayer.FindByNameOrID(input);
                    if (list.Count == 0)
                    {
                        var plr2 = TShock.UserAccounts.GetUserAccountByName(input);
                        if (plr2 != null)
                        {
                            int index = findClan(plr2.ID);
                            if (index == -1 || index != clanindex)
                            {
                                args.Player.SendErrorMessage($"{plr2.Name} is not a member of your clan!");
                                return;
                            }
                            else
                            {
                                if (clans[clanindex].owner == plr2.ID)
                                {
                                    args.Player.SendErrorMessage("You cannot ban the owner of your clan!");
                                    return;
                                }
                                else if (clans[clanindex].admins.Contains(plr2.ID))
                                {
                                    args.Player.SendErrorMessage("You cannot ban an admin of your clan!");
                                    return;
                                }
                                clans[clanindex].members.Remove(plr2.ID);
                                clans[clanindex].banned.Add(plr2.ID);
                                Data.changeMembers(clans[clanindex].owner, clans[clanindex]);
                                Data.changeBanned(clans[clanindex].owner, clans[clanindex].banned);
                                args.Player.SendSuccessMessage($"You have banned {plr2.Name} from your clan!");
                                TShock.Log.Info($"{args.Player.Account.Name} banned {plr2.Name} from the {clans[clanindex].name} clan.");
                            }
                        }
                        else
                        {
                            args.Player.SendErrorMessage($"No player found by the name {input}.");
                            return;
                        }
                        return;
                    }
                    else if (list.Count > 1 && list[0].Name != input)
                    {
                        args.Player.SendMultipleMatchError(list.Select(p => p.Name));
                        return;
                    }

                    TSPlayer plr = list[0];

                    if (clans[clanindex].owner == plr.Account.ID)
                    {
                        args.Player.SendErrorMessage("You cannot ban the owner of your clan!");
                        return;
                    }
                    else if (clans[clanindex].admins.Contains(plr.Account.ID))
                    {
                        args.Player.SendErrorMessage("You cannot ban an admin of your clan!");
                        return;
                    }
                    clans[clanindex].members.Remove(plr.Account.ID);
                    clans[clanindex].banned.Add(plr.Account.ID);
                    Data.changeMembers(clans[clanindex].owner, clans[clanindex]);
                    Data.changeBanned(clans[clanindex].owner, clans[clanindex].banned);
                    args.Player.SendSuccessMessage($"You have banned {plr.Name} from your clan!");
                    plr.SendInfoMessage($"You have been banned from {clans[clanindex].name} by {args.Player.Name}!");
                    TShock.Log.Info($"{args.Player.Account.Name} banned {plr.Name} from the {clans[clanindex].name} clan.");
                    return;
                }
                //Clan Unban
                if (type == "unban")
                {
                    if (clanindex == -1)
                    {
                        args.Player.SendErrorMessage("You are not in a clan!");
                        return;
                    }
                    if (clans[clanindex].owner != args.Player.Account.ID && !clans[clanindex].admins.Contains(args.Player.Account.ID))
                    {
                        args.Player.SendErrorMessage("You cannot unban players from your clan!");
                        return;
                    }
                    var list = TSPlayer.FindByNameOrID(input);
                    if (list.Count == 0)
                    {
                        var plr2 = TShock.UserAccounts.GetUserAccountByName(input);
                        if (plr2 != null)
                        {
                            if (!clans[clanindex].banned.Contains(plr2.ID))
                            {
                                args.Player.SendErrorMessage($"{plr2.Name} is not banned from your clan!");
                            }
                            else
                            {
                                clans[clanindex].banned.Remove(plr2.ID);
                                Data.changeBanned(clans[clanindex].owner, clans[clanindex].banned);
                                args.Player.SendSuccessMessage($"You have unbanned {plr2.Name} from your clan!");
                                TShock.Log.Info($"{args.Player.Account.Name} unbanned {plr2.Name} from the {clans[clanindex].name} clan.");
                            }
                        }
                        else
                        {
                            args.Player.SendErrorMessage($"No player found by the name {input}.");
                        }
                        return;
                    }
                    else if (list.Count > 1 && list[0].Name != input)
                    {
                        args.Player.SendMultipleMatchError( list.Select(p => p.Name));
                        return;
                    }

                    TSPlayer plr = list[0];

                    if (!clans[clanindex].banned.Contains(plr.Account.ID))
                    {
                        args.Player.SendErrorMessage($"{plr.Name} is not banned from your clan!");
                        return;
                    }

                    clans[clanindex].banned.Remove(plr.Account.ID);
                    Data.changeBanned(clans[clanindex].owner, clans[clanindex].banned);
                    args.Player.SendSuccessMessage($"You have unbanned {plr.Name} from your clan!");
                    plr.SendInfoMessage($"You have been unbanned from {clans[clanindex].name} by {args.Player.Name}!");
                    TShock.Log.Info($"{args.Player.Account.Name} unbanned {plr.Name} from the {clans[clanindex].name} clan.");
                    return;
                }
                #endregion
                #region clan promote/demote
                //Clan Promote
                if (type == "promote")
                {
                    if (clanindex == -1)
                    {
                        args.Player.SendErrorMessage("You are not in a clan!");
                        return;
                    }
                    if (clans[clanindex].owner != args.Player.Account.ID)
                    {
                        args.Player.SendErrorMessage("You cannot promote clan members to admin in this clan!");
                        return;
                    }
                    var list = TSPlayer.FindByNameOrID(input);
                    if (list.Count == 0)
                    {
                        var plr = TShock.UserAccounts.GetUserAccountByName(input);
                        if (plr == null)
                        {
                            args.Player.SendErrorMessage($"No player found by the name {input}");
                            return;
                        }
                        if (clans[clanindex].admins.Contains(plr.ID))
                        {
                            args.Player.SendErrorMessage($"{plr.Name} is already an admin in this clan!");
                            return;
                        }
                        if (!clans[clanindex].members.Contains(plr.ID))
                        {
                            args.Player.SendErrorMessage($"{plr.Name} is not a member of your clan!");
                            return;
                        }
                        clans[clanindex].admins.Add(plr.ID);
                        clans[clanindex].members.Remove(plr.ID);
                        Data.changeMembers(args.Player.Account.ID, clans[clanindex]);
                        args.Player.SendSuccessMessage($"{plr.Name} is now an admin of your clan!");
                        TShock.Log.Info($"{args.Player.Account.Name} made {plr.Name} an admin of the {clans[clanindex].name} clan.");
                        return;
                    }
                    if (list.Count > 1 && list[0].Name != input)
                    {
                        args.Player.SendMultipleMatchError( list.Select(p => p.Name));
                        return;
                    }
                    if (clans[clanindex].admins.Contains(list[0].Account.ID))
                    {
                        args.Player.SendErrorMessage($"{list[0].Name} is already an admin in this clan!");
                        return;
                    }
                    if (!clans[clanindex].members.Contains(list[0].Account.ID))
                    {
                        args.Player.SendErrorMessage($"{list[0].Name} is not a member of your clan!");
                        return;
                    }
                    clans[clanindex].admins.Add(list[0].Account.ID);
                    clans[clanindex].members.Remove(list[0].Account.ID);
                    Data.changeMembers(args.Player.Account.ID, clans[clanindex]);
                    args.Player.SendSuccessMessage($"{list[0].Name} is now an admin of your clan!");
                    TShock.Log.Info($"{args.Player.Account.Name} made {list[0].Account.Name} an admin of the {clans[clanindex].name} clan.");
                    list[0].SendInfoMessage($"You are now an admin of the {clans[clanindex].name} clan by {args.Player.Name}.");
                    return;
                }
                //Clan Demote
                if (type == "demote")
                {
                    if (clanindex == -1)
                    {
                        args.Player.SendErrorMessage("You are not in a clan!");
                        return;
                    }
                    if (clans[clanindex].owner != args.Player.Account.ID)
                    {
                        args.Player.SendErrorMessage("You cannot demote clan members in this clan!");
                        return;
                    }
                    var list = TSPlayer.FindByNameOrID(input);
                    if (list.Count == 0)
                    {
                        var plr = TShock.UserAccounts.GetUserAccountByName(input);
                        if (plr == null)
                        {
                            args.Player.SendErrorMessage($"No player found by the name {input}");
                            return;
                        }
                        if (!clans[clanindex].admins.Contains(plr.ID))
                        {
                            args.Player.SendErrorMessage($"{plr.Name} is not an admin in this clan!");
                            return;
                        }
                        clans[clanindex].admins.Remove(plr.ID);
                        clans[clanindex].members.Add(plr.ID);
                        Data.changeMembers(args.Player.Account.ID, clans[clanindex]);
                        args.Player.SendSuccessMessage($"{plr.Name} is no longer an admin of your clan!");
                        TShock.Log.Info($"{args.Player.Account.Name} demoted {plr.Name} from admin in the {clans[clanindex].name} clan.");
                        return;
                    }
                    if (list.Count > 1 && list[0].Name != input)
                    {
                        args.Player.SendMultipleMatchError(list.Select(p => p.Name));
                        return;
                    }
                    if (!clans[clanindex].admins.Contains(list[0].Account.ID))
                    {
                        args.Player.SendErrorMessage($"{list[0].Name} is not an admin in this clan!");
                        return;
                    }
                    clans[clanindex].admins.Remove(list[0].Account.ID);
                    clans[clanindex].members.Add(list[0].Account.ID);
                    Data.changeMembers(args.Player.Account.ID, clans[clanindex]);
                    args.Player.SendSuccessMessage($"{list[0].Name} is no longer an admin of your clan!");
                    TShock.Log.Info($"{args.Player.Account.Name} demoted {list[0].Account.Name} from admin in the {clans[clanindex].name} clan.");
                    list[0].SendInfoMessage($"You have been demoted from the {clans[clanindex]} by {args.Player.Name}.");
                    return;
                }
                #endregion
                args.Player.SendErrorMessage("Invalid syntax. Use /clan help for help.");
            }
            else
            {
                args.Player.SendErrorMessage("Invalid syntax. Use /clan help for help.");
            }

        }

        private void ClansStaff(CommandArgs args)
        {
            //cs <option> <clan name> [params]

            //2+
            //cs members <clan name>
            //3
            //cs kick <clan> <player>
            //3
            //cs ban <clan> <player>
            //3
            //cs unban <clan> <player>
            //3+
            //cs prefix <clan> <prefix+>
            //2+
            //cs delete <clan+> -- admins+?

            #region clan help
            if (args.Parameters.Count > 0 && args.Parameters[0].ToLower() == "help")
            {
                int pagenumber;

                if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out pagenumber))
                    return;

                List<string> cmds = new List<string>();

                cmds.Add("members <clan name> [page #] - List all of the members in the specified clan.");
                cmds.Add("prefix <clan name> <prefix> - Changes the specified clan's chat prefix.");
                cmds.Add("kick <clan name> <player name> - Kicks the specified player from the specified clan.");
                cmds.Add("ban <clan name> <player name> - Bans the specified player from the specified clan.");
                cmds.Add("unban <clan name> <player name> - Unbans the specified player from the specified clan.");
                if (args.Player.Group.HasPermission("clans.admin"))
                    cmds.Add("delete <clan name> - Deletes the specified clan. Do not use without good reason.");

                PaginationTools.SendPage(args.Player, pagenumber, cmds,
                    new PaginationTools.Settings
                    {
                        HeaderFormat = "ClanStaff Sub-Commands ({0}/{1}):",
                        FooterFormat = "Type {0}cs help {{0}} for more sub-commands.".SFormat(args.Silent ? TShock.Config.Settings.CommandSilentSpecifier : TShock.Config.Settings.CommandSpecifier)
                    }
                );
            }
            #endregion

            else if (args.Parameters.Count > 1)
            {
                string type = args.Parameters[0].ToLower();
                string clan = args.Parameters[1];

                #region cs members
                //cs members <clan name> [page #]
                if (type == "members")
                {
                    var clanindexlist = findClanByName(clan);
                    if (clanindexlist.Count == 0)
                    {
                        args.Player.SendErrorMessage($"No clans found by the name \"{clan}\".");
                    }
                    else if (clanindexlist.Count > 1)
                    {
                        List<string> matches = new List<string>();

                        foreach (int index in clanindexlist)
                            matches.Add(clans[index].name);

                        args.Player.SendMultipleMatchError(matches);
                    }
                    else
                    {
                        int clanindex = clanindexlist[0];

                        int pagenumber;
                        if (!PaginationTools.TryParsePageNumber(args.Parameters, 2, args.Player, out pagenumber))
                            return;

                        List<string> members = new List<string>();

                        members.Add(TShock.UserAccounts.GetUserAccountByID(clans[clanindex].owner).Name + " (Owner)");

                        foreach (int Accountid in clans[clanindex].admins)
                            members.Add(TShock.UserAccounts.GetUserAccountByID(Accountid).Name + " (Admin)");
                        foreach (int Accountid in clans[clanindex].members)
                            members.Add(TShock.UserAccounts.GetUserAccountByID(Accountid).Name);
                        foreach (int Accountid in clans[clanindex].invited)
                            members.Add(TShock.UserAccounts.GetUserAccountByID(Accountid).Name + " (Invited)");

                        PaginationTools.SendPage(args.Player, pagenumber, members,
                            new PaginationTools.Settings
                            {
                                HeaderFormat = clans[clanindex].name + " Clan Members ({0}/{1}):",
                                FooterFormat = "Type {0}cs members {1} {{0}} for more clan members.".SFormat(args.Silent ? TShock.Config.Settings.CommandSilentSpecifier : TShock.Config.Settings.CommandSpecifier, clans[clanindex].name)
                            }
                        );
                    }
                }
                #endregion
                #region cs kick
                //cs kick <clan> <player>
                else if (type == "kick" && args.Parameters.Count == 3)
                {
                    var clanindexlist = findClanByName(clan);
                    if (clanindexlist.Count == 0)
                    {
                        args.Player.SendErrorMessage($"No clans found by the name \"{clan}\".");
                    }
                    else if (clanindexlist.Count > 1)
                    {
                        List<string> matches = new List<string>();

                        foreach (int index in clanindexlist)
                            matches.Add(clans[index].name);

                        args.Player.SendMultipleMatchError(matches);
                    }
                    else
                    {
                        string name = args.Parameters[2];
                        var plr = TShock.UserAccounts.GetUserAccountByName(name);

                        if (plr == null)
                        {
                            args.Player.SendErrorMessage("No players found by the name \"{name}\"");
                        }
                        else
                        {
                            int clanindex = clanindexlist[0];
                            if (plr.ID == clans[clanindex].owner)
                            {
                                args.Player.SendErrorMessage("You cannot kick the owner of the clan!");
                            }
                            else if (clans[clanindex].admins.Contains(plr.ID))
                            {
                                clans[clanindex].admins.Remove(plr.ID);
                                Data.changeMembers(clans[clanindex].owner, clans[clanindex]);
                                args.Player.SendSuccessMessage($"Successfully kicked {plr.Name} from the {clans[clanindex].name} clan.");
                                TShock.Log.Info($"{args.Player.Account.Name} kicked {plr.Name} from the {clans[clanindex].name} clan.");
                            }
                            else if (clans[clanindex].members.Contains(plr.ID))
                            {
                                clans[clanindex].members.Remove(plr.ID);
                                Data.changeMembers(clans[clanindex].owner, clans[clanindex]);
                                args.Player.SendSuccessMessage($"Successfully kicked {plr.Name} from the {clans[clanindex].name} clan.");
                                TShock.Log.Info($"{args.Player.Account.Name} kicked {plr.Name} from the {clans[clanindex].name} clan.");
                            }
                            else
                            {
                                args.Player.SendErrorMessage($"{plr.Name} is not in the {clans[clanindex].name} clan!");
                            } //end if (plr.ID == clans[clanindex].owner)
                        } //end if (plr == null)
                    } //end if (clanindexlist.Count == 0)
                } //end if (type == "kick" && args.Parameters.Count == 3)
                #endregion
                #region cs ban
                //cs ban <clan> <player>
                else if (type == "ban" && args.Parameters.Count == 3)
                {
                    var clanindexlist = findClanByName(clan);
                    if (clanindexlist.Count == 0)
                    {
                        args.Player.SendErrorMessage($"No clans found by the name \"{clan}\".");
                    }
                    else if (clanindexlist.Count > 1)
                    {
                        List<string> matches = new List<string>();

                        foreach (int index in clanindexlist)
                            matches.Add(clans[index].name);

                        args.Player.SendMultipleMatchError(matches);
                    }
                    else
                    {
                        string name = args.Parameters[2];
                        var plr = TShock.UserAccounts.GetUserAccountByName(name);

                        if (plr == null)
                        {
                            args.Player.SendErrorMessage("No players found by the name \"{name}\"");
                        }
                        else
                        {
                            int clanindex = clanindexlist[0];
                            if (plr.ID == clans[clanindex].owner)
                            {
                                args.Player.SendErrorMessage("You cannot ban the owner of the clan!");
                            }
                            else if (clans[clanindex].admins.Contains(plr.ID))
                            {
                                clans[clanindex].admins.Remove(plr.ID);
                                clans[clanindex].banned.Add(plr.ID);
                                Data.changeMembers(clans[clanindex].owner, clans[clanindex]);
                                Data.changeBanned(clans[clanindex].owner, clans[clanindex].banned);
                                args.Player.SendSuccessMessage($"Successfully banned {plr.Name} from the {clans[clanindex].name} clan.");
                                TShock.Log.Info($"{args.Player.Account.Name} banned {plr.Name} from the {clans[clanindex].name} clan.");
                            }
                            else if (clans[clanindex].members.Contains(plr.ID))
                            {
                                clans[clanindex].members.Remove(plr.ID);
                                clans[clanindex].banned.Add(plr.ID);
                                Data.changeMembers(clans[clanindex].owner, clans[clanindex]);
                                Data.changeBanned(clans[clanindex].owner, clans[clanindex].banned);
                                args.Player.SendSuccessMessage($"Successfully banned {plr.Name} from the {clans[clanindex].name} clan.");
                                TShock.Log.Info($"{args.Player.Account.Name} banned {plr.Name} from the {clans[clanindex].name} clan.");
                            }
                            else
                            {
                                args.Player.SendErrorMessage($"{plr.Name} is not in the {clans[clanindex].name} clan!");
                            } //end if (plr.ID == clans[clanindex].owner)
                        } //end if (plr == null)
                    } //end if (clanindexlist.Count == 0)
                } //end if (type == "ban" && args.Parameters.Count == 3)

                else if (type == "unban" && args.Parameters.Count == 3)
                {
                    var clanindexlist = findClanByName(clan);
                    if (clanindexlist.Count == 0)
                    {
                        args.Player.SendErrorMessage($"No clans found by the name \"{clan}\".");
                    }
                    else if (clanindexlist.Count > 1)
                    {
                        List<string> matches = new List<string>();

                        foreach (int index in clanindexlist)
                            matches.Add(clans[index].name);

                        args.Player.SendMultipleMatchError( matches);
                    }
                    else
                    {
                        string name = args.Parameters[2];
                        var plr = TShock.UserAccounts.GetUserAccountByName(name);

                        if (plr == null)
                        {
                            args.Player.SendErrorMessage("No players found by the name \"{name}\"");
                        }
                        else
                        {
                            int clanindex = clanindexlist[0];
                            if (!clans[clanindex].banned.Contains(plr.ID))
                            {
                                args.Player.SendErrorMessage($"{name} is not banned from the {clans[clanindex].name} clan!");
                            }
                            else
                            {
                                clans[clanindex].banned.Remove(plr.ID);
                                clans[clanindex].members.Remove(plr.ID);
                                Data.changeMembers(clans[clanindex].owner, clans[clanindex]);
                                Data.changeBanned(clans[clanindex].owner, clans[clanindex].banned);
                                args.Player.SendSuccessMessage($"Successfully unbanned {plr.Name} from the {clans[clanindex].name} clan.");
                                TShock.Log.Info($"{args.Player.Account.Name} unbanned {plr.Name} from the {clans[clanindex].name} clan.");
                            } //end if (!clans[clanindex].banned.Contains(plr.ID))
                        } //end if (plr == null)
                    } //end if (clanindexlist.Count == 0)
                } //end if (type == "unban" && args.Parameters.Count == 3)
                #endregion
                #region cs prefix
                else if (type == "prefix" && args.Parameters.Count == 3)
                {
                    var clanindexlist = findClanByName(clan);
                    if (clanindexlist.Count == 0)
                    {
                        args.Player.SendErrorMessage($"No clans found by the name \"{clan}\".");
                    }
                    else if (clanindexlist.Count > 1)
                    {
                        List<string> matches = new List<string>();

                        foreach (int index in clanindexlist)
                            matches.Add(clans[index].name);

                        args.Player.SendMultipleMatchError(matches);
                    }
                    else
                    {
                        string prefix = args.Parameters[2];

                        if (prefix.Contains("[i") || prefix.Contains("[c"))
                        {
                            args.Player.SendErrorMessage("You cannot add item/color tags in clan prefixes!");
                        }
                        else if (prefix.Length > 20)
                        {
                            args.Player.SendErrorMessage("Prefix length too long!");
                        }
                        else
                        {
                            clans[clanindexlist[0]].prefix = prefix;
                            Data.clanPrefix(clans[clanindexlist[0]].owner, prefix);
                            args.Player.SendSuccessMessage($"Successfully changed the clan prefix of the {clans[clanindexlist[0]].name} clan to \"{prefix}\".");
                            TShock.Log.Info($"{args.Player.Account.Name} changed the {clans[clanindexlist[0]].name} clan prefix to \"{prefix}\".");
                        } //end if (prefix.Contains("[i") || prefix.Contains("[c"))
                    } //end if (clanindexlist.Count == 0)
                } //end if (type == "prefix" && args.Parameters.Count == 3)
                #endregion
                #region cs delete
                else if (type == "delete" && args.Player.Group.HasPermission("clans.admin"))
                {
                    var clanindexlist = findClanByName(clan);
                    if (clanindexlist.Count == 0)
                    {
                        args.Player.SendErrorMessage($"No clans found by the name \"{clan}\".");
                    }
                    else if (clanindexlist.Count > 1)
                    {
                        List<string> matches = new List<string>();

                        foreach (int index in clanindexlist)
                            matches.Add(clans[index].name);

                        args.Player.SendMultipleMatchError(matches);
                    }
                    else
                    {
                        args.Player.SendSuccessMessage($"Successfully removed the {clans[clanindexlist[0]].name} clan.");
                        TShock.Log.Info($"{args.Player.Account.Name} removed the {clans[clanindexlist[0]].name} clan.");
                        Data.removeClan(clans[clanindexlist[0]].owner);
                        clans.Remove(clans[clanindexlist[0]]);
                    }
                }
                #endregion
                else
                {
                    args.Player.SendErrorMessage("Unknown sub-command. Use /cs help for a list of valid sub-commands.");
                }
            }
            else
            {
                args.Player.SendErrorMessage("Unknown sub-command. Use /cs help for a list of valid sub-commands.");
            }
        }

        private void CChat(CommandArgs args)
        {
            int chatindex = findClan(args.Player.Account.ID);
            if (chatindex == -1)
            {
                args.Player.SendErrorMessage("You are not in a clan!");
                return;
            }
            else if (args.Player.mute)
            {
                args.Player.SendErrorMessage("You are muted.");
                return;
            }
            foreach (TSPlayer plr in TShock.Players)
            {
                if (plr != null && plr.Active && plr.IsLoggedIn && findClan(plr.Account.ID) == chatindex)
                    plr.SendMessage($"(Clanchat) [{args.Player.Name}]: {string.Join(" ", args.Parameters)}", Color.ForestGreen);
            }
        }
        #endregion

        #region Support
        private void CReload(CommandArgs args)
        {
            Data.loadClans();
            Config = Config.GetConfig();
            args.Player.SendSuccessMessage("Clans have been reloaded from the database.");
            TShock.Log.Info($"{args.Player.Account.Name} reloaded Clans database.");
        }

        private int findClan(int Accountid)
        {
            if (Accountid == -1)
                return -1;

            for (int i = 0; i < clans.Count; i++)
            {
                if (clans[i].owner == Accountid || clans[i].admins.Contains(Accountid) || clans[i].members.Contains(Accountid))
                    return i;
            }

            return -1;
        }

        private List<int> findClanByName(string name)
        {
            List<int> clanslist = new List<int>();

            for (int i = 0; i < clans.Count; i++)
            {
                if (clans[i].name.Contains(name))
                    clanslist.Add(i);

                if (clans[i].name == name) //exact match
                    return new List<int>() { i };
            }

            return clanslist;
        }

        private int getInvite(int Accountid)
        {
            for (int i = 0; i < clans.Count; i++)
            {
                if (clans[i].invited.Contains(Accountid))
                    return i;
            }
            return -1;
        }
        #endregion
    }
}
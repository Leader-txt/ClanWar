using Microsoft.Data.Sqlite;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria;
using TShockAPI;
using TShockAPI.DB;

namespace ClanWar
{
    public class Item
    {
        public int netID { get; set; }
        public int stack { get; set; }
        public byte prefix { get; set; }
    }
    public class Data
    {
        public static IDbConnection db;

        public static void DBConnect()
        {
            switch (TShock.Config.Settings.StorageType.ToLower())
            {
                case "mysql":
                    string[] dbHost = TShock.Config.Settings.MySqlHost.Split(':');
                    db = new MySqlConnection()
                    {
                        ConnectionString = string.Format("Server={0}; Port={1}; Database={2}; Uid={3}; Pwd={4};",
                            dbHost[0],
                            dbHost.Length == 1 ? "3306" : dbHost[1],
                            TShock.Config.Settings.MySqlDbName,
                            TShock.Config.Settings.MySqlUsername,
                            TShock.Config.Settings.MySqlPassword)

                    };
                    break;

                case "sqlite":
                    string sql = Path.Combine(TShock.SavePath, "ClanWar.sqlite");
                    db = new SqliteConnection(string.Format("Data Source={0}", sql));
                    break;

            }

            SqlTableCreator sqlcreator = new SqlTableCreator(db, db.GetSqlType() == SqlType.Sqlite ? (IQueryBuilder)new SqliteQueryCreator() : new MysqlQueryCreator());

            sqlcreator.EnsureTableStructure(new SqlTable("Clans",
                new SqlColumn("owner", MySqlDbType.Int32) { Primary = true, Unique = true, Length = 7 },
                new SqlColumn("name", MySqlDbType.Text) { Length = 30 },
                new SqlColumn("admins", MySqlDbType.Text) { Length = 100 },
                new SqlColumn("members", MySqlDbType.Text) { Length = 100 },
                new SqlColumn("prefix", MySqlDbType.Text) { Length = 30 },
                new SqlColumn("banned", MySqlDbType.Text) { Length = 100 },
                new SqlColumn("priv", MySqlDbType.Int32) { Length = 1 }));
            ///ClanUser
            ///用户类
            ///名字
            ///AccountID
            ///经验
            ///等级
            sqlcreator.EnsureTableStructure(new SqlTable("ClanUser",
                new SqlColumn("name", MySqlDbType.Text),
                new SqlColumn("ID", MySqlDbType.Int32),
                new SqlColumn("Exp", MySqlDbType.Int32),
                new SqlColumn("Level", MySqlDbType.Int32)));

            sqlcreator.EnsureTableStructure(new SqlTable("RewardKill",
                new SqlColumn("Hunt", MySqlDbType.Text),
                new SqlColumn("Sender", MySqlDbType.Text),
                new SqlColumn("RewardExp", MySqlDbType.Int32),
                new SqlColumn("RewardItem", MySqlDbType.Text),
                new SqlColumn("Receiver",MySqlDbType.Text)));
        }
        #region RewardKill
        public static RewardKill[] GetRewardKills(string name)
        {
            var kills = new List<RewardKill>();
            using (var reader = db.QueryReader($"select Hunt,RewardExp,RewardItem,Sender,Receiver from RewardKill where Receiver='{name}'"))
            {
                while (reader.Read())
                {
                    var kill = new RewardKill()
                    {
                        Hunt = reader.Reader.GetString(0),
                        RewardExp = reader.Reader.GetInt32(1),
                        RewardItem = JsonConvert.DeserializeObject<List<Item>>(reader.Reader.GetString(2)),
                        Sender = reader.Reader.GetString(3),
                        Receiver = reader.Reader.GetString(4)
                    };
                    kills.Add(kill);
                }
            }
            return kills.ToArray();
        }
        public static RewardKill[] GetRewardKill()
        {
            var kills=new List<RewardKill>();
            using (var reader =db.QueryReader("select Hunt,RewardExp,RewardItem,Sender,Receiver from RewardKill"))
            {
                while (reader.Read())
                {
                    var kill = new RewardKill();
                    try
                    {
                        kill.Receiver = reader.Reader.GetString(4);
                    }
                    catch { }
                    try
                    {
                        kill.Sender= reader.Reader.GetString(3);
                    }
                    catch { }
                    try
                    {
                        kill.RewardItem = JsonConvert.DeserializeObject<List<Item>>(reader.Reader.GetString(2));
                    }
                    catch { }
                    try
                    {
                        kill.RewardExp = reader.Reader.GetInt32(1);
                    }
                    catch { }
                    try
                    {
                        kill.Hunt = reader.Reader.GetString(0);
                    }
                    catch { }
                    kills.Add(kill);
                }
            }
            return kills.ToArray();
        }
        public static RewardKill GetRewardKill(string name)
        {
            RewardKill kill = null;
            using (var reader = db.QueryReader($"select Hunt,RewardExp,RewardItem,Sender from RewardKill where Hunt='{name}'"))
            {
                if (reader.Read())
                {
                    kill = new RewardKill()
                    {
                        Hunt = reader.Reader.GetString(0),
                        RewardExp = reader.Reader.GetInt32(1),
                        RewardItem = JsonConvert.DeserializeObject<List<Item>>(reader.Reader.GetString(2)),
                        Sender= reader.Reader.GetString(3),
                    };
                }
            }
            return kill;
        }
        #endregion
        #region ClanUser

        /// <summary>
        /// 获取用户信息
        /// </summary>
        /// <param name="id">用户id</param>
        /// <returns>用户对象，若无则为null</returns>
        public static ClanUser GetClanUser(int id)
        {
            ClanUser user = null;
            using (var reader = db.QueryReader($"select name,ID,Exp,Level from ClanUser where ID={id}"))
            {
                if (reader.Read())
                {
                    user = new ClanUser() { Name = reader.Reader.GetString(0), ID = reader.Reader.GetInt32(1), Exp = reader.Reader.GetInt32(2), Level = reader.Reader.GetInt32(3) };
                }
            }
            return user;
        }
        /// <summary>
        /// 获取用户信息
        /// </summary>
        /// <param name="name">用户名</param>
        /// <returns>用户对象，若无则为null</returns>
        public static ClanUser GetClanUser(string name)
        {
            ClanUser user = null;
            using (var reader=db.QueryReader($"select name,ID,Exp,Level from ClanUser where name='{name}'"))
            {
                if (reader.Read())
                {
                    user = new ClanUser() { Name = reader.Reader.GetString(0), ID = reader.Reader.GetInt32(1), Exp = reader.Reader.GetInt32(2), Level = reader.Reader.GetInt32(3) };
                }
                else
                {
                    using (var re=TShock.DB.QueryReader($"select ID from Users where Username='{name}'"))
                    {
                        if (re.Read())
                        {
                            user = new ClanUser() { ID = re.Reader.GetInt32(0), Name = name };
                        }
                    }
                }
            }
            return user;
        }
        #endregion
        public static void loadClans()
        {
            ClanWar.clans.Clear();

            using (QueryResult reader = db.QueryReader(@"SELECT * FROM Clans"))
            {
                while (reader.Read())
                {
                    string adminstr = reader.Get<string>("admins");
                    List<int> adminlist = new List<int>();
                    if (adminstr != "")
                    {
                        adminstr = adminstr.Trim(',');
                        string[] adminsplit = adminstr.Split(',');
                        foreach (string str in adminsplit)
                            adminlist.Add(int.Parse(str));
                    }

                    string memberstr = reader.Get<string>("members");
                    List<int> memberlist = new List<int>();
                    if (memberstr != "")
                    {
                        memberstr = memberstr.Trim(',');
                        string[] membersplit = memberstr.Split(',');
                        foreach (string str in membersplit)
                            memberlist.Add(int.Parse(str));
                    }

                    string banstr = reader.Get<string>("banned");
                    List<int> banlist = new List<int>();
                    if (banstr != "")
                    {
                        banstr = banstr.Trim(',');
                        string[] bansplit = banstr.Split(',');
                        foreach (string str in bansplit)
                            banlist.Add(int.Parse(str));
                    }

                    bool ispriv = reader.Get<int>("priv") == 1 ? true : false;

                    ClanWar.clans.Add(new Clan(reader.Get<string>("name"), reader.Get<int>("owner"))
                    {
                        admins = adminlist,
                        banned = banlist,
                        members = memberlist,
                        prefix = reader.Get<string>("prefix"),
                        cprivate = ispriv,
                        invited = new List<int>()
                    });
                }
            }
        }

        public static void removeClan(int owner)
        {
            int result = db.Query("DELETE FROM Clans WHERE owner=@0", owner);
            if (result != 1)
                TShock.Log.Error($"Database error: Failed to delete from Clans where owner = {owner}.");
        }

        public static void changeOwner(int oldowner, Clan newclan)
        {
            string admins = ",";
            string members = ",";
            admins += string.Join(",", newclan.admins);
            admins += ",";
            members += string.Join(",", newclan.members);
            members += ",";

            if (newclan.admins.Count == 0)
                admins = "";
            if (newclan.members.Count == 0)
                members = "";
            int result = db.Query("UPDATE Clans SET owner=@0,admins=@1,members=@2 WHERE owner=@3;", newclan.owner, admins, members, oldowner);
            if (result != 1)
                TShock.Log.Error($"Database error: Failed to change owner where oldowner = {oldowner} and newowner = {newclan.owner}.");
        }

        public static void changeMembers(int owner, Clan newclan)
        {
            string admins = ",";
            string members = ",";
            admins += string.Join(",", newclan.admins);
            admins += ",";
            members += string.Join(",", newclan.members);
            members += ",";
            if (newclan.admins.Count == 0)
                admins = "";
            if (newclan.members.Count == 0)
                members = "";
            int result = db.Query("UPDATE Clans SET admins=@0,members=@1 WHERE owner=@2;", admins, members, owner);
            if (result != 1)
                TShock.Log.Error($"Database error: Failed to update players where owner = {owner}.");
        }

        public static void newClan(string name, int owner)
        {
            int result = db.Query("INSERT INTO Clans (owner, name, admins, members, prefix, banned, priv) VALUES (@0, @1, '', '', '', '', @2);", owner, name, 0);
            if (result != 1)
                TShock.Log.Error($"Database error: Failed to create a new clan with owner = {owner}.");
        }

        public static void clanPrefix(int owner, string newprefix)
        {
            int result = db.Query("UPDATE Clans SET prefix=@0 WHERE owner=@1;", newprefix, owner);
            if (result != 1)
                TShock.Log.Error($"Database error: Failed to set new prefix where owner = {owner}.");
        }

        public static void changeBanned(int owner, List<int> bannedlist)
        {
            string banned = ",";
            banned += string.Join(",", bannedlist);
            banned += ",";
            if (bannedlist.Count == 0)
                banned = "";
            int result = db.Query("UPDATE Clans SET banned=@0 WHERE owner=@1;", banned, owner);
            if (result != 1)
                TShock.Log.Error($"Database error: Failed to update banned list where owner = {owner}.");
        }

        public static void changePrivate(int owner, bool isPrivate)
        {
            int newpriv = isPrivate ? 1 : 0;
            int result = db.Query("UPDATE Clans SET priv=@0 WHERE owner=@1;", newpriv, owner);
            if (result != 1)
                TShock.Log.Error($"Database error: Failed to update private setting where owner = {owner}.");
        }
    }
}

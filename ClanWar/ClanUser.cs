using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TShockAPI;
using TShockAPI.DB;

namespace ClanWar
{
    public class ClanUser
    {
        public string Name { get; set; }
        public int ID { get; set; }
        public int Exp { get; set; }
        public int Level { get; set; }
        public void Save()
        {
            if(Data.GetClanUser(Name )!=null)
            {
                Data.db.Query($"delete from ClanUser where name='{Name}'");
            }
            Data.db.Query($"insert into ClanUser (name,ID,Exp,Level)values('{Name}',{ID},{Exp},{Level})");
        }
        public void Del()
        {
            Data.db.Query($"delete from ClanUser where name='{Name}'");
        }
    }
}

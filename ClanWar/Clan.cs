using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClanWar
{
    public class Clan
    {
        public string name;
        public int owner;
        public List<int> admins;
        public List<int> members;
        public string prefix;
        public List<int> banned;
        public bool cprivate;
        public List<int> invited;

        public Clan(string _name, int _owner)
        {
            name = _name;
            owner = _owner;
            admins = new List<int>();
            members = new List<int>();
            prefix = "";
            banned = new List<int>();
            cprivate = false;
            invited = new List<int>();
        }
    }
}
# ClanWar
TShock plugin —— ClanWar
# 简介:
## 核心
玩家独自或团队合作努力生存下去 目标是首杀boss 所有boss首杀都有奖励 最终目标杀月总 首杀的团队或者个人为赢家 （等级不高于新手保护等级的不算）<br>
## 特点
死亡部分物品掉落（包括物品栏与背包格里的 弹药格钱币格） 可以设置物品栏前若干格不掉落 目前盔甲饰品等专属栏不会掉落<br>
玩家需要努力升级 升级需要经验 经验来源于做任务 做悬赏 击杀他人（按比例扣掉被杀玩家的经验 增加击杀者的经验 二者相等）  升级可以获得永久buff 物品<br>
强制pvp 新手在新手保护等级范围内 不会遭到他人的攻击 <br>

### 任务系统 
玩家完成指定任务:包括 与npc交谈 击杀 使用 装备佩戴 制作 搜集 去世
### 公会系统 
玩家可以创建公会 公会成员之间不可以pvp 公会可以指定专属头衔 公会属于团体 公会中的人率先杀了月总 代表公会胜利
### 悬赏系统 
玩家支付物品/经验等 悬赏击杀某人 接下悬赏的人击杀悬赏目标则可以获得所对应的奖励
### 保护区 
在保护区内禁止一切pvp

# 命令
clan 权限：clanwar.use <br>
c 权限:clanwar.use 聊天设置 <br>
clanreload 权限：clanwar.reload <br>
clanstaff,cs 权限 clanwar.mod <br>
悬赏 权限:clanwar.use <br>
magclan 权限:clanwar.admin 管理命令 <br>
clanwar,cw 权限:clanwar.use 公会战玩家命令 <br>

```{
  "KillRewards": [//boss击杀奖励
    {
      "Boss": 0,//bossid
      "Reward": [//奖励命令，执行对像为服务器，如需填写玩家名称以name代替，例子：/give 1 name 1
        ""
      ],
      "Finished": false//是否已完成boss击杀
    }
  ],
  "Levels": [//升级奖励，每一级都要写对应配置项
    {
      "Level": 1,//等级
      "Exp": 0,//升级到该等级所需经验
      "RewardCmd": [//奖励命令
        "/give 1 name 1"
      ]
    }
  ],
  "NoDropIndexs": [//不会掉落的格子索引
    0
  ],
  "MaxDrop": 0,//死亡最多掉落物品数量
  "MinDrop": 0,//死亡最少掉落物品数量
  "GetRateMin": 0.0,//击杀获取经验值最小比例，该项大于0小于1
  "GetRateMax": 0.0,//击杀获取经验值最大比例，该项大于0小于1
  "PvpLevel": 0,//高于此等级才能pvp
  "KillTimer": 100,//连杀间隔，连杀间隔小于此才能获得连杀奖励
  "RewardBuff": [//连杀奖励buff
    0
  ],
  "ProtectArea": "ProtectArea"//保护区名称
}
```

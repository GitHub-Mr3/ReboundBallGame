using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameData
{
    /// <summary>本地存储键：历史最大通关关卡 id（图鉴解锁进度按它来算）</summary>
    public const string MaxLevelIdKey = "maxLevelId";

    public const string AudioVolumData = "AudioVolumData";

    /// <summary>存储键：玩家当前持有的货币数量（由 CurrencyMgr 统一读写）</summary>
    public const string CoinKey = "coin";

    /// <summary>存储键：累计抽奖次数（用于稀有宠物解禁门槛与冷却）</summary>
    public const string LotteryDrawCountKey = "lotteryDrawCount";

    /// <summary>存储键：最近一次抽中品质 5（SSS）时的累计抽数</summary>
    public const string LotteryLastRare5Key = "lotteryLastRare5";

    /// <summary>存储键：免费单抽的使用日期（yyyyMMdd）</summary>
    public const string LotteryFreeDateKey = "lotteryFreeDate";

    /// <summary>存储键：已拥有的宠物 id 列表（逗号分隔，由 LotteryView 读写）</summary>
    public const string HavePetIdsKey = "havePetIds";

    /// <summary>存储键：宠物碎片总数（所有宠物共用一份，由 PetFragmentMgr 统一读写）</summary>
    public const string PetFragmentKey = "petFragment";

    /// <summary>存储键：图鉴集齐 13 只宠物的豪华礼包是否已领取（1 = 已领，只发一次）</summary>
    public const string HandBookGiftClaimedKey = "handBookGiftClaimed";

    /// <summary>存储键：出战宠物 id（技能系统的技能来源，由 PetSkillMgr 读写）</summary>
    public const string BattlePetIdKey = "battlePetId";

    /// <summary>存储键：宠物等级表（"宠物id:等级" 逗号分隔，由 PetLevelMgr 统一读写）</summary>
    public const string PetLevelKey = "petLevels";

    public const int MaxLevelNum = 12;

    /// <summary>技能 id：圆墩暂歇（小球可暂停一次，再点继续）——暂停按钮和按键触发都按它判断</summary>
    public const int PetSkill_Pause = 12;


    //事件

    public const string EventID_HandBookInfo = "EventID_HandBookInfo";
    public const string EventID_LeveLock = "EventID_LeveLock";
    public const string EventID_ShowParticleObj = "EventID_ShowParticleObj";
    public const string Event_SelectPet = "Event_SelectPet";

    /// <summary>货币数量变化：携带最新的货币总数（int），CurrencyMgr 增减成功时抛出</summary>
    public const string EventID_CoinChanged = "EventID_CoinChanged";

    /// <summary>抽奖结果：携带本次抽到的宠物 id 列表（List&lt;int&gt;），LotteryView 每次抽完抛出</summary>
    public const string EventID_LotteryResult = "EventID_LotteryResult";

    /// <summary>宠物碎片数量变化：携带最新的碎片总数（int），PetFragmentMgr 增减成功时抛出</summary>
    public const string EventID_PetFragmentChanged = "EventID_PetFragmentChanged";

    /// <summary>宠物等级变化：携带升级的宠物 id（int），PetLevelMgr 升级成功时抛出</summary>
    public const string EventID_PetLevelChanged = "EventID_PetLevelChanged";

    //数据类型
    public struct HandBookInfoData
    {
        public Sprite icon;
        public string name;
        public string desc;
    }

    public static string GetLevelName(int index)
    {
        if (index < 0)
            index = 0;
        if (index >= LevelName.Count)
        {
            return "流浪汪";
        }

        return LevelName[index];
    }

    public static string GetLevelSortName(int index)
    {
        var levelName = GetLevelName(index);
        var names = levelName.Split('.');
        if (names.Length > 1)
        {
            return $"<color=#FF001A><b> {names[names.Length - 1]}</b></color>";
        }

        return $"<color=#FF001A><b> {levelName}</b></color>";
    }

    private static List<string> LevelName = new List<string>()
    {
        "柴犬.小焰",
        "博美.雪球",
        "金毛.暖阳",
        "哈士奇.星夜",
        "柯基.青芜",
        "泰迪.桃桃",
        "杰克罗素.小黄巾",
        "罗威纳.赤岩",
        "马尔济斯.紫缨",
        "雪纳瑞.灰砾",
        "边牧.彩斑",
        "松狮.圆墩",
        "中华神犬.旺财"
    };

    public struct PetData
    {
        public int Id;

        //名字
        public string name;

        //描述
        public string desc;
        public int spriteIndex;
        public int SkillID;
        public int quality;
    }

    public struct PetSkillData
    {
        public int SkillID;
        public string SkillName;
        public string SkillDesc;
    }

    public static PetSkillData GetPetSkillData(int skillID)
    {
        if (_petSkillDatas.ContainsKey(skillID))
            return _petSkillDatas[skillID];
        return new PetSkillData();
    }

    private static Dictionary<int, PetSkillData> _petSkillDatas = new()
    {
        [1] = new PetSkillData()
        {
            SkillID = 1,
            SkillName = "烈焰之瞳",
            SkillDesc = "目标判定范围 +15%（满级 +20%）"
        },

        [2] = new PetSkillData()
        {
            SkillID = 2,
            SkillName = "雪球减速",
            SkillDesc = "所有转盘转速 -15%（满级 -25%）"
        },

        [3] = new PetSkillData()
        {
            SkillID = 3,
            SkillName = "暖阳锁轨",
            SkillDesc = "目标漂移速度 -30%（满级 -50%）"
        },
        [4] = new PetSkillData()
        {
            SkillID = 4,
            SkillName = "星夜疾行",
            SkillDesc = "每关免费复活 1 次（满级 3 次）"
        },

        [5] = new PetSkillData()
        {
            SkillID = 5,
            SkillName = "青芜饱腹",
            SkillDesc = "本关通关所需投喂次数 -1（满级 -3，至少 1 次）"
        },
        [6] = new PetSkillData()
        {
            SkillID = 6,
            SkillName = "桃桃拆家",
            SkillDesc = "每关随机停用 1 台转盘（满级 3 台，至少留 1 台）"
        },
        [7] = new PetSkillData()
        {
            SkillID = 7,
            SkillName = "黄巾护体",
            SkillDesc = "撞到机关不判失败，弹回 1 次（满级 3 次）"
        },
        [8] = new PetSkillData()
        {
            SkillID = 8,
            SkillName = "赤岩同心",
            SkillDesc = "所有转盘强制同向旋转"
        },
        [9] = new PetSkillData()
        {
            SkillID = 9,
            SkillName = "紫缨定局",
            SkillDesc = "取消随机初始角，同一套布局"
        },
        [10] = new PetSkillData()
        {
            SkillID = 10,
            SkillName = "灰砾丰饶",
            SkillDesc = "获得的奖励增加 80%（满级 150%）"
        },
        [11] = new PetSkillData()
        {
            SkillID = 11,
            SkillName = "彩斑穿针",
            SkillDesc = "小球碰撞判定缩小 15%（满级 30%）"
        },
        [12] = new PetSkillData()
        {
            SkillID = 12,
            SkillName = "圆墩暂歇",
            SkillDesc = "小球可暂停 1 次，点场景决定新方向（满级 3 次）"
        },
        [13] = new PetSkillData()
        {
            // 原来这里写的是 SkillID = 4（星夜疾行），导致第 13 只宠物实际吃的是技能 4、
            // 「旺财三重奏」永远取不到；改成 13 后与名字 / 描述 / skillTable[13] 对齐。
            SkillID = 13,
            SkillName = "旺财三重奏",
            SkillDesc = "终极三重：转速 -20% + 目标判定 +12% + 每关 2 次失败免疫（满级 -40% / +24% / 4 次）"
        }
    };

    private static List<int> _allPetID = new List<int>() { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13 };
    private static List<int> havePetID = new List<int>();

    public static List<int> GetHavePetID()
    {
        return havePetID;
    }

    public static void AddPetID(int id)
    {
        havePetID.Add(id);
    }

    /// <summary>
    /// 刷新已解锁宠物列表：以存档为准重建 havePetID（图鉴 / 小窝 / 扭蛋都读这份列表）。
    /// 解锁来源两条：
    ///   ① 通关进度：历史最大通关关卡 id = 已解锁的宠物号（第 N 关通关解锁第 N 只，最多 _allPetID.Count 只）；
    ///   ② 扭蛋抽到的：HavePetIdsKey 存档里的 id 串。
    /// 启动时、打开图鉴/小窝前调一次，解锁状态就能正确显示。
    /// </summary>
    public static void RefreshUnlockedPets()
    {
        havePetID.Clear();

        // ① 通关进度
        int maxLevelId = Mathf.Max(0, StorageMgr.GetInt(MaxLevelIdKey, 0));
        int byLevel = Mathf.Min(maxLevelId, _allPetID.Count);
        for (int id = 1; id <= byLevel; id++)
        {
            havePetID.Add(id);
        }

        // ② 扭蛋抽到的（逗号分隔的 id 串），去重补进去
        string saved = StorageMgr.GetString(HavePetIdsKey, "");
        if (string.IsNullOrEmpty(saved)) return;

        string[] parts = saved.Split(',');
        for (int i = 0; i < parts.Length; i++)
        {
            if (int.TryParse(parts[i], out int id) && !havePetID.Contains(id))
            {
                havePetID.Add(id);
            }
        }
    }

    public static List<int> GetAllPetID()
    {
        return _allPetID;
    }

    //宠物数据  
    public static PetData GetPetData(int id)
    {
        if (_petDatas.TryGetValue(id, out PetData data))
            return data;
        return new PetData();
    }

    private static Dictionary<int, PetData> _petDatas = new()
    {
        [1] = new PetData()
        {
            name = $"{GameData.GetLevelName(0)}",
            desc = "山野里跑出来的元气柴犬，红红的领巾配上金色吊牌，笑容永远热烈灿烂。把这团跳动的小火焰收入图鉴，感受源源不断的开心能量！",
            SkillID = 1,
            Id = 1,
            spriteIndex = 0,
            quality = 3
        },
        [2] = new PetData()
        {
            name = $"{GameData.GetLevelName(1)}",
            desc = "像天上落下来的蓬松小雪团，一身雪白绒毛软乎乎。蓝色领结带着清脆铃铛，眨着圆溜溜大眼睛等待被发现。解锁雪球，收获一份纯净的甜蜜治愈",
            SkillID = 2,
            Id = 2,
            spriteIndex = 1,
            quality = 4
        },
        [3] = new PetData()
        {
            name = $"{GameData.GetLevelName(2)}",
            desc = "温柔的阳光大男孩，金色被毛如同午后暖阳。性格宽厚又热忱，无论何时都带着开朗笑容。解锁暖阳，让这份温暖的陪伴，照亮你的收集冒险。",
            SkillID = 3,
            Id = 3,
            spriteIndex = 2,
            quality = 3
        },
        [4] = new PetData()
        {
            name = $"{GameData.GetLevelName(3)}",
            desc = "诞生于静谧星夜之下的哈士奇，深邃冰蓝色眼眸藏着整片星空。深蓝色披巾随风轻扬，又酷又灵动。解锁星夜，捕获这只自带神秘感的星空小狗。",
            SkillID = 4,
            Id = 4,
            spriteIndex = 3,
            quality = 1
        },
        [5] = new PetData()
        {
            name = $"{GameData.GetLevelName(4)}",
            desc = "林间短腿小精灵，翠绿领巾点缀小花，蹦蹦跳跳穿梭花草之间。短短的四肢藏着无限活力。解锁青芜，将裹挟草木花香的可爱，珍藏进图鉴",
            SkillID = 5,
            Id = 5,
            spriteIndex = 4,
            quality = 2
        },
        [6] = new PetData()
        {
            name = $"{GameData.GetLevelName(5)}",
            desc = "粉系浪漫小公主，卷曲的棕毛柔软蓬松，头上缀着粉嫩桃花。红缎蝴蝶结精致雅致，少女感满满。解锁桃桃，邂逅这只温柔甜美的花间小狗",
            SkillID = 6,
            Id = 6,
            spriteIndex = 5,
            quality = 2
        },
        [7] = new PetData()
        {
            name = $"{GameData.GetLevelName(6)}",
            desc = "热爱探险的小小冒险家，活力永远满格。亮黄色领巾随风飘荡，好奇心永不停歇，哪里有趣就往哪里跑。解锁小黄巾，和它一起开启全新的探索旅程。",
            SkillID = 7,
            Id = 7,
            spriteIndex = 6,
            quality = 2
        },
        [8] = new PetData()
        {
            name = $"{GameData.GetLevelName(7)}",
            desc = "沉稳可靠的护卫伙伴，如同赤红岩石一般坚毅。红披风搭配华丽配饰，外表帅气气场十足，内心忠诚温柔。解锁赤岩，收获一位值得信赖的毛茸茸搭档。",
            SkillID = 8,
            Id = 8,
            spriteIndex = 7,
            quality = 4
        },
        [9] = new PetData()
        {
            name = $"{GameData.GetLevelName(8)}",
            desc = "优雅的白毛小仙女，长长的丝滑白毛轻盈飘逸。紫色蝴蝶结和小花配饰典雅动人，举止温柔。解锁紫缨，把这只仙气满满的小狗收入你的收藏。",
            SkillID = 9,
            Id = 9,
            spriteIndex = 8,
            quality = 4
        },
        [10] = new PetData()
        {
            name = $"{GameData.GetLevelName(9)}",
            desc = "自带小胡子的俏皮 “小老头”，灰白斑驳的毛发很有辨识度。藏青色围巾精致大方，冷静外表下藏着调皮灵魂。解锁灰砾，认识这只风趣独特的小狗。",
            SkillID = 10,
            Id = 10,
            spriteIndex = 9,
            quality = 1
        },
        [11] = new PetData()
        {
            name = $"{GameData.GetLevelName(10)}",
            desc = "聪明绝顶的林间机灵鬼，斑驳的被毛色彩灵动，眼神充满灵气。绿系飘带随风摇曳，学习能力超强。解锁彩斑，收下这只聪慧又活泼的小伙伴。",
            SkillID = 11,
            Id = 11,
            spriteIndex = 10,
            quality = 3
        },
        [12] = new PetData()
        {
            name = $"{GameData.GetLevelName(11)}",
            desc = "圆滚滚的厚实毛团子，肉乎乎的脸庞，一副慵懒惬意的模样。红绳挂着圆金牌，性格憨厚温顺。解锁圆墩，抱走这团沉甸甸、软乎乎的毛茸茸。",
            SkillID = 12,
            Id = 12,
            spriteIndex = 11,
            quality = 3
        },
        [13] = new PetData()
        {
            name = $"{GameData.GetLevelName(12)}",
            desc = "金环耀世，红袍加身。田园之躯，神犬之魂。它是游戏中最强的守护者，也是你最忠诚的伙伴。铃铛响处，邪祟尽退；披风展时，万敌臣服。中华田园犬，以凡人之躯，比肩神明。",
            SkillID = 13,
            Id = 13,
            spriteIndex = 12,
            quality = 5
        },
    };

    //VIEW  
    public const string HandBookInfos = "HandBookInfos";
    public const string HandBookItem = "HandBookItem";
    public const string HandBookView = "HandBookView";
    public const string HelpView = "HelpView";
    public const string LevelItem = "LevelItem";
    public const string LevelView = "LevelView";
    public const string PetHouseView = "PetHouseView";
    public const string PetHouseItem = "PetHouseItem";
    public const string SettingView = "SettingView";
    public const string LotteryView = "LotteryView";
    public const string LotteryTipsView = "LotteryTipsView";
    public const string ShopTipItem = "ShopTipItem";
    public const string GetRewardView = "GetRewardView";
    public const string RewardItem = "RewardItem";
    public const string CommonTips = "CommonTips";

    public static string GetQualityName(int quality)
    {
        switch (quality)
        {
            case 1:
                return "B级";
            case 2:
                return "A级";
            case 3:
                return "S级";
            case 4:
                return "SS级";
            case 5:
                return "SSS级";
        }

        return "C级";
    }

    /// <summary>
    /// 宠物列表的展示顺序（不会改动 GameData 里的原始顺序，返回的是新列表）：
    ///   ① 已拥有的排前面，未解锁的排后面；
    ///   ② 同样是已拥有（或同样未解锁）时，品质低的排前面（B → SSS）；
    ///   ③ 品质也相同就按宠物 id 升序。
    /// </summary>
    public static List<int> GetSortedPetID()
    {
        List<int> sorted = new List<int>(GameData.GetAllPetID());
        HashSet<int> owned = new HashSet<int>(GameData.GetHavePetID());

        sorted.Sort((a, b) =>
        {
            // ① 已解锁优先
            bool ownA = owned.Contains(a);
            bool ownB = owned.Contains(b);
            if (ownA != ownB) return ownA ? -1 : 1;

            // ② 品质低的在前（B → SSS）
            int qualityA = GameData.GetPetData(a).quality;
            int qualityB = GameData.GetPetData(b).quality;
            if (qualityA != qualityB) return qualityA.CompareTo(qualityB);

            // ③ 同品质按宠物 id 升序
            return a.CompareTo(b);
        });

        return sorted;
    }
}
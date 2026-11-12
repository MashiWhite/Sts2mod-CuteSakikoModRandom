using ChaosCardGenerator;

namespace CuteSakikoModRandom.CuteSakikoModRandomCode;

/// <summary>
/// 卡名词片库：决定生成出来的随机卡「叫什么」。
///
/// ★ 核心原则（2026-11-09 用户反馈「卡名不要角色互相串」）：
///   每个角色的词片**只取该角色真实卡名的拆分**，绝不跨池借词。
///   下面的表是脚本从 zhs 本地化 + eng 本地化自动生成的，
///   每条都能逐字还原一张真实卡名，英文也用真实卡名的原文。
///
/// AutoAnthony 的拼装规则（硬约束，改的时候必须守住）：
///   · 中文每块 ≤ 2 字，拼接必须逐字还原 <see cref="NameEntry.Chinese"/>；
///   · 英文主词干（EnglishMiddle）非空且不含空格；
///   · 非 Strike 名不能含「打击」/「Strike」。
///
/// ⚠️ 生成后要跑 SelfTest 的「词片自检」，它会逐条验证上面三条规则。
/// </summary>
internal static class RandomNames
{
    internal readonly record struct NameEntry(
        string PartId,
        string Chinese,
        string English,
        IReadOnlyList<string> ChineseParts,
        string EnglishPrefix,
        string EnglishMiddle,
        string EnglishSuffix);

    /// <summary>
    /// 名字里不能出现的机制词。
    ///
    /// ★ 2026-11-11 用户第 3 条：**「琴奏」「弦奏」必须放行**——
    ///   它们等价于本体的「打击」（PianoStrike / AnonStrike = CardTag.Strike + 关键词），
    ///   现在由 RandomDefinitionBuilder.ApplyNameKeywords 自动补
    ///   「打击 tag + Playpiano/Playguitar 关键词」，并由 SakiKeywordRuntime 注册运行期适配器，
    ///   所以名字带「琴奏」不会再出现「名不副实」。
    ///
    /// 其余机制词（回忆 / 骑士 / 出鞘 / 猫咪 / 芭菲 / 莱芜 / 压力 / 和弦 / 音符）仍然禁用：
    ///   它们不是「打击」的同义词，随便挂关键词会改变本体计数（如 Memory 会进记忆牌堆）。
    /// </summary>
    private static readonly string[] MechanicWords =
    [
        "回忆", "骑士", "出鞘", "猫咪", "芭菲", "莱芜", "压力", "和弦", "音符"
    ];

    internal static IReadOnlyList<NameEntry> For(string profileId) => Filter(profileId switch
    {
        RandomProfiles.Saki => Saki,
        RandomProfiles.Anon => Anon,
        RandomProfiles.Rana => Rana,
        _ => throw new ArgumentOutOfRangeException(nameof(profileId), profileId, "Unknown random profile.")
    });

    private static IReadOnlyList<NameEntry> Filter(IReadOnlyList<NameEntry> entries) =>
        entries.Where(entry => !MechanicWords.Any(word => entry.Chinese.Contains(word, StringComparison.Ordinal)))
            .ToArray();

    /// <summary>把词片注册进 AutoAnthony。必须在首次生成前调用。</summary>
    internal static void Register(string profileId)
    {
        var entries = For(profileId);
        try
        {
            var parts = entries.Select(entry => new ComponentCardNameParts(
                entry.PartId,
                entry.ChineseParts,
                entry.EnglishPrefix,
                entry.EnglishMiddle,
                entry.EnglishSuffix)).ToArray();
            CardNameGenerator.RegisterExternalParts($"csk:names:{profileId}", parts);
        }
        catch (Exception exception)
        {
            // ★ 这里绝不能静默失败：词片注册不进去，整个 profile 就注册不了，
            //   最终表现为「卡池全是本体默认卡」，很难排查（2026-11-09 就踩过一次）。
            //   常见原因：PartId 撞了原版词片——AutoAnthony 的 Parts 表是**全局唯一**的，
            //   官方词片里有 Parry / ParticleWall 等，我们若直接用真实卡名当 ID 就会撞。
            //   ⇒ 本模组的 PartId 一律带 csSaki_ / csAnon_ / csRana_ 前缀。
            throw new InvalidOperationException(
                $"卡名词片注册失败（profile={profileId}）——"
                + "请检查 PartId 是否与 AutoAnthony 原版词片重名（必须带 csSaki_/csAnon_/csRana_ 前缀）。"
                + $"原始异常：{exception.Message}",
                exception);
        }
    }

    private static NameEntry E(string id, string chinese, string english,
        IReadOnlyList<string> parts, string prefix, string middle, string suffix) =>
        new(id, chinese, english, parts, prefix, middle, suffix);

    /// <summary>
    /// 祥子的词片：全部来自祥子真实卡名（59 条）。
    /// 主题：遗忘 / 回忆 / 琴奏 / 骑士 / 舞台 / 压力。
    /// </summary>
    private static readonly NameEntry[] Saki =
    [
        E("csSaki_DefendSaki", "防御", "Defend", ["防御"], "", "Defend", ""),
        E("csSaki_OnlyOblivion", "唯有忘却", "Only Oblivion", ["唯有", "忘却"], "Only ", "Oblivion", ""),
        E("csSaki_PianoStrike", "琴奏", "Piano Performance", ["琴奏"], "Piano ", "Performance", ""),
        E("csSaki_TemporaryForgetfulness", "暂且遗忘", "Temporary Forgetfulness", ["暂且", "遗忘"], "Temporary ", "Forgetfulness", ""),
        E("csSaki_Argument", "强词夺理", "Argumentative", ["强词", "夺理"], "", "Argumentative", ""),
        E("csSaki_Bansogo", "创可贴", "Band-Aid", ["创", "可贴"], "", "BandAid", ""),
        E("csSaki_Disheartened", "心灰意冷", "Discouraged", ["心灰", "意冷"], "", "Discouraged", ""),
        E("csSaki_DontPost", "别再贴了", "Don't Post Anymore", ["别再", "贴了"], "Don't Post ", "Anymore", ""),
        E("csSaki_EmptyLure", "空利诱人", "Empty Lure", ["空利", "诱人"], "Empty ", "Lure", ""),
        E("csSaki_Forget", "遗忘", "Forget", ["遗忘"], "", "Forget", ""),
        E("csSaki_GetMemory", "陷入回忆", "Falling Into Memory", ["陷入", "回忆"], "Falling Into ", "Memory", ""),
        E("csSaki_Glare", "瞪视", "Glare", ["瞪视"], "", "Glare", ""),
        E("csSaki_HateSelf", "自责", "Self-Hatred", ["自责"], "", "SelfHatred", ""),
        E("csSaki_HeartShield", "心之壁", "Heart's Wall", ["心", "之壁"], "Heart's ", "Wall", ""),
        E("csSaki_NeedPractice", "需要练习", "Need Practice", ["需要", "练习"], "Need ", "Practice", ""),
        E("csSaki_Obsession", "偏执", "Obsession", ["偏执"], "", "Obsession", ""),
        E("csSaki_Read", "阅读", "Read", ["阅读"], "", "Read", ""),
        E("csSaki_RegainMemory", "重拾回忆", "Regain Memory", ["重拾", "回忆"], "Regain ", "Memory", ""),
        E("csSaki_StressResponse", "应激反应", "Stress Response", ["应激", "反应"], "Stress ", "Response", ""),
        E("csSaki_Unsheathe", "骑士出鞘", "Knight Unsheathed", ["骑士", "出鞘"], "Knight ", "Unsheathed", ""),
        E("csSaki_Work", "客服工作", "Customer Service Work", ["客服", "工作"], "Customer Service ", "Work", ""),
        E("csSaki_AbsoluteAuthority", "绝对权威", "Absolute Authority", ["绝对", "权威"], "Absolute ", "Authority", ""),
        E("csSaki_AllForget", "忘却一切", "Forget Everything", ["忘却", "一切"], "Forget ", "Everything", ""),
        E("csSaki_AngrySaki", "生气了", "Angry Sakiko", ["生", "气了"], "Angry ", "Sakiko", ""),
        E("csSaki_Apathetic", "麻木", "Apathetic", ["麻木"], "", "Apathetic", ""),
        E("csSaki_BearMemory", "背负回忆", "Bear Memory", ["背负", "回忆"], "Bear ", "Memory", ""),
        E("csSaki_BeGod", "我来成神", "I Will Become God", ["我来", "成神"], "I Will Become ", "God", ""),
        E("csSaki_BlackBirthday", "黑色生日", "Black Birthday", ["黑色", "生日"], "Black ", "Birthday", ""),
        E("csSaki_BowToMe", "向我俯首", "Bow to Me", ["向我", "俯首"], "Bow to ", "Me", ""),
        E("csSaki_ByMyReign", "由我君临", "By My Reign", ["由我", "君临"], "By My ", "Reign", ""),
        E("csSaki_DeterminedToWin", "志在必得", "Determined to Win", ["志在", "必得"], "Determined to ", "Win", ""),
        E("csSaki_DifficultChoice", "艰难抉择", "Difficult Choice", ["艰难", "抉择"], "Difficult ", "Choice", ""),
        E("csSaki_FinalLive", "终幕演出", "Final Performance", ["终幕", "演出"], "Final ", "Performance", ""),
        E("csSaki_LoftMoon", "阁楼之月", "Loft Moon", ["阁楼", "之月"], "Loft ", "Moon", ""),
        E("csSaki_Masquerade", "假面舞会", "Masquerade", ["假面", "舞会"], "", "Masquerade", ""),
        E("csSaki_MusicalScore", "乐谱", "Musical Score", ["乐谱"], "Musical ", "Score", ""),
        E("csSaki_Oblivionis", "遗忘之力", "Oblivionis", ["遗忘", "之力"], "", "Oblivionis", ""),
        E("csSaki_Patience", "忍耐", "Patience", ["忍耐"], "", "Patience", ""),
        E("csSaki_SakiSweep", "横扫", "Sweep", ["横扫"], "", "Sweep", ""),
        E("csSaki_BlackWhite", "黑白分明", "Black and White", ["黑白", "分明"], "Black and ", "White", ""),
        E("csSaki_DontCareOthers", "旁若无人", "Oblivious to Others", ["旁若", "无人"], "Oblivious to ", "Others", ""),
        E("csSaki_EncourageAgain", "再度鼓励", "Encourage Again", ["再度", "鼓励"], "Encourage ", "Again", ""),
        E("csSaki_FeatherBirds", "一丘之貉", "Birds of a Feather", ["一丘", "之貉"], "Birds of a ", "Feather", ""),
        E("csSaki_GoodCry", "积郁成泣", "Cry It Out", ["积郁", "成泣"], "Cry It ", "Out", ""),
        E("csSaki_HeartAtk", "心灵痛击", "Heart Attack", ["心灵", "痛击"], "Heart ", "Attack", ""),
        E("csSaki_Intermission", "幕间会议", "Intermission", ["幕间", "会议"], "", "Intermission", ""),
        E("csSaki_LikeDream", "如梦一场", "Like a Dream", ["如梦", "一场"], "Like a ", "Dream", ""),
        E("csSaki_Marionette", "提线木偶", "Marionette", ["提线", "木偶"], "", "Marionette", ""),
        E("csSaki_MeetPerformance", "迎接舞台", "Welcome the Stage", ["迎接", "舞台"], "Welcome the ", "Stage", ""),
        E("csSaki_MemoryBurning", "燃烧回忆", "Burning Memories", ["燃烧", "回忆"], "Burning ", "Memories", ""),
        E("csSaki_MemoryComing", "回忆涌来", "Flood of Memories", ["回忆", "涌来"], "Flood of ", "Memories", ""),
        E("csSaki_OtherHalf", "半身", "Other Half", ["半身"], "Other ", "Half", ""),
        E("csSaki_Parry", "招架", "Parry", ["招架"], "", "Parry", ""),
        E("csSaki_Quarrel", "争吵", "Quarrel", ["争吵"], "", "Quarrel", ""),
        E("csSaki_RedTea", "红茶", "Black Tea", ["红茶"], "Black ", "Tea", ""),
        E("csSaki_SakiBathe", "泡澡", "Take a Bath", ["泡澡"], "Take a ", "Bath", ""),
        E("csSaki_SharpenBlade", "磨砺锋刃", "Sharpen the Blade", ["磨砺", "锋刃"], "Sharpen the ", "Blade", ""),
        E("csSaki_StrikeHeart", "直击内心", "Strike the Heart", ["直击", "内心"], "Strike the ", "Heart", ""),
        E("csSaki_WishHappiness", "祝你幸福", "Wish You Happiness", ["祝你", "幸福"], "Wish You ", "Happiness", ""),
    ];

    /// <summary>
    /// 爱音的词片：全部来自爱音真实卡名（41 条）。
    /// 主题：演奏 / 弦 / 练习 / 舞台 / 怯场。
    /// </summary>
    private static readonly NameEntry[] Anon =
    [
        E("csAnon_AnchorConnection", "连接之锚", "Anchor Connection", ["连接", "之锚"], "Anchor ", "Connection", ""),
        E("csAnon_PlayImmediately", "即刻演奏", "Play Immediately", ["即刻", "演奏"], "Play ", "Immediately", ""),
        E("csAnon_AnonDefend", "防御", "Defend", ["防御"], "", "Defend", ""),
        E("csAnon_AnonStrike", "弦奏", "Guitar Performance", ["弦奏"], "Guitar ", "Performance", ""),
        E("csAnon_PlayChord", "奏响和弦", "Play Chords", ["奏响", "和弦"], "Play ", "Chords", ""),
        E("csAnon_TurnIntoNote", "化为音符", "Turn into Notes", ["化为", "音符"], "Turn into ", "Notes", ""),
        E("csAnon_Exhaustion", "累死了", "Exhausted", ["累", "死了"], "", "Exhausted", ""),
        E("csAnon_FreshStart", "崭新开始", "Fresh Start", ["崭新", "开始"], "Fresh ", "Start", ""),
        E("csAnon_LeadMeeting", "主导会议", "Lead the Meeting", ["主导", "会议"], "Lead the ", "Meeting", ""),
        E("csAnon_MindlessPlay", "无心演奏", "Mindless Play", ["无心", "演奏"], "Mindless ", "Play", ""),
        E("csAnon_Provoke", "挑衅", "Provoke", ["挑衅"], "", "Provoke", ""),
        E("csAnon_SeekingTarget", "寻找目标", "Seeking Target", ["寻找", "目标"], "Seeking ", "Target", ""),
        E("csAnon_SmoothPlay", "流畅演奏", "Smooth Play", ["流畅", "演奏"], "Smooth ", "Play", ""),
        E("csAnon_SoCute", "好可爱", "So Cute", ["好", "可爱"], "So ", "Cute", ""),
        E("csAnon_WrongPlay", "弹错了", "Played Wrong", ["弹", "错了"], "Played ", "Wrong", ""),
        E("csAnon_AnonBathe", "泡澡", "Take a Bath", ["泡澡"], "Take a ", "Bath", ""),
        E("csAnon_BiteGuitar", "咬吉他", "Bite the Guitar", ["咬", "吉他"], "Bite the ", "Guitar", ""),
        E("csAnon_DrinkWater", "喝水冷静", "Drink Water to Calm Down", ["喝水", "冷静"], "Drink Water to Calm ", "Down", ""),
        E("csAnon_ImpromptuPlay", "即兴演奏", "Impromptu Play", ["即兴", "演奏"], "Impromptu ", "Play", ""),
        E("csAnon_LingeringTaste", "意犹未尽", "Lingering Taste", ["意犹", "未尽"], "Lingering ", "Taste", ""),
        E("csAnon_Osusume", "推荐饮料", "Recommended Drink", ["推荐", "饮料"], "Recommended ", "Drink", ""),
        E("csAnon_PerfectPlay", "完美演奏", "Perfect Play", ["完美", "演奏"], "Perfect ", "Play", ""),
        E("csAnon_PlayTogether", "共同演奏", "Play Together", ["共同", "演奏"], "Play ", "Together", ""),
        E("csAnon_StageNerves", "怯场", "Stage Nerves", ["怯场"], "Stage ", "Nerves", ""),
        E("csAnon_CommunicateProperly", "好好沟通", "Communicate Properly", ["好好", "沟通"], "Communicate ", "Properly", ""),
        E("csAnon_Contemplation", "沉思", "Contemplation", ["沉思"], "", "Contemplation", ""),
        E("csAnon_EasyNow", "消消气", "Take It Easy", ["消", "消气"], "Take It ", "Easy", ""),
        E("csAnon_EscapismTendency", "习惯逃避", "Tendency to Escape", ["习惯", "逃避"], "Tendency to ", "Escape", ""),
        E("csAnon_FollowBeat", "跟上节奏", "Follow the Beat", ["跟上", "节奏"], "Follow the ", "Beat", ""),
        E("csAnon_GiveCandy", "给你吃糖", "Here Some Candy", ["给你", "吃糖"], "Here Some ", "Candy", ""),
        E("csAnon_Hekitenbansou", "碧天伴走", "Azure Sky Companion", ["碧天", "伴走"], "Azure Sky ", "Companion", ""),
        E("csAnon_MendedGrudge", "冰释前嫌", "Mended Grudge", ["冰释", "前嫌"], "Mended ", "Grudge", ""),
        E("csAnon_Mishap", "意外事件", "Mishap", ["意外", "事件"], "", "Mishap", ""),
        E("csAnon_NaiVitality", "毫无活力", "No Vitality", ["毫无", "活力"], "No ", "Vitality", ""),
        E("csAnon_NearSight", "近视眼", "Nearsighted", ["近", "视眼"], "", "Nearsighted", ""),
        E("csAnon_SecretPractice", "偷偷练习", "Secret Practice", ["偷偷", "练习"], "Secret ", "Practice", ""),
        E("csAnon_SmartAnon", "聪明阿侬", "Smart Anon", ["聪明", "阿侬"], "Smart ", "Anon", ""),
        E("csAnon_SteadyPractice", "坚持练习", "Steady Practice", ["坚持", "练习"], "Steady ", "Practice", ""),
        E("csAnon_Strum", "扫弦", "Strum", ["扫弦"], "", "Strum", ""),
        E("csAnon_SugarOverload", "糖分过高", "Sugar Overload", ["糖分", "过高"], "Sugar ", "Overload", ""),
        E("csAnon_TouchedPerformance", "感动演奏", "Touched Performance", ["感动", "演奏"], "Touched ", "Performance", ""),
    ];

    /// <summary>
    /// 乐奈的词片：全部来自乐奈真实卡名（49 条）。
    /// 主题：芭菲 / 猫咪 / 莱芜 / 吃 / 懒散。
    /// </summary>
    private static readonly NameEntry[] Rana =
    [
        E("csRana_ParfaitTreat", "有人请客", "Someone's Treat", ["有人", "请客"], "Someone's ", "Treat", ""),
        E("csRana_StormInhale", "暴风吸入", "Storm Inhale", ["暴风", "吸入"], "Storm ", "Inhale", ""),
        E("csRana_EatParfait", "大口朵颐", "Devour", ["大口", "朵颐"], "", "Devour", ""),
        E("csRana_RanaDefend", "防御", "Defend", ["防御"], "", "Defend", ""),
        E("csRana_RanaStrike", "打击", "Strike", ["打击"], "", "Strike", ""),
        E("csRana_WantLive", "想要莱芜", "I Want a Live", ["想要", "莱芜"], "I Want a ", "Live", ""),
        E("csRana_BuyParfait", "买芭菲", "Buy Parfait", ["买", "芭菲"], "Buy ", "Parfait", ""),
        E("csRana_CatchNeko", "猫咪打击", "Cat Strike", ["猫咪", "打击"], "Cat ", "Strike", ""),
        E("csRana_Contented", "满足", "Contented", ["满足"], "", "Contented", ""),
        E("csRana_GotCaught", "被抓住了", "Got Caught", ["被抓", "住了"], "Got ", "Caught", ""),
        E("csRana_GotPetted", "被摸摸了", "Got Petted", ["被摸", "摸了"], "Got ", "Petted", ""),
        E("csRana_GrandMa", "奶奶的", "Grandmas", ["奶", "奶的"], "", "Grandmas", ""),
        E("csRana_GroomQuietly", "乖乖理毛", "Groom Quietly", ["乖乖", "理毛"], "Groom ", "Quietly", ""),
        E("csRana_GuitarStrike", "吉他打击", "Guitar Strike", ["吉他", "打击"], "Guitar ", "Strike", ""),
        E("csRana_NekoPet", "撸猫", "Pet the Cat", ["撸猫"], "Pet the ", "Cat", ""),
        E("csRana_NekoTreasure", "猫咪宝藏", "Cat Treasure", ["猫咪", "宝藏"], "Cat ", "Treasure", ""),
        E("csRana_PreLive", "莱芜预备", "Live Preparation", ["莱芜", "预备"], "Live ", "Preparation", ""),
        E("csRana_SkillShowLive", "炫技莱芜", "Skill Show Live", ["炫技", "莱芜"], "Skill Show ", "Live", ""),
        E("csRana_UninvitedGuest", "不请自来", "Uninvited Guest", ["不请", "自来"], "Uninvited ", "Guest", ""),
        E("csRana_WaitItCool", "等它变凉", "Let It Cool", ["等它", "变凉"], "Let It ", "Cool", ""),
        E("csRana_EncoreWish", "还想安可", "Encore Wish", ["还想", "安可"], "Encore ", "Wish", ""),
        E("csRana_EveLive", "莱芜前夕", "Eve of Live", ["莱芜", "前夕"], "Eve of ", "Live", ""),
        E("csRana_FinallyHere", "终于来了", "Finally Here", ["终于", "来了"], "Finally ", "Here", ""),
        E("csRana_LiveCraving", "芜瘾莱了", "Live Craving", ["芜瘾", "莱了"], "Live ", "Craving", ""),
        E("csRana_NekoMarch", "小猫出动", "March of the Cats", ["小猫", "出动"], "March of the ", "Cats", ""),
        E("csRana_SnackView", "边吃边看", "Snack and Watch", ["边吃", "边看"], "Snack and ", "Watch", ""),
        E("csRana_SweetTaste", "豪吃", "Feast", ["豪吃"], "", "Feast", ""),
        E("csRana_WantBoth", "两者皆要", "I Want Both", ["两者", "皆要"], "I Want ", "Both", ""),
        E("csRana_DontRushMe", "别催我", "Don't Rush Me", ["别", "催我"], "Don't Rush ", "Me", ""),
        E("csRana_EatCandy", "吃糖", "Eat Candy", ["吃糖"], "Eat ", "Candy", ""),
        E("csRana_FastStudy", "快速学习", "Fast Study", ["快速", "学习"], "Fast ", "Study", ""),
        E("csRana_HummingParfait", "哼歌芭菲", "Humming Parfait", ["哼歌", "芭菲"], "Humming ", "Parfait", ""),
        E("csRana_HungryCatPounce", "饿猫扑食", "Hungry Cat Pounce", ["饿猫", "扑食"], "Hungry Cat ", "Pounce", ""),
        E("csRana_HungryNeko", "猫饿了", "Hungry Cat", ["猫", "饿了"], "Hungry ", "Cat", ""),
        E("csRana_ImmersedGuitar", "沉浸打击", "Immersed Guitar", ["沉浸", "打击"], "Immersed ", "Guitar", ""),
        E("csRana_LiveWithYou", "伴你相奏", "Perform with You", ["伴你", "相奏"], "Perform with ", "You", ""),
        E("csRana_ManyNeko", "好多猫咪", "So Many Cats", ["好多", "猫咪"], "So Many ", "Cats", ""),
        E("csRana_NekoLeader", "猫咪领袖", "Cat Leader", ["猫咪", "领袖"], "Cat ", "Leader", ""),
        E("csRana_NekoTrance", "和猫发呆", "Spacing Out with Cats", ["和猫", "发呆"], "Spacing Out with ", "Cats", ""),
        E("csRana_Osoba", "荞麦面", "Soba Noodles", ["荞", "麦面"], "Soba ", "Noodles", ""),
        E("csRana_PickyEater", "挑食", "Picky Eater", ["挑食"], "Picky ", "Eater", ""),
        E("csRana_PrayParfait", "祈愿芭菲", "Wish for Parfait", ["祈愿", "芭菲"], "Wish for ", "Parfait", ""),
        E("csRana_RainfallOmen", "雨前征兆", "Omen of Rain", ["雨前", "征兆"], "Omen of ", "Rain", ""),
        E("csRana_SkipClass", "逃课", "Skip Class", ["逃课"], "Skip ", "Class", ""),
        E("csRana_SoundCheck", "试音打击", "Sound Check", ["试音", "打击"], "Sound ", "Check", ""),
        E("csRana_SpinachParfait", "大力芭菲", "Spinach Parfait", ["大力", "芭菲"], "Spinach ", "Parfait", ""),
        E("csRana_StageRest", "收工睡觉", "Wrap Up and Sleep", ["收工", "睡觉"], "Wrap Up and ", "Sleep", ""),
        E("csRana_VocalHarmony", "和声", "Vocal Harmony", ["和声"], "Vocal ", "Harmony", ""),
        E("csRana_WantMoreEat", "我还要吃", "I Want More", ["我还", "要吃"], "I Want ", "More", ""),
    ];
}



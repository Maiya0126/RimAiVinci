using Verse;
using RimWorld;

namespace RimAiVinci
{
    // 这个类用来保存每一幅生成的画作数据结构
    // 它会被 ArtDataStore 存入存档文件中
    public class ArtData : IExposable
    {
        // 唯一ID (GUID)
        public string artID;

        // 🌟 核心字段：关联的小人 ThingID (例如 "Human123")
        // 我们通过这个字段把画和人对应起来
        public string pawnID;

        // 🌟 核心字段：图片的相对路径 (例如 "Human123/982734.png")
        // 我们不存绝对路径，因为玩家可能会移动游戏文件夹
        public string relativePath;

        // 生成时使用的提示词 (方便以后查看)
        public string prompt;

        // 生成时间 (Ticks)
        public long timestamp;

        // 当时的小人名字 (快照，防止小人改名后对不上)
        public string authorName;

        // 画作标题 (预留功能，以后可以让玩家改名)
        public string title;

        // ==========================================
        // 构造函数
        // ==========================================

        // 1. 无参构造函数 (Scribe 保存/加载系统必须需要这个)
        public ArtData()
        {
        }

        // 2. 方便代码里调用的构造函数 (可选)
        public ArtData(string artId, string pId, string path, string pText, string pName)
        {
            this.artID = artId;
            this.pawnID = pId;
            this.relativePath = path;
            this.prompt = pText;
            this.authorName = pName;
            this.timestamp = System.DateTime.Now.Ticks;
            this.title = "Untitled"; // 默认为无题
        }

        // ==========================================
        // 保存逻辑
        // ==========================================
        public void ExposeData()
        {
            Scribe_Values.Look(ref artID, "artID");
            Scribe_Values.Look(ref pawnID, "pawnID");
            Scribe_Values.Look(ref relativePath, "relativePath");
            Scribe_Values.Look(ref prompt, "prompt");
            Scribe_Values.Look(ref timestamp, "timestamp");
            Scribe_Values.Look(ref authorName, "authorName");
            Scribe_Values.Look(ref title, "title", "Untitled");
        }
    }
}
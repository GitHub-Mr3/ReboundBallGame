using System.IO;
using UnityEngine;
using WeChatWASM;
using System.Collections.Generic;

public class GameJsModifier : LifeCycleBase
{
    public override void afterCopyDefault()
    {
        string gameJsPath = Path.Combine(BuildTemplateHelper.DstMinigameDir, "game.js");

        if (!File.Exists(gameJsPath)) return;

        string content = File.ReadAllText(gameJsPath);
        if (content.Contains("import './GameRecommend.js'")) return;

        // 按行拆分成列表
        var lines = new List<string>(content.Split('\n'));

        // 在第 2 行之后插入（索引 2 代表第三行位置，即第二行和第三行之间）
        if (lines.Count >= 2)
        {
            lines.Insert(2, "import './GameRecommend.js';");
        }
        else
        {
            // 文件行数不够，直接追加到末尾兜底
            lines.Add("import './GameRecommend.js';");
        }

        File.WriteAllText(gameJsPath, string.Join("\n", lines));
        Debug.Log("已自动在 game.js 第二行和第三行之间插入 GameRecommend.js");
    }
}
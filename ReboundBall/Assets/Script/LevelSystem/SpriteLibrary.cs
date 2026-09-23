using UnityEngine;

/// <summary>
/// 目标点位贴图库：挂到场景里常驻的物体上（推荐挂在 Win 或 GameResult 上），
/// 在 Inspector 里把每关要用的 Sprite 按顺序拖进 sprites，
/// 关卡配置里的 LevelData.winSpriteIndex 就是这里的下标。
///
/// 不挂这个组件也能跑，只是 winSpriteIndex 不会生效（win 保持场景里原本的贴图）。
/// </summary>
public class SpriteLibrary : SingleMonoBase<SpriteLibrary>
{
    [Tooltip("下标对应 LevelData.winSpriteIndex，顺序即关卡配置里填的数字")]
    public Sprite[] sprites = new Sprite[0];

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        Debug.Log($"[WinSpriteLibrary] 注册目标点位贴图 {sprites.Length} 张" +
                  (sprites == null || sprites.Length == 0 ? "（数组为空！请在 Inspector 里拖入 Sprite）" : ""));
    }

    public int GetLevelIconSpriteNum()
    {
        return sprites.Length;
    }

    public Sprite GetLevelIconSprite(int index)
    {
        if (index < 0 || sprites == null || sprites.Length == 0) return null;

        // 超出范围一律夹到最后一张
        int i = Mathf.Clamp(index, 0, sprites.Length - 1);

        Sprite sprite = sprites[i];
        if (sprite == null)
        {
            Debug.LogWarning($"[LevelConfig] 目标点位贴图库第 {i} 项为空");
        }

        return sprite;
    }
}
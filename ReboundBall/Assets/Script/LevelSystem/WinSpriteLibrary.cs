using UnityEngine;

/// <summary>
/// 目标点位贴图库：挂到场景里常驻的物体上（推荐挂在 Win 或 GameResult 上），
/// 在 Inspector 里把每关要用的 Sprite 按顺序拖进 sprites，
/// 关卡配置里的 LevelData.winSpriteIndex 就是这里的下标。
///
/// 不挂这个组件也能跑，只是 winSpriteIndex 不会生效（win 保持场景里原本的贴图）。
/// </summary>
public class WinSpriteLibrary : MonoBehaviour
{
    [Tooltip("下标对应 LevelData.winSpriteIndex，顺序即关卡配置里填的数字")]
    public Sprite[] sprites = new Sprite[0];

    private void Awake()
    {
        LevelConfig.SetWinSprites(sprites);
        Debug.Log($"[WinSpriteLibrary] 注册目标点位贴图 {LevelConfig.WinSprites.Length} 张" +
                  (sprites == null || sprites.Length == 0 ? "（数组为空！请在 Inspector 里拖入 Sprite）" : ""));
    }
}

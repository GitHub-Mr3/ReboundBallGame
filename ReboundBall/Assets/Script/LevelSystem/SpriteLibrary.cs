using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 目标点位贴图库：挂到场景里常驻的物体上（推荐挂在 Win 或 GameResult 上），
/// 在 Inspector 里把每关要用的 Sprite 按顺序拖进 sprites，
/// 关卡配置里的 LevelData.winSpriteIndex 就是这里的下标。
///
/// 不挂这个组件也能跑，只是 winSpriteIndex 不会生效（win 保持场景里原本的贴图）。
/// </summary>
public class SpriteLibrary : SingleMonoBase<SpriteLibrary>
{
    [FormerlySerializedAs("sprites")] [Tooltip("下标对应 LevelData.winSpriteIndex，顺序即关卡配置里填的数字")] [SerializeField]
    private Sprite[] petIcon = new Sprite[0];

    [SerializeField] private Sprite[] petTextureSprite = new Sprite[0];
    [SerializeField] private Sprite[] QualityBgSprite = new Sprite[0];
    [SerializeField] private Sprite PetShuipian;

    [Tooltip("金币图标，拖 Assets/UI/NewUI/moneyIcon.png 进来（签到 / 奖励界面显示金币奖励用）")] [SerializeField]
    private Sprite CoinSprite;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        DebugMgr.Instance.Log($"[WinSpriteLibrary] 注册目标点位贴图 {petIcon.Length} 张" +
                  (petIcon == null || petIcon.Length == 0 ? "（数组为空！请在 Inspector 里拖入 Sprite）" : ""));
    }

    public Sprite GetPetShuipianSprite()
    {
        return PetShuipian;
    }

    /// <summary>金币图标（CoinSprite 没拖就返回 null，调用方自己判空）</summary>
    public Sprite GetCoinSprite()
    {
        return CoinSprite;
    }

    public Sprite GetQualityBgSprite(int quality)
    {
        return QualityBgSprite[quality - 1];
    }

    public int GetLevelIconSpriteNum()
    {
        return petIcon.Length;
    }

    public int GetPetTextureSpriteNum()
    {
        return petTextureSprite.Length;
    }

    public Sprite GetPetTextureSprite(int index)
    {
        if (index < 0 || petTextureSprite == null || petTextureSprite.Length == 0) return null;

        // 超出范围一律夹到最后一张
        int i = Mathf.Clamp(index, 0, petTextureSprite.Length - 1);

        Sprite sprite = petTextureSprite[i];
        if (sprite == null)
        {
            DebugMgr.Instance.LogWarning($"[LevelConfig] 目标点位贴图库第 {i} 项为空");
        }

        return sprite;
    }

    public Sprite GetLevelIconSprite(int index)
    {
        if (index < 0 || petIcon == null || petIcon.Length == 0) return null;

        // 超出范围一律夹到最后一张
        int i = Mathf.Clamp(index, 0, petIcon.Length - 1);

        Sprite sprite = petIcon[i];
        if (sprite == null)
        {
            DebugMgr.Instance.LogWarning($"[LevelConfig] 目标点位贴图库第 {i} 项为空");
        }

        return sprite;
    }
}
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace AIGame
{
    /// <summary>
    /// UI 流光效果：挂到 Image / RawImage / Text / TextMeshProUGUI 上即可。
    /// 运行时会自动在目标下生成一条柔光带，从左向右扫过（不影响原组件的贴图、颜色、字体）。
    ///
    /// 用法：
    ///   1) 把组件挂到图片或文本所在的 GameObject 上（可和 Image / TextMeshProUGUI 同物体）；
    ///   2) 需要时才扫一次的场景（按钮点击、获得奖励等）：调用 PlayOnce()；常驻跑的就不管它。
    ///
    /// 裁切方式（Clip Mode）：
    ///   · Rect  按矩形裁切（自动挂 RectMask2D）：文字整块扫光，最稳，默认；
    ///   · Shape 按贴图/字形形状裁切（自动挂 Mask + 模板遮罩）：形状精确，
    ///           但 TMP 生成子网格（回退字体、表情）时可能被裁掉，图片上放心用；
    ///   · None  不裁切，光带会溢出目标范围。
    ///
    /// 注意：Rect / Shape 模式若目标上原本没有裁切组件，会由本组件自动挂上，
    /// 组件被禁用或移除时会一并删掉（原本就有的不会被删）。
    /// </summary>
    [AddComponentMenu("MrTool/UI 流光效果（UIShineEffect）")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public class UIShineEffect : MonoBehaviour
    {
        public enum ClipMode
        {
            /// <summary>按目标矩形裁切（RectMask2D），推荐。</summary>
            Rect = 0,
            /// <summary>按贴图 / 字形形状裁切（模板 Mask），图片效果最好。</summary>
            Shape = 1,
            /// <summary>不裁切。</summary>
            None = 2
        }

        [Header("外观")]
        [Tooltip("流光颜色，Alpha 决定亮度")]
        [SerializeField] Color shineColor = new Color(1f, 1f, 1f, 0.65f);
        [Tooltip("光带宽度，按目标宽度的比例算")]
        [Range(0.05f, 1f)] [SerializeField] float widthRatio = 0.28f;
        [Tooltip("倾斜角度（0 = 竖直光带）")]
        [Range(-80f, 80f)] [SerializeField] float angle = 22f;

        [Header("节奏")]
        [Tooltip("一次扫过所需时间（秒）")]
        [Min(0.05f)] [SerializeField] float duration = 1.1f;
        [Tooltip("两次流光之间的间隔（秒）")]
        [Min(0f)] [SerializeField] float interval = 1.8f;
        [Tooltip("进入播放后的首次延迟（秒）")]
        [Min(0f)] [SerializeField] float startDelay = 0f;
        [Tooltip("循环播放；关闭则只扫一次")]
        [SerializeField] bool loop = true;
        [Tooltip("启用组件时自动开始播放")]
        [SerializeField] bool playOnEnable = true;
        [Tooltip("使用不受 TimeScale 影响的真实时间（暂停游戏时也能闪）")]
        [SerializeField] bool useUnscaledTime = true;

        [Header("裁切")]
        [SerializeField] ClipMode clipMode = ClipMode.Rect;

        [Header("资源（可选）")]
        [Tooltip("光带贴图。留空时用运行时生成的渐变贴图；" +
                 "打包到微信小游戏等平台时建议指定项目里的真实 Sprite 资源，避免运行时贴图失效。")]
        [SerializeField] Sprite bandSprite;

        // ---- 运行时状态 ----
        RectTransform _rt;
        TMP_Text _tmp;      // 目标是 TMP 文字时，用文字实际包围盒算光带，避免矩形空白处冒光
        GameObject _bandGo;
        RectTransform _bandRt;
        Image _bandImg;

        bool _playing;      // 是否处于播放流程中
        bool _loopNow;      // 本次播放是否循环
        int _stage;         // 0 = 等待，1 = 扫过
        float _timer;       // 当前阶段计时

        Component _clipAdded;   // 本组件自动挂上的裁切组件（禁用时删掉）

        static Sprite s_bandSprite;

        // ============================================================
        // 对外接口
        // ============================================================

        public bool IsPlaying { get { return _playing; } }

        /// <summary>按面板设置播放（含 startDelay / loop）。</summary>
        public void Play()
        {
            BeginPlay(loop);
        }

        /// <summary>只扫一次（不受面板 loop 影响），适合按钮点击等场合。</summary>
        public void PlayOnce()
        {
            BeginPlay(false);
        }

        /// <summary>立刻停止并隐藏光带。</summary>
        public void Stop()
        {
            _playing = false;
            _stage = 0;
            _timer = 0f;
            if (_bandGo != null) _bandGo.SetActive(false);
        }

        [ContextMenu("播放一次（运行时）")]
        void ContextPlayOnce()
        {
            if (!Application.isPlaying)
            {
                DebugMgr.Instance.Log("[凡人烟火] UI 流光效果只在运行时播放，请先进入 Play 模式。");
                return;
            }
            PlayOnce();
        }

        void BeginPlay(bool loopOnce)
        {
            if (!Application.isPlaying) return;

            EnsureBand();
            if (_bandGo == null) return;

            _loopNow = loopOnce;
            _playing = true;
            _stage = 0;
            _timer = Mathf.Max(0f, startDelay);
            if (_timer <= 0f) _stage = 1;   // 无延迟：直接进入扫过阶段
        }

        // ============================================================
        // 生命周期
        // ============================================================

        void OnEnable()
        {
            if (!Application.isPlaying) return;   // 编辑器下不生成子对象，避免污染预制体

            _rt = transform as RectTransform;
            if (playOnEnable) Play();
        }

        void OnDisable()
        {
            _playing = false;

            if (_bandGo != null)
            {
                if (Application.isPlaying) Destroy(_bandGo);
                else DestroyImmediate(_bandGo);
            }
            _bandGo = null; _bandRt = null; _bandImg = null;

            // 只删自己挂上去的裁切组件，原本就有的保持不动
            if (_clipAdded != null)
            {
                if (Application.isPlaying) Destroy(_clipAdded);
                else DestroyImmediate(_clipAdded);
                _clipAdded = null;
            }
        }

        void Update()
        {
            if (!_playing || _bandRt == null || _bandImg == null) return;

            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            if (dt <= 0f) return;

            if (_stage == 0)
            {
                _timer -= dt;
                if (_timer > 0f) return;
                _stage = 1;
                _timer = 0f;
            }

            _timer += dt;
            float p = Mathf.Clamp01(_timer / Mathf.Max(0.05f, duration));
            Layout(p);

            if (p >= 1f)
            {
                if (_loopNow)
                {
                    _stage = 0;
                    _timer = Mathf.Max(0f, interval);
                    if (_timer <= 0f) _stage = 1;
                }
                else
                {
                    Stop();
                }
            }
        }

        // ============================================================
        // 光带
        // ============================================================

        void EnsureBand()
        {
            if (_bandGo != null) return;

            _rt = transform as RectTransform;
            if (_rt == null) return;
            _tmp = GetComponent<TMP_Text>();

            _bandGo = new GameObject("ShineBand", typeof(RectTransform), typeof(Image));
            _bandGo.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
            _bandGo.transform.SetParent(transform, false);
            _bandGo.transform.SetAsLastSibling();   // 盖在目标之上

            _bandRt = (RectTransform)_bandGo.transform;
            _bandRt.anchorMin = new Vector2(0.5f, 0.5f);
            _bandRt.anchorMax = new Vector2(0.5f, 0.5f);
            _bandRt.pivot = new Vector2(0.5f, 0.5f);

            _bandImg = _bandGo.GetComponent<Image>();
            _bandImg.sprite = BandSprite;
            if (_bandImg.sprite == null)
            {
                // 拿不到贴图时绝不画：Image 无贴图会退化成一个纯色实心矩形，看着像一大片色块
                DebugMgr.Instance.LogWarning("[凡人烟火] UI 流光：光带贴图不可用，已跳过（目标：" + name + "）。请在面板上指定 Band Sprite。");
                if (Application.isPlaying) Destroy(_bandGo);
                else DestroyImmediate(_bandGo);
                _bandGo = null; _bandRt = null; _bandImg = null;
                return;
            }
            _bandImg.type = Image.Type.Simple;
            _bandImg.preserveAspect = false;
            _bandImg.raycastTarget = false;   // 不挡点击
            _bandImg.maskable = true;
            _bandImg.color = shineColor;

            _bandGo.SetActive(false);
            SetupClip();
        }

        /// <summary>光带位置 / 尺寸：p = 0 在左侧外面，p = 1 在右侧外面。</summary>
        void Layout(float p)
        {
            Rect r = _rt.rect;
            if (r.width <= 1f || r.height <= 1f) { _bandGo.SetActive(false); return; }
            _bandGo.SetActive(true);

            _bandImg.color = shineColor;

            // 优先用 TMP 文字的实际包围盒（局部空间）：
            // 标题矩形往往比文字大很多（不同平台自适应尺寸不一致），
            // 按矩形算光带会在空白处乱扫、斜着溢出，看起来像一条横穿的大斜杠
            float cx = r.center.x, cy = r.center.y;
            float areaW = r.width, areaH = r.height;
            if (_tmp != null)
            {
                Bounds b = _tmp.textBounds;
                if (b.size.x > 1f && b.size.y > 1f)
                {
                    cx = b.center.x; cy = b.center.y;
                    areaW = b.size.x; areaH = b.size.y;
                }
            }

            float bandW = Mathf.Max(2f, areaW * widthRatio);
            float rad = Mathf.Abs(angle) * Mathf.Deg2Rad;
            float sin = Mathf.Sin(rad);
            float cos = Mathf.Max(0.2f, Mathf.Cos(rad));
            // 旋转后仍要盖住整个高度，否则斜着扫时上下会缺角
            float needH = (areaH + bandW * sin) / cos + 8f;

            _bandRt.sizeDelta = new Vector2(bandW, needH);
            _bandRt.localEulerAngles = new Vector3(0f, 0f, angle);

            float travel = areaW + bandW;
            float x = -travel * 0.5f + travel * p;
            _bandRt.anchoredPosition = new Vector2(cx + x, cy);
        }

        void SetupClip()
        {
            _clipAdded = null;

            switch (clipMode)
            {
                case ClipMode.Rect:
                    if (GetComponent<RectMask2D>() == null && GetComponent<Mask>() == null)
                        _clipAdded = gameObject.AddComponent<RectMask2D>();
                    break;

                case ClipMode.Shape:
                    if (GetComponent<Mask>() == null && GetComponent<RectMask2D>() == null)
                    {
                        Mask m = gameObject.AddComponent<Mask>();
                        m.showMaskGraphic = true;   // 目标本身照常显示，只是兼作遮罩
                        _clipAdded = m;
                    }
                    break;
            }
        }

        /// <summary>运行时生成的柔光带贴图（只建一次，中间的窄高光 + 两侧柔和过渡）。</summary>
        Sprite BandSprite
        {
            get
            {
                if (bandSprite != null) return bandSprite;
                if (s_bandSprite != null) return s_bandSprite;

                // 优先用烘焙进项目里的真实资源（运行时生成的贴图在微信小游戏上可能失效，导致光带变成实心色块）
                s_bandSprite = Resources.Load<Sprite>("ui_shine_band");
                if (s_bandSprite != null) return s_bandSprite;

                const int W = 128, H = 4;
                Texture2D tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
                tex.name = "UIShineBandTex";
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.filterMode = FilterMode.Bilinear;
                tex.hideFlags = HideFlags.HideAndDontSave;

                Color[] px = new Color[W * H];
                for (int x = 0; x < W; x++)
                {
                    float u = x / (float)(W - 1);
                    float d = Mathf.Abs(u - 0.5f) * 2f;            // 0 = 中心，1 = 边缘
                    float a = Mathf.Pow(Mathf.Clamp01(1f - d), 2.2f);   // 柔和主光带
                    a += Mathf.Pow(Mathf.Clamp01(1f - d), 8f) * 0.6f;   // 中心窄高光
                    a = Mathf.Clamp01(a);
                    for (int y = 0; y < H; y++) px[y * W + x] = new Color(1f, 1f, 1f, a);
                }
                tex.SetPixels(px);
                tex.Apply();

                s_bandSprite = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                s_bandSprite.name = "UIShineBand";
                s_bandSprite.hideFlags = HideFlags.HideAndDontSave;
                return s_bandSprite;
            }
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            // 编辑器里调参数时，如果正在播放，下一帧就会自动套用新值
            duration = Mathf.Max(0.05f, duration);
            interval = Mathf.Max(0f, interval);
            startDelay = Mathf.Max(0f, startDelay);
        }
#endif
    }
}

using UnityEngine;
using TMPro;

/// <summary>
/// TextMeshPro 描边工具
/// 挂载到带有 TextMeshProUGUI / TextMeshPro 的物体上即可自动添加描边
/// </summary>
[RequireComponent(typeof(TMP_Text))]
[ExecuteAlways]
public class TMPOutlineTool : MonoBehaviour
{
    [Header("描边设置")]
    [Tooltip("是否启用描边")]
    public bool enableOutline = true;

    [Tooltip("描边颜色")]
    public Color outlineColor = new Color(0f, 0f, 0f, 1f);

    [Tooltip("描边厚度（0~1，越大越粗）")]
    [Range(0f, 1f)]
    public float outlineWidth = 0.2f;

    [Tooltip("描边柔和度（0~1，越大越模糊）")]
    [Range(0f, 1f)]
    public float outlineSoftness = 0f;

    [Header("阴影叠加（可选）")]
    [Tooltip("是否同时添加阴影")]
    public bool enableShadow = false;

    [Tooltip("阴影颜色")]
    public Color shadowColor = new Color(0f, 0f, 0f, 0.5f);

    [Tooltip("阴影偏移")]
    public Vector2 shadowOffset = new Vector2(1f, -1f);

    private TMP_Text _text;
    private bool _isDirty = false;

    private void OnEnable()
    {
        _text = GetComponent<TMP_Text>();
        ApplyOutline();
    }

    private void OnValidate()
    {
        // 在 Inspector 中修改参数时实时更新
        if (_text == null)
            _text = GetComponent<TMP_Text>();

        _isDirty = true;
    }

    private void Update()
    {
        if (_isDirty)
        {
            _isDirty = false;
            ApplyOutline();
        }
    }

    /// <summary>
    /// 应用描边效果
    /// </summary>
    public void ApplyOutline()
    {
        if (_text == null)
            _text = GetComponent<TMP_Text>();

        if (_text == null) return;

        // 确保材质实例独立，避免影响其他文本
        if (_text.fontMaterial == null) return;

        // 开启/关闭描边关键字
        if (enableOutline)
        {
            _text.fontMaterial.EnableKeyword(ShaderUtilities.Keyword_Outline);
            _text.fontMaterial.SetColor(ShaderUtilities.ID_OutlineColor, outlineColor);
            _text.fontMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, outlineWidth);
            _text.fontMaterial.SetFloat(ShaderUtilities.ID_OutlineSoftness, outlineSoftness);
        }
        else
        {
            _text.fontMaterial.DisableKeyword(ShaderUtilities.Keyword_Outline);
        }

        // 阴影
        if (enableShadow)
        {
            _text.fontMaterial.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            _text.fontMaterial.SetColor(ShaderUtilities.ID_UnderlayColor, shadowColor);
            _text.fontMaterial.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, shadowOffset.x);
            _text.fontMaterial.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, shadowOffset.y);
            _text.fontMaterial.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0f);
            _text.fontMaterial.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0f);
        }
        else
        {
            _text.fontMaterial.DisableKeyword(ShaderUtilities.Keyword_Underlay);
        }

        // 通知 TMP 重建
        _text.SetMaterialDirty();
        _text.SetVerticesDirty();
    }

    /// <summary>
    /// 重置为默认描边
    /// </summary>
    [ContextMenu("重置描边")]
    public void ResetOutline()
    {
        outlineColor = Color.black;
        outlineWidth = 0.2f;
        outlineSoftness = 0f;
        enableOutline = true;
        ApplyOutline();
    }

    /// <summary>
    /// 关闭描边
    /// </summary>
    [ContextMenu("关闭描边")]
    public void DisableOutline()
    {
        enableOutline = false;
        ApplyOutline();
    }
}
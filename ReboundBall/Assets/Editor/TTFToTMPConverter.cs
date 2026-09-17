using UnityEngine;
using UnityEditor;
using TMPro;
using UnityEngine.TextCore.LowLevel; // 需要导入 TextMeshPro 的命名空间
/*
 *通过TTF字体创建TMP使用的字体文件
 * 
 */

public class TTFToTMPConverter : EditorWindow
{
    [MenuItem("MrTool/Convert TTF to TMP Font", false, 30)]
    static void ConvertSelectedTTFToTMP()
    {
        // 1. 获取当前在 Project 窗口中选中的对象
        UnityEngine.Object selectedObj = Selection.activeObject;
        if (selectedObj == null)
        {
            Debug.LogWarning("请先选中一个 TTF 字体文件。");
            return;
        }

        // 2. 尝试将其转换为 Font 类型
        Font selectedFont = selectedObj as Font;
        if (selectedFont == null)
        {
            Debug.LogWarning("选中的文件不是一个有效的 TrueType 字体 (.ttf)。");
            return;
        }

        // 3. 获取字体文件的路径，用于生成新资源的文件名
        string fontPath = AssetDatabase.GetAssetPath(selectedFont);
        string directory = System.IO.Path.GetDirectoryName(fontPath);
        string fileName = System.IO.Path.GetFileNameWithoutExtension(fontPath);

        // 4. 调用 TMP 的核心 API 创建字体资源
        // 这里使用了最简参数，你也可以根据需要调整 Atlas 大小和字符集
        TMP_FontAsset tmpFontAsset = TMP_FontAsset.CreateFontAsset(
            selectedFont,        // 源 Font 对象
            90,                  // samplingPointSize
            9,                   // atlasPadding
            GlyphRenderMode.SDFAA, // 渲染模式，SDFAA 是大多数情况下的推荐值[citation:11]
            1024,                // atlasWidth
            1024                 // atlasHeight
        );

        if (tmpFontAsset == null)
        {
            Debug.LogError($"无法从字体 {selectedFont.name} 创建 TMP 字体资源。");
            return;
        }

        // 5. 给新生成的 TMP 字体资源命名，并保存到磁盘
        tmpFontAsset.name = $"{fileName} SDF";
        string assetPath = System.IO.Path.Combine(directory, $"{fileName} SDF.asset");
        AssetDatabase.CreateAsset(tmpFontAsset, assetPath);
        AssetDatabase.SaveAssets();

        // 6. 刷新 Project 视图，并高亮新生成的文件
        AssetDatabase.Refresh();
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = tmpFontAsset;

        Debug.Log($"成功在 {assetPath} 生成 TMP 字体资源。");
    }

    // 可选：控制菜单项是否可用，只有选中 .ttf 文件时才启用
    [MenuItem("Assets/Convert TTF to TMP Font", true)]
    static bool ValidateConvertSelectedTTFToTMP()
    {
        return Selection.activeObject is Font;
    }
}
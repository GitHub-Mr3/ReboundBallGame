using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/*
 * Text → TMP Text 批量替换工具
 *
 * 选择一个节点（场景对象 / 预制体实例）或一个预制体资源，把它及其所有子节点上的
 * UnityEngine.UI.Text 全部替换为 TMPro.TextMeshProUGUI，并统一指定 TMP 字体资源。
 *
 * · 资源形态的 .prefab 会直接修改资源文件本身（请先提交版本控制，无 Undo）；
 * · 场景对象走 Undo，可 Ctrl+Z 整体撤销；
 * · 文字内容 / 颜色 / 字号 / 对齐 / 自动缩放 / 换行溢出 / Raycast / Maskable 会被继承；
 * · Text 上的 Shadow / Outline 组件会保留，对 TMP 依然生效；
 * · 可把指向旧 Text 的引用（脚本字段、Button.targetGraphic、UnityEvent 等）改指向新的 TMP。
 *
 * 字体资源用 MrTool/Convert TTF to TMP Font 先生成（选中 .ttf 后执行）。
 */
public class TextToTMPConverter : EditorWindow
{
    const string PrefKeyFont = "MrTool.TextToTMP.Font";

    TMP_FontAsset _font;
    GameObject _target;
    bool _includeInactive = true;
    bool _unifyTmpFonts = true;
    bool _fixReferences = true;

    Vector2 _scroll;
    string _status = "";

    // ============================================================
    // 入口
    // ============================================================

    [MenuItem("MrTool/Text → TMP Text（替换文字组件）", false, 35)]
    static void Open()
    {
        TextToTMPConverter w = GetWindow<TextToTMPConverter>("Text → TMP");
        w.minSize = new Vector2(580, 520);
        w.Show();
    }

    [MenuItem("GameObject/UI/MrTool/Text → TMP Text（用指定字体）", false, 30)]
    static void OpenFromHierarchy()
    {
        TextToTMPConverter w = GetWindow<TextToTMPConverter>("Text → TMP");
        w.minSize = new Vector2(580, 520);
        if (Selection.activeGameObject != null) w._target = Selection.activeGameObject;
        w.Show();
    }

    [MenuItem("CONTEXT/Text/替换为 TMP Text（MrTool）", false, 10)]
    static void OpenFromComponent(MenuCommand command)
    {
        Text text = command.context as Text;
        if (text == null) return;

        TextToTMPConverter w = GetWindow<TextToTMPConverter>("Text → TMP");
        w.minSize = new Vector2(580, 520);
        w._target = text.gameObject;
        w.Show();
    }

    void OnEnable()
    {
        string path = EditorPrefs.GetString(PrefKeyFont, "");
        if (!string.IsNullOrEmpty(path)) _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (_font == null) _font = FindDefaultFont();
    }

    /// <summary>优先取 Assets/Font 下的 TMP 字体资源作为默认值。</summary>
    static TMP_FontAsset FindDefaultFont()
    {
        string[] guids;
        try { guids = AssetDatabase.FindAssets("t:TMP_FontAsset"); }
        catch { return null; }

        for (int i = 0; i < guids.Length; i++)
        {
            string p = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (p.StartsWith("Assets/Font/", StringComparison.OrdinalIgnoreCase))
                return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(p);
        }
        for (int i = 0; i < guids.Length; i++)
        {
            string p = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (p.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(p);
        }
        return null;
    }

    // ============================================================
    // 界面
    // ============================================================

    void OnGUI()
    {
        EditorGUILayout.LabelField("把 Text 批量替换为 TMP Text", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "选择场景中的节点、或 Project 里的 .prefab 资源，替换它及所有子节点的 UI.Text。\n" +
            "· 同一个 GameObject 只能有一个 Graphic，所以会先删除旧 Text 再添加 TMP；\n" +
            "· .prefab 资源会被直接改写（请先提交版本控制，不可 Undo）；\n" +
            "· 场景对象可 Ctrl+Z 整体撤销；\n" +
            "· 文字/颜色/字号/对齐/换行等会继承，Shadow、Outline 组件保留。", MessageType.Info);

        EditorGUILayout.Space();

        // ---------------- 字体 ----------------
        EditorGUILayout.LabelField("字体", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        _font = (TMP_FontAsset)EditorGUILayout.ObjectField("TMP 字体资源", _font, typeof(TMP_FontAsset), false);
        if (EditorGUI.EndChangeCheck() && _font != null)
            EditorPrefs.SetString(PrefKeyFont, AssetDatabase.GetAssetPath(_font));

        if (_font == null)
            EditorGUILayout.HelpBox("未指定 TMP 字体资源。先在 Project 里选中 .ttf，" +
                                    "执行菜单 MrTool/Convert TTF to TMP Font 生成字体资源。", MessageType.Warning);

        EditorGUILayout.Space();

        // ---------------- 目标 ----------------
        EditorGUILayout.LabelField("目标", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        _target = (GameObject)EditorGUILayout.ObjectField("节点 / 预制体", _target, typeof(GameObject), true);
        if (GUILayout.Button("用当前选中", GUILayout.Width(84)))
            _target = Selection.activeGameObject;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.LabelField(DescribeTarget(_target), EditorStyles.miniLabel);

        EditorGUILayout.Space();

        // ---------------- 选项 ----------------
        EditorGUILayout.LabelField("选项", EditorStyles.boldLabel);
        _includeInactive = EditorGUILayout.Toggle("包含未激活的子节点", _includeInactive);
        _unifyTmpFonts = EditorGUILayout.Toggle("把已有 TMP 文本的字体也统一为上面的资源", _unifyTmpFonts);
        EditorGUILayout.LabelField("      该节点下所有 TextMeshProUGUI 都会用这个字体", EditorStyles.miniLabel);
        _fixReferences = EditorGUILayout.Toggle("把指向旧 Text 的引用改指向 TMP", _fixReferences);
        EditorGUILayout.LabelField("      脚本字段、Button.targetGraphic、UnityEvent 等都会被修正", EditorStyles.miniLabel);

        EditorGUILayout.Space();

        bool ready = _target != null && _font != null;
        EditorGUI.BeginDisabledGroup(!ready);
        if (GUILayout.Button("替换", GUILayout.Height(30)))
        {
            _status = DoConvert();
            Repaint();
        }
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.Space();
        _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
        EditorGUILayout.TextArea(_status, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    static string DescribeTarget(GameObject target)
    {
        if (target == null) return "未选择目标。";

        string path = AssetDatabase.GetAssetPath(target);
        if (!string.IsNullOrEmpty(path) && path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            return "预制体资源：" + path + "（直接改写资源文件）";

        if (PrefabUtility.IsPartOfPrefabInstance(target))
            return "场景中的预制体实例：" + GetTransformPath(target.transform) + "（改动会作为覆盖保存）";

        if (target.scene.IsValid())
            return "场景对象：" + GetTransformPath(target.transform) + "（可 Ctrl+Z 撤销）";

        return "目标不可修改（既不是场景对象，也不是 .prefab 资源）。";
    }

    // ============================================================
    // 替换主流程
    // ============================================================

    string DoConvert()
    {
        if (_target == null) return "请先选择节点或预制体。";
        if (_font == null) return "请先指定 TMP 字体资源。";

        string assetPath = AssetDatabase.GetAssetPath(_target);
        bool isPrefabAsset = !string.IsNullOrEmpty(assetPath)
                             && assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase);

        Counters c = new Counters();
        StringBuilder log = new StringBuilder();

        if (isPrefabAsset)
        {
            GameObject root = null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(assetPath);
                if (root == null) return "无法加载预制体：" + assetPath;

                Convert(root, false, c, log);
                PrefabUtility.SaveAsPrefabAsset(root, assetPath);
                AssetDatabase.SaveAssets();
            }
            catch (Exception e)
            {
                return "处理预制体失败：" + e.Message;
            }
            finally
            {
                if (root != null) PrefabUtility.UnloadPrefabContents(root);
            }
        }
        else if (_target.scene.IsValid())
        {
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Text → TMP 批量替换");

            Convert(_target, true, c, log);

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(_target.scene);
        }
        else
        {
            return "目标不可修改：请选择场景中的对象，或 Project 里的 .prefab 资源。";
        }

        return BuildReport(c, log, isPrefabAsset ? assetPath : GetTransformPath(_target.transform), isPrefabAsset);
    }

    string BuildReport(Counters c, StringBuilder log, string targetPath, bool isPrefabAsset)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("目标：").Append(targetPath).Append('\n');
        sb.Append("字体：").Append(_font.name).Append('\n');
        sb.Append('\n');
        sb.Append("已替换 Text → TextMeshProUGUI：").Append(c.Converted).Append(" 个\n");
        if (c.FontsUnified > 0) sb.Append("统一字体的 TMP 文本：").Append(c.FontsUnified).Append(" 个\n");
        if (c.Failed > 0) sb.Append("失败：").Append(c.Failed).Append(" 个\n");
        if (_fixReferences) sb.Append("修正的引用：").Append(c.RefsFixed).Append(" 处\n");
        if (c.Converted == 0 && c.FontsUnified == 0) sb.Append("\n没找到需要处理的文本组件。\n");

        if (log.Length > 0)
        {
            sb.Append("\n---- 已替换 ----\n");
            sb.Append(log);
        }

        if (isPrefabAsset && (c.Converted > 0 || c.FontsUnified > 0))
            sb.Append("\n注意：预制体资源已直接改写，若效果不对请用版本控制还原。");

        Debug.Log("[Text → TMP] " + sb.ToString().Replace("\n", " | "));
        return sb.ToString();
    }

    void Convert(GameObject root, bool useUndo, Counters c, StringBuilder log)
    {
        // ---------- 1) 收集需要替换的 Text ----------
        Text[] texts = root.GetComponentsInChildren<Text>(_includeInactive);
        List<Text> targets = new List<Text>(texts.Length);
        HashSet<int> doomed = new HashSet<int>();

        for (int i = 0; i < texts.Length; i++)
        {
            Text src = texts[i];
            if (src == null) continue;

            // 理论上不可能同时存在两个 Graphic，保险起见还是判一下
            if (src.gameObject.GetComponent<TextMeshProUGUI>() != null) continue;

            targets.Add(src);
            doomed.Add(src.GetInstanceID());
        }

        // ---------- 2) 删除组件前快照所有指向旧 Text 的引用 ----------
        List<RefRecord> refs = (_fixReferences && targets.Count > 0)
            ? SnapshotReferences(GetScanComponents(root, useUndo), doomed)
            : new List<RefRecord>();

        // ---------- 3) 逐个替换：先删旧 Text，再加 TMP ----------
        //    Graphic 互斥：旧 Text 不删掉，AddComponent<TextMeshProUGUI> 会直接报错
        Dictionary<int, TextMeshProUGUI> map = new Dictionary<int, TextMeshProUGUI>();

        for (int i = 0; i < targets.Count; i++)
        {
            Text src = targets[i];
            if (src == null) continue;

            GameObject go = src.gameObject;
            string itemPath = GetPath(go.transform, root.transform);

            bool hadCanvasRenderer = go.GetComponent<CanvasRenderer>() != null;
            TextData data = Capture(src);
            int oldId = src.GetInstanceID();

            RemoveComponent(src, useUndo);

            TextMeshProUGUI tmp;
            try
            {
                tmp = AddComponent(go, useUndo);
            }
            catch (Exception e)
            {
                c.Failed++;
                log.Append("  [失败] ").Append(itemPath).Append(" - ").Append(e.Message).Append('\n');
                continue;
            }

            if (tmp == null)
            {
                c.Failed++;
                log.Append("  [失败] ").Append(itemPath).Append(" - 无法添加 TextMeshProUGUI\n");
                continue;
            }

            // 删除 Graphic 有时会连带清掉 CanvasRenderer，缺了就补回来
            if (hadCanvasRenderer && go.GetComponent<CanvasRenderer>() == null)
            {
                try
                {
                    if (useUndo) Undo.AddComponent<CanvasRenderer>(go);
                    else go.AddComponent<CanvasRenderer>();
                }
                catch { /* 补不回来也能显示，忽略 */ }
            }

            Apply(data, tmp, _font);

            map[oldId] = tmp;
            c.Converted++;
            log.Append("  ").Append(itemPath).Append('\n');
        }

        // ---------- 4) 回填引用 ----------
        if (refs.Count > 0 && map.Count > 0)
            c.RefsFixed = RestoreReferences(useUndo, refs, map);

        // ---------- 5) 已有 TMP 文本统一字体 ----------
        if (_unifyTmpFonts)
        {
            TextMeshProUGUI[] tmps = root.GetComponentsInChildren<TextMeshProUGUI>(_includeInactive);
            for (int i = 0; i < tmps.Length; i++)
            {
                TextMeshProUGUI t = tmps[i];
                if (t == null || map.ContainsValue(t)) continue;
                if (t.font == _font) continue;
                t.font = _font;
                c.FontsUnified++;
            }
        }
    }

    List<Component> GetScanComponents(GameObject root, bool useUndo)
    {
        List<Component> components = new List<Component>();
        if (useUndo && root.scene.IsValid())
        {
            // 场景：扫描整场景，跨物体的引用也能修好
            GameObject[] roots = root.scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
                components.AddRange(roots[i].GetComponentsInChildren<Component>(true));
        }
        else
        {
            components.AddRange(root.GetComponentsInChildren<Component>(true));
        }
        return components;
    }

    /// <summary>删除组件之前，记录所有指向旧 Text 的序列化引用（删掉之后引用会被清空，就找不回来了）。</summary>
    List<RefRecord> SnapshotReferences(List<Component> components, HashSet<int> doomedTextIds)
    {
        List<RefRecord> records = new List<RefRecord>();

        for (int i = 0; i < components.Count; i++)
        {
            Component comp = components[i];
            if (comp == null) continue;
            if (comp is Transform || comp is Text) continue;
            if (doomedTextIds.Contains(comp.GetInstanceID())) continue;

            SerializedObject so;
            try { so = new SerializedObject(comp); }
            catch { continue; }

            SerializedProperty it = so.GetIterator();
            while (it.NextVisible(true))
            {
                if (it.propertyType != SerializedPropertyType.ObjectReference) continue;

                UnityEngine.Object refObj = it.objectReferenceValue;
                if (refObj == null) continue;
                if (!doomedTextIds.Contains(refObj.GetInstanceID())) continue;

                records.Add(new RefRecord
                {
                    owner = comp,
                    path = it.propertyPath,
                    oldId = refObj.GetInstanceID()
                });
            }
        }
        return records;
    }

    /// <summary>把快照里记录的引用改指向新的 TMP 组件。</summary>
    int RestoreReferences(bool useUndo, List<RefRecord> records, Dictionary<int, TextMeshProUGUI> map)
    {
        Dictionary<int, List<RefRecord>> groups = new Dictionary<int, List<RefRecord>>();
        Dictionary<int, Component> owners = new Dictionary<int, Component>();

        for (int i = 0; i < records.Count; i++)
        {
            RefRecord r = records[i];
            if (r.owner == null) continue;

            TextMeshProUGUI tmp;
            if (!map.TryGetValue(r.oldId, out tmp) || tmp == null) continue;

            int id = r.owner.GetInstanceID();
            List<RefRecord> list;
            if (!groups.TryGetValue(id, out list))
            {
                list = new List<RefRecord>();
                groups[id] = list;
                owners[id] = r.owner;
            }
            list.Add(r);
        }

        int restored = 0;
        foreach (KeyValuePair<int, List<RefRecord>> kv in groups)
        {
            Component owner = owners[kv.Key];
            if (owner == null) continue;

            SerializedObject so;
            try { so = new SerializedObject(owner); }
            catch { continue; }

            bool changed = false;
            List<RefRecord> list = kv.Value;
            for (int i = 0; i < list.Count; i++)
            {
                SerializedProperty p = so.FindProperty(list[i].path);
                if (p == null || p.propertyType != SerializedPropertyType.ObjectReference) continue;

                TextMeshProUGUI tmp;
                if (!map.TryGetValue(list[i].oldId, out tmp) || tmp == null) continue;

                p.objectReferenceValue = tmp;
                changed = true;
                restored++;
            }

            if (changed)
            {
                if (useUndo) so.ApplyModifiedProperties();
                else so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
        return restored;
    }

    // ============================================================
    // 属性快照 / 回填（旧 Text 会被删除，必须先把数据抄出来）
    // ============================================================

    struct TextData
    {
        public string text;
        public Color color;
        public bool raycastTarget;
        public bool maskable;
        public bool richText;
        public bool bestFit;
        public int fontSize;
        public int minSize;
        public int maxSize;
        public TextAnchor anchor;
        public FontStyle style;
        public HorizontalWrapMode hWrap;
        public VerticalWrapMode vWrap;
    }

    static TextData Capture(Text src)
    {
        TextData d = new TextData();
        d.text = src.text;
        d.color = src.color;
        d.raycastTarget = src.raycastTarget;
        d.maskable = src.maskable;
        d.richText = src.supportRichText;
        d.bestFit = src.resizeTextForBestFit;
        d.fontSize = src.fontSize;
        d.minSize = src.resizeTextMinSize;
        d.maxSize = src.resizeTextMaxSize;
        d.anchor = src.alignment;
        d.style = src.fontStyle;
        d.hWrap = src.horizontalOverflow;
        d.vWrap = src.verticalOverflow;
        return d;
    }

    static void Apply(TextData d, TextMeshProUGUI dst, TMP_FontAsset font)
    {
        if (font != null) dst.font = font;

        dst.text = d.text;
        dst.color = d.color;
        dst.raycastTarget = d.raycastTarget;
        dst.maskable = d.maskable;
        dst.richText = d.richText;
        dst.alignment = ToTmpAlignment(d.anchor);

        FontStyles styles = FontStyles.Normal;
        if ((d.style & FontStyle.Bold) != 0) styles |= FontStyles.Bold;
        if ((d.style & FontStyle.Italic) != 0) styles |= FontStyles.Italic;
        dst.fontStyle = styles;

        dst.enableAutoSizing = d.bestFit;
        if (d.bestFit)
        {
            dst.fontSizeMin = d.minSize;
            dst.fontSizeMax = d.maxSize;
            dst.fontSize = Mathf.Clamp(d.fontSize, d.minSize, d.maxSize);
        }
        else
        {
            dst.fontSize = d.fontSize;
        }

        dst.enableWordWrapping = d.hWrap == HorizontalWrapMode.Wrap;
        dst.overflowMode = d.vWrap == VerticalWrapMode.Truncate
            ? TextOverflowModes.Truncate
            : TextOverflowModes.Overflow;

        dst.SetAllDirty();
    }

    static TextAlignmentOptions ToTmpAlignment(TextAnchor anchor)
    {
        switch (anchor)
        {
            case TextAnchor.UpperLeft: return TextAlignmentOptions.TopLeft;
            case TextAnchor.UpperCenter: return TextAlignmentOptions.Top;
            case TextAnchor.UpperRight: return TextAlignmentOptions.TopRight;
            case TextAnchor.MiddleLeft: return TextAlignmentOptions.Left;
            case TextAnchor.MiddleCenter: return TextAlignmentOptions.Center;
            case TextAnchor.MiddleRight: return TextAlignmentOptions.Right;
            case TextAnchor.LowerLeft: return TextAlignmentOptions.BottomLeft;
            case TextAnchor.LowerCenter: return TextAlignmentOptions.Bottom;
            case TextAnchor.LowerRight: return TextAlignmentOptions.BottomRight;
        }
        return TextAlignmentOptions.Center;
    }

    // ============================================================
    // 工具
    // ============================================================

    static TextMeshProUGUI AddComponent(GameObject go, bool useUndo)
    {
        if (useUndo) return Undo.AddComponent<TextMeshProUGUI>(go);
        return go.AddComponent<TextMeshProUGUI>();
    }

    static void RemoveComponent(Component c, bool useUndo)
    {
        if (c == null) return;
        if (useUndo) Undo.DestroyObjectImmediate(c);
        else UnityEngine.Object.DestroyImmediate(c);
    }

    static string GetPath(Transform t, Transform root)
    {
        string path = t.name;
        while (t != null && t.parent != null && t != root)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }

    static string GetTransformPath(Transform t)
    {
        if (t == null) return "";

        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }

    struct RefRecord
    {
        public Component owner;
        public string path;
        public int oldId;
    }

    class Counters
    {
        public int Converted;
        public int FontsUnified;
        public int Failed;
        public int RefsFixed;
    }
}

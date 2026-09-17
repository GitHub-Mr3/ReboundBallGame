using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/*
 * TMP 字体批量替换工具
 *
 * 三种范围：
 *   1) 选中节点 —— Hierarchy 里选中的对象（含所有子节点）；在 Project 里选中的 .prefab
 *      资源 / 文件夹（文件夹会搜出里面所有 .prefab）也会一并处理；
 *   2) 当前场景 —— 当前打开的活动场景中所有 TMP 文本组件；
 *   3) 整个项目 —— 扫描 Assets 下所有 .prefab 资源。
 *
 * · 覆盖范围是所有 TMP_Text（TextMeshProUGUI / TextMeshPro，含 InputField、Dropdown
 *   里的 caption / item / placeholder），只看组件本身，不重复遍历；
 * · 场景对象走 Undo，可 Ctrl+Z 撤销；.prefab 资源直接改写文件，请先提交版本控制；
 * · 可指定"仅替换某个旧字体"，留空则是把所有字体统一成目标字体；
 * · 原来的字号 / 颜色 / 样式 / 对齐都不动；只有原来用的是"该字体的默认材质"时才同步
 *   换成新字体的默认材质，自定义材质（描边、发光等）会保留。
 */
public class TMPFontReplacer : EditorWindow
{
    const string PrefKeyTarget = "MrTool.TMPFontReplacer.Target";
    const string PrefKeyFilter = "MrTool.TMPFontReplacer.Filter";
    const string DefaultFontPath = "Assets/Font/SIMHEI SDF.asset";

    enum Mode { Selection = 0, Scene = 1, Project = 2 }
    static readonly string[] ModeNames = { "选中节点", "当前场景", "整个项目" };

    TMP_FontAsset _target;
    TMP_FontAsset _filter;
    Mode _mode = Mode.Selection;
    bool _includeInactive = true;
    bool _autoSave = true;
    bool _setAsDefault = false;

    Vector2 _scroll;
    string _status = "";

    // 真正的执行放到 OnGUI 之外：扫描/替换会弹进度条并触发重绘，
    // 在布局过程中做这些事会打断 Begin/End 的 GUILayout 栈（Invalid GUILayout state）。
    enum Pending { None, Scan, Replace }
    Pending _pending;

    // ============================================================
    // 入口
    // ============================================================

    [MenuItem("MrTool/替换 TMP 字体（场景 / 选中节点）", false, 36)]
    static void Open()
    {
        TMPFontReplacer w = GetWindow<TMPFontReplacer>("替换 TMP 字体");
        w.minSize = new Vector2(560, 540);
        w.Show();
    }

    [MenuItem("GameObject/UI/MrTool/替换 TMP 字体（用指定字体）", false, 31)]
    static void OpenFromHierarchy()
    {
        TMPFontReplacer w = GetWindow<TMPFontReplacer>("替换 TMP 字体");
        w.minSize = new Vector2(560, 540);
        w._mode = Mode.Selection;
        w.Show();
    }

    /// <summary>
    /// 一键修字体：没选中就修 Assets/Font 下所有 TMP 字体；选中了只修选中的。
    /// 修完再去赋值 / 替换，就不会再抛 UnassignedReferenceException 了。
    /// </summary>
    [MenuItem("MrTool/修复 TMP 字体（图集 + 默认材质）", false, 38)]
    static void RepairFontsMenu()
    {
        List<TMP_FontAsset> list = new List<TMP_FontAsset>();

        for (int i = 0; i < Selection.objects.Length; i++)
        {
            TMP_FontAsset fa = Selection.objects[i] as TMP_FontAsset;
            if (fa != null && !list.Contains(fa)) list.Add(fa);
        }

        if (list.Count == 0)
        {
            string[] guids = AssetDatabase.FindAssets("t:TMP_FontAsset");
            for (int i = 0; i < guids.Length; i++)
            {
                string p = AssetDatabase.GUIDToAssetPath(guids[i]);
                TMP_FontAsset fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(p);
                if (fa != null && !list.Contains(fa)) list.Add(fa);
            }
        }

        if (list.Count == 0) { Debug.LogWarning("[凡人烟火] 没有找到 TMP 字体资源。"); return; }

        StringBuilder sb = new StringBuilder();
        int fixedCount = 0;
        for (int i = 0; i < list.Count; i++)
        {
            string why;
            if (IsFontHealthy(list[i], out why)) continue;   // 已经正常的跳过
            string msg = RepairFontAsset(list[i]);
            sb.AppendLine(msg);
            fixedCount++;
        }

        string report = fixedCount == 0
            ? "所有 TMP 字体都正常，无需修复。"
            : "修复了 " + fixedCount + " 个字体：\n" + sb;
        Debug.Log("[凡人烟火] " + report);
        EditorUtility.DisplayDialog("修复 TMP 字体", report, "确定");
    }

    /// <summary>
    /// 全项目修复 TMP 文本组件的材质引用。
    /// 适用症状：字体已经修好，但部分 TMPText 在 Game/Scene 里仍然不显示 ——
    /// 组件上的材质还是字体空壳时期 TMP 临时 new 出来、后来被销毁的旧材质（引用已失效）。
    /// 注意：TMP Inspector 的 Material Preset 下拉在组件材质为空时会照常显示第一项的名字，
    /// 看起来"有材质"，实际引用是空的，别被它骗了。
    /// </summary>
    [MenuItem("MrTool/修复 TMP 文本材质引用（场景 + 预制体）", false, 39)]
    static void RepairTextMaterialsMenu()
    {
        StringBuilder sb = new StringBuilder();
        int sceneFixed = 0, prefabFixed = 0, prefabFiles = 0;

        try
        {
            // ---------- 1) 所有已加载的场景 ----------
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene sc = SceneManager.GetSceneAt(i);
                if (!sc.IsValid() || !sc.isLoaded) continue;

                int n = 0;
                GameObject[] roots = sc.GetRootGameObjects();
                for (int r = 0; r < roots.Length; r++)
                {
                    TMP_Text[] ts = roots[r].GetComponentsInChildren<TMP_Text>(true);
                    for (int k = 0; k < ts.Length; k++)
                        if (FixTextMaterial(ts[k])) n++;
                }

                if (n > 0)
                {
                    EditorSceneManager.MarkSceneDirty(sc);
                    EditorSceneManager.SaveScene(sc);
                }
                sceneFixed += n;
                sb.AppendLine("场景 " + sc.path + "：修复 " + n + " 个");
            }

            // ---------- 2) Assets 下所有 .prefab 资源 ----------
            string[] guids = AssetDatabase.FindAssets("t:Prefab");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)) continue;

                if (i % 20 == 0 && EditorUtility.DisplayCancelableProgressBar("修复预制体材质引用", path,
                        (float)i / Mathf.Max(1, guids.Length)))
                { sb.AppendLine("（已取消）"); break; }

                GameObject prefab = null;
                try
                {
                    prefab = PrefabUtility.LoadPrefabContents(path);
                    if (prefab == null) continue;

                    int n = 0;
                    TMP_Text[] ts = prefab.GetComponentsInChildren<TMP_Text>(true);
                    for (int k = 0; k < ts.Length; k++)
                        if (FixTextMaterial(ts[k])) n++;

                    if (n > 0)
                    {
                        PrefabUtility.SavePrefabAsset(prefab);
                        prefabFiles++;
                        prefabFixed += n;
                        sb.AppendLine("预制体 " + path + "：修复 " + n + " 个");
                    }
                }
                catch (Exception e) { sb.AppendLine("× " + path + "：" + e.Message); }
                finally
                {
                    if (prefab != null)
                    {
                        try { PrefabUtility.UnloadPrefabContents(prefab); }
                        catch { /* 已卸载则忽略 */ }
                    }
                }
            }
        }
        finally { EditorUtility.ClearProgressBar(); }

        string report = "材质引用修复完成：场景 " + sceneFixed + " 个，预制体 " + prefabFixed +
                        " 个（" + prefabFiles + " 个文件）。\n" + sb;
        Debug.Log("[凡人烟火] " + report);
        EditorUtility.DisplayDialog("修复 TMP 文本材质", report, "确定");
    }

    /// <summary>
    /// 修复单个 TMP 文本组件的材质：材质为空 / 已销毁 / 图集对不上字体的，
    /// 一律重新挂上字体自己的默认材质。返回是否真的改了。
    /// </summary>
    static bool FixTextMaterial(TMP_Text t)
    {
        if (IsDead(t)) return false;

        TMP_FontAsset fa = t.font;
        if (IsDead(fa)) return false;   // 字体都没挂的，归"替换字体"管，这里不处理

        // 字体本身没默认材质的话，顺手把字体也修了
        Material should = null;
        try { should = fa.material; } catch { should = null; }
        if (IsDead(should))
        {
            RepairFontAsset(fa);
            try { should = fa.material; } catch { should = null; }
        }
        if (IsDead(should)) return false;

        Material cur = null;
        try { cur = t.fontSharedMaterial; } catch { cur = null; }

        if (!IsDead(cur))
        {
            if (cur.GetInstanceID() == should.GetInstanceID()) return false;   // 已经是默认材质

            // 图集对得上字体的（描边 / 发光等 preset 材质）→ 正常，保留
            Texture atlas = (fa.atlasTextures != null && fa.atlasTextures.Length > 0) ? fa.atlasTextures[0] : null;
            Texture curTex = null;
            try { curTex = cur.GetTexture(TMPro.ShaderUtilities.ID_MainTex); } catch { curTex = null; }
            if (curTex != null && atlas != null && curTex.GetInstanceID() == atlas.GetInstanceID())
                return false;
        }

        t.fontSharedMaterial = should;   // 走 TMP 的 SetSharedMaterial，同时刷新 CanvasRenderer
        EditorUtility.SetDirty(t);
        return true;
    }

    [MenuItem("CONTEXT/TMP_Text/替换字体（MrTool）", false, 10)]
    static void OpenFromComponent(MenuCommand command)
    {
        TMP_Text t = command.context as TMP_Text;
        if (t == null) return;
        Selection.activeGameObject = t.gameObject;
        TMPFontReplacer w = GetWindow<TMPFontReplacer>("替换 TMP 字体");
        w.minSize = new Vector2(560, 540);
        w._mode = Mode.Selection;
        w.Show();
    }

    void OnEnable()
    {
        _target = LoadSaved(PrefKeyTarget);
        _filter = LoadSaved(PrefKeyFilter);
        if (_target == null)
            _target = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DefaultFontPath);
        if (_target == null)
            _target = FindFirstFontAsset();
    }

    void OnDisable()
    {
        SaveRef(PrefKeyTarget, _target);
        SaveRef(PrefKeyFilter, _filter);
    }

    static TMP_FontAsset LoadSaved(string key)
    {
        string p = EditorPrefs.GetString(key, "");
        return string.IsNullOrEmpty(p) ? null : AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(p);
    }

    static void SaveRef(string key, TMP_FontAsset f)
    {
        if (f != null) EditorPrefs.SetString(key, AssetDatabase.GetAssetPath(f));
        else EditorPrefs.DeleteKey(key);
    }

    /// <summary>兜底默认字体：优先 Assets/Font 下的 TMP 字体资源。</summary>
    static TMP_FontAsset FindFirstFontAsset()
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
        EditorGUILayout.LabelField("TMP 字体批量替换", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "把 TMP 文本组件（TextMeshProUGUI / TextMeshPro）的字体统一替换掉。\n" +
            "· 选中节点：Hierarchy 选中项的所有子节点；Project 里选 .prefab 或文件夹同样有效；\n" +
            "· 当前场景：当前活动场景中的全部 TMP 文本；\n" +
            "· 整个项目：Assets 下所有 .prefab 资源（场景需单独用上一项处理）；\n" +
            "· 场景改动可 Ctrl+Z 撤销，预制体资源直接写文件，改前请先提交版本控制。", MessageType.Info);

        EditorGUILayout.Space();

        // ---------------- 字体 ----------------
        EditorGUILayout.LabelField("字体", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        _target = (TMP_FontAsset)EditorGUILayout.ObjectField("目标字体", _target, typeof(TMP_FontAsset), false);
        if (EditorGUI.EndChangeCheck()) SaveRef(PrefKeyTarget, _target);

        _filter = (TMP_FontAsset)EditorGUILayout.ObjectField("仅替换该字体（留空=全部）", _filter, typeof(TMP_FontAsset), false);
        EditorGUILayout.LabelField("      留空时，所有不是目标字体的 TMP 文本都会被换掉（含字体为空的）", EditorStyles.miniLabel);

        if (_target == null)
            EditorGUILayout.HelpBox("未指定目标字体。先在 Project 里选中 .ttf，执行 MrTool/Convert TTF to TMP Font 生成字体资源。", MessageType.Warning);
        else
        {
            string why;
            if (!IsFontHealthy(_target, out why))
            {
                EditorGUILayout.HelpBox("目标字体不可用：" + why + "\n" +
                                        "这种空壳资源赋给 TMP 会直接抛 UnassignedReferenceException，请先修复或换一个字体。",
                                        MessageType.Error);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("自动修复该字体（补字符 + 生成图集）", GUILayout.Height(24)))
                {
                    TMP_FontAsset f = _target;
                    TMPFontReplacer self = this;
                    EditorApplication.delayCall += delegate
                    {
                        string msg = RepairFontAsset(f);
                        Debug.Log("[凡人烟火] " + msg);
                        self._status = msg;
                        self.Repaint();
                    };
                }
                if (GUILayout.Button("打开 TMP Font Asset Creator", GUILayout.Width(180), GUILayout.Height(24)))
                    EditorApplication.ExecuteMenuItem("Window/TextMeshPro/Font Asset Creator");
                EditorGUILayout.EndHorizontal();
            }
            else EditorGUILayout.LabelField("      字体体检：正常（字符 " + CharCountOf(_target) + " 个，图集已就绪）", EditorStyles.miniLabel);
        }

        EditorGUILayout.Space();

        // ---------------- 范围 ----------------
        EditorGUILayout.LabelField("范围", EditorStyles.boldLabel);
        _mode = (Mode)GUILayout.Toolbar((int)_mode, ModeNames, GUILayout.Height(24));
        EditorGUILayout.LabelField(DescribeScope(), EditorStyles.miniLabel);

        EditorGUILayout.Space();

        // ---------------- 选项 ----------------
        EditorGUILayout.LabelField("选项", EditorStyles.boldLabel);
        _includeInactive = EditorGUILayout.Toggle("包含未激活对象", _includeInactive);
        _autoSave = EditorGUILayout.Toggle("替换后自动保存", _autoSave);
        _setAsDefault = EditorGUILayout.Toggle("同时设为 TMP Settings 默认字体", _setAsDefault);
        EditorGUILayout.LabelField("      默认字体影响新建文本与缺字回退，勾选后新文本自动用该字体", EditorStyles.miniLabel);

        EditorGUILayout.Space();

        bool ready = _target != null;
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("扫描预览（不改动）", GUILayout.Height(26))) _pending = Pending.Scan;
        EditorGUI.BeginDisabledGroup(!ready);
        if (GUILayout.Button("执行替换", GUILayout.Height(26))) _pending = Pending.Replace;
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
        EditorGUILayout.TextArea(_status, EditorStyles.textArea, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();

        // 布局全部结束后再派发实际操作
        if (_pending != Pending.None)
        {
            Pending p = _pending;
            _pending = Pending.None;
            EditorApplication.delayCall += delegate { Execute(p); };
        }
    }

    void Execute(Pending p)
    {
        try
        {
            _status = p == Pending.Scan ? DoScan() : DoReplace();
        }
        catch (Exception e)
        {
            _status = "出错了：\n" + e;
            Debug.LogException(e);
        }
        finally { EditorUtility.ClearProgressBar(); }
        Repaint();
    }

    string DescribeScope()
    {
        switch (_mode)
        {
            case Mode.Scene:
                Scene sc = SceneManager.GetActiveScene();
                return sc.IsValid() ? "      当前场景：" + sc.path + "（未保存的新场景也能改）" : "      没有已加载的场景";
            case Mode.Project:
                return "      扫描 Assets 下所有 .prefab 资源";
            default:
                return "      当前选中：" + DescribeSelection();
        }
    }

    static string DescribeSelection()
    {
        if (Selection.objects == null || Selection.objects.Length == 0) return "（没有选中任何对象）";
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < Selection.objects.Length && i < 4; i++)
        {
            UnityEngine.Object o = Selection.objects[i];
            if (o == null) continue;
            if (sb.Length > 0) sb.Append("、");
            string p = AssetDatabase.GetAssetPath(o);
            sb.Append(string.IsNullOrEmpty(p) ? o.name : p);
        }
        if (Selection.objects.Length > 4) sb.Append(" 等 ").Append(Selection.objects.Length).Append(" 项");
        return sb.ToString();
    }

    // ============================================================
    // 收集目标
    // ============================================================

    /// <summary>选中模式下需要处理的场景根对象。</summary>
    static List<GameObject> CollectSelectionRoots()
    {
        List<GameObject> roots = new List<GameObject>();
        GameObject[] gos = Selection.gameObjects;
        for (int i = 0; i < gos.Length; i++)
            if (gos[i] != null) roots.Add(gos[i]);
        // 只选了 Project 资源时 gameObjects 为空，由 CollectPrefabAssets 处理
        return roots;
    }

    /// <summary>选中模式 / 项目模式下需要处理的 .prefab 资源路径。</summary>
    List<string> CollectPrefabAssets()
    {
        List<string> paths = new List<string>();
        switch (_mode)
        {
            case Mode.Project:
                {
                    string[] guids = AssetDatabase.FindAssets("t:Prefab");
                    for (int i = 0; i < guids.Length; i++)
                    {
                        string p = AssetDatabase.GUIDToAssetPath(guids[i]);
                        if (p.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)) paths.Add(p);
                    }
                    break;
                }
            case Mode.Selection:
                {
                    for (int i = 0; i < Selection.objects.Length; i++)
                    {
                        UnityEngine.Object o = Selection.objects[i];
                        if (o == null) continue;
                        if (o is GameObject)
                        {
                            string p = AssetDatabase.GetAssetPath(o);
                            if (!string.IsNullOrEmpty(p) && p.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                                paths.Add(p);
                            continue;
                        }
                        // Project 里选中的文件夹：搜出它下面所有预制体
                        string dir = AssetDatabase.GetAssetPath(o);
                        if (string.IsNullOrEmpty(dir)) continue;
                        if (!AssetDatabase.IsValidFolder(dir)) continue;
                        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { dir });
                        for (int k = 0; k < guids.Length; k++)
                        {
                            string p = AssetDatabase.GUIDToAssetPath(guids[k]);
                            if (p.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)) paths.Add(p);
                        }
                    }
                    break;
                }
        }

        // 去重
        List<string> uniq = new List<string>();
        HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < paths.Count; i++)
            if (seen.Add(paths[i])) uniq.Add(paths[i]);
        return uniq;
    }

    /// <summary>收集一个 GameObject（含子节点）上所有需要替换的 TMP_Text。</summary>
    static void Collect(GameObject root, bool includeInactive, TMP_FontAsset filter, TMP_FontAsset target, List<TMP_Text> outList)
    {
        if (root == null) return;
        TMP_Text[] ts = root.GetComponentsInChildren<TMP_Text>(includeInactive);
        for (int i = 0; i < ts.Length; i++)
            if (Match(ts[i], filter, target)) outList.Add(ts[i]);
    }

    /// <summary>命中判定：filter 为空 = 所有不是目标字体的都换（含字体为空的）。</summary>
    static bool Match(TMP_Text t, TMP_FontAsset filter, TMP_FontAsset target)
    {
        if (t == null) return false;
        if (filter != null) return t.font == filter;
        return t.font != target;
    }

    static List<TMP_Text> CollectSceneTexts(Scene scene, bool includeInactive, TMP_FontAsset filter, TMP_FontAsset target)
    {
        List<TMP_Text> list = new List<TMP_Text>();
        if (!scene.IsValid() || !scene.isLoaded) return list;
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++) Collect(roots[i], includeInactive, filter, target, list);
        return list;
    }

    // ============================================================
    // 单个组件替换
    // ============================================================

    static bool ApplyFont(TMP_Text t, TMP_FontAsset target)
    {
        if (t == null || target == null) return false;
        if (t.font == target) return false;

        // 原来用的是旧字体的默认材质 → 跟着换；自定义材质（描边等）保留
        Material oldMat = t.fontSharedMaterial;
        bool useDefaultMat = oldMat == null || (t.font != null && oldMat == t.font.material);

        Undo.RecordObject(t, "Replace TMP Font");
        t.font = target;
        if (useDefaultMat) t.fontSharedMaterial = target.material;

        EditorUtility.SetDirty(t);
        return true;
    }

    static bool ApplyFontNoUndo(TMP_Text t, TMP_FontAsset target)
    {
        if (t == null || target == null) return false;
        if (t.font == target) return false;

        Material oldMat = t.fontSharedMaterial;
        bool useDefaultMat = oldMat == null || (t.font != null && oldMat == t.font.material);

        t.font = target;
        if (useDefaultMat) t.fontSharedMaterial = target.material;

        EditorUtility.SetDirty(t);
        return true;
    }

    // ============================================================
    // 扫描预览
    // ============================================================

    string DoScan()
    {
        if (_target == null) return "请先指定目标字体。";
        string why;
        if (!IsFontHealthy(_target, out why)) return "目标字体不可用：" + why + "\n请先在窗口上方点「自动修复该字体」，或改用 Assets/Font 下正常的 SDF 字体。";

        StringBuilder sb = new StringBuilder();
        int total = 0;

        try
        {
            if (_mode == Mode.Selection || _mode == Mode.Scene)
            {
                List<GameObject> roots = new List<GameObject>();
                if (_mode == Mode.Selection) roots = CollectSelectionRoots();
                else
                {
                    Scene sc = SceneManager.GetActiveScene();
                    if (!sc.IsValid()) return "当前没有已加载的场景。";
                    roots.AddRange(sc.GetRootGameObjects());
                }

                List<TMP_Text> texts = new List<TMP_Text>();
                for (int i = 0; i < roots.Count; i++) Collect(roots[i], _includeInactive, _filter, _target, texts);

                sb.AppendLine("场景内命中 " + texts.Count + " 个 TMP 文本组件：");
                for (int i = 0; i < texts.Count && i < 30; i++)
                    sb.AppendLine("  · " + GetPath(texts[i].transform) + "　[" + NameOf(texts[i].font) + " → " + _target.name + "]");
                if (texts.Count > 30) sb.AppendLine("  …… 其余 " + (texts.Count - 30) + " 个未列出");
                total += texts.Count;
            }

            if (_mode == Mode.Selection || _mode == Mode.Project)
            {
                List<string> paths = CollectPrefabAssets();
                sb.AppendLine("预制体资源 " + paths.Count + " 个：");
                for (int i = 0; i < paths.Count; i++)
                {
                    if (i % 40 == 0 && EditorUtility.DisplayCancelableProgressBar("扫描预制体", paths[i], (float)i / Mathf.Max(1, paths.Count)))
                    { sb.AppendLine("（已取消）"); break; }

                    GameObject prefab = PrefabUtility.LoadPrefabContents(paths[i]);
                    if (prefab == null) continue;
                    List<TMP_Text> texts = new List<TMP_Text>();
                    try { Collect(prefab, _includeInactive, _filter, _target, texts); }
                    finally { PrefabUtility.UnloadPrefabContents(prefab); }

                    // 输出做个上限，避免上千个预制体把 TextArea 撑爆
                    if (texts.Count > 0 && sb.Length < 20000) sb.AppendLine("  · " + paths[i] + "　" + texts.Count + " 个");
                    total += texts.Count;
                }
                if (paths.Count == 0) sb.AppendLine("  （没有找到 .prefab；选中模式下请在 Hierarchy 选节点，或在 Project 选 .prefab / 文件夹）");
            }
        }
        finally { EditorUtility.ClearProgressBar(); }

        sb.Insert(0, "扫描完成，共 " + total + " 个组件会被替换。\n\n");
        return sb.ToString();
    }

    // ============================================================
    // 执行替换
    // ============================================================

    string DoReplace()
    {
        if (_target == null) return "请先指定目标字体。";
        string why;
        if (!IsFontHealthy(_target, out why)) return "已终止：目标字体不可用（" + why + "）。\n请先修复字体再替换，否则赋值时会抛 UnassignedReferenceException。";

        StringBuilder sb = new StringBuilder();
        int totalText = 0, totalPrefab = 0;

        try
        {
            // ---------- 场景部分 ----------
            bool dirtyScene = false;
            Scene scene = SceneManager.GetActiveScene();
            if (_mode == Mode.Selection || _mode == Mode.Scene)
            {
                List<GameObject> roots = new List<GameObject>();
                if (_mode == Mode.Selection) roots = CollectSelectionRoots();
                else
                {
                    if (!scene.IsValid()) return "当前没有已加载的场景。";
                    roots.AddRange(scene.GetRootGameObjects());
                }

                List<TMP_Text> texts = new List<TMP_Text>();
                for (int i = 0; i < roots.Count; i++) Collect(roots[i], _includeInactive, _filter, _target, texts);

                int n = 0;
                for (int i = 0; i < texts.Count; i++)
                    if (ApplyFont(texts[i], _target)) n++;

                totalText = n;
                dirtyScene = n > 0;
                sb.AppendLine("场景内替换：" + n + " 个 TMP 文本组件");

                if (n > 0 && scene.IsValid())
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (_autoSave) EditorSceneManager.SaveScene(scene);
                }
            }

            // ---------- 预制体资源部分 ----------
            if (_mode == Mode.Selection || _mode == Mode.Project)
            {
                List<string> paths = CollectPrefabAssets();
                for (int i = 0; i < paths.Count; i++)
                {
                    if (i % 20 == 0 && EditorUtility.DisplayCancelableProgressBar("替换预制体字体", paths[i], (float)i / Mathf.Max(1, paths.Count)))
                    { sb.AppendLine("（已取消）"); break; }

                    int n = ReplacePrefabFile(paths[i], sb);
                    if (n > 0) totalPrefab++;
                    totalText += n;
                }
                sb.Insert(0, "预制体资源：改动 " + totalPrefab + " 个文件\n");
            }

            if (_setAsDefault)
            {
                TMP_Settings settings = TMP_Settings.instance;
                if (settings != null)
                {
                    // 走序列化字段，兼容不同 TMP 版本的 defaultFontAsset 定义
                    SerializedObject so = new SerializedObject(settings);
                    SerializedProperty p = so.FindProperty("m_defaultFontAsset");
                    if (p != null)
                    {
                        p.objectReferenceValue = _target;
                        so.ApplyModifiedPropertiesWithoutUndo();
                        EditorUtility.SetDirty(settings);
                        AssetDatabase.SaveAssets();
                        sb.AppendLine("已把【" + _target.name + "】设为 TMP Settings 默认字体。");
                    }
                    else sb.AppendLine("（TMP Settings 上没有 m_defaultFontAsset 字段，未设置默认字体）");
                }
                else sb.AppendLine("（找不到 TMP Settings，未设置默认字体）");
            }

            if (!_autoSave && dirtyScene) sb.AppendLine("提示：未开启自动保存，场景改动请手动 Ctrl+S 保存。");
        }
        finally { EditorUtility.ClearProgressBar(); }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        sb.Insert(0, "替换完成：共 " + totalText + " 个 TMP 文本组件，预制体 " + totalPrefab + " 个。\n");
        Debug.Log("[凡人烟火] TMP 字体替换完成：" + totalText + " 个组件 → " + _target.name);
        return sb.ToString();
    }

    // ============================================================
    // 预制体文件：替换 + 保存（带回退）
    // ============================================================

    /// <summary>替换单个 .prefab 文件里的字体，返回实际改掉的组件数。</summary>
    int ReplacePrefabFile(string path, StringBuilder log)
    {
        GameObject prefab = null;
        try
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) return 0;

            PrefabAssetType type = PrefabUtility.GetPrefabAssetType(asset);
            if (type == PrefabAssetType.MissingAsset || type == PrefabAssetType.Model)
            {
                log.AppendLine("  × " + path + "　跳过（" + type + " 资源不可写）");
                return 0;
            }

            prefab = PrefabUtility.LoadPrefabContents(path);
            if (prefab == null) return 0;

            List<TMP_Text> texts = new List<TMP_Text>();
            Collect(prefab, _includeInactive, _filter, _target, texts);
            int n = 0;
            for (int k = 0; k < texts.Count; k++)
                if (ApplyFontNoUndo(texts[k], _target)) n++;
            if (n == 0) return 0;

            // 变体 / 只读等资源上 SavePrefabAsset 可能抛 "Can't save a Prefab instance"，
            // 这时回退成直接改写资源本体，保证改动不丢、也不中断整批任务。
            bool saved = false;
            try { PrefabUtility.SavePrefabAsset(prefab); saved = true; }
            catch (Exception e)
            {
                Debug.LogWarning("[凡人烟火] SavePrefabAsset 失败（" + path + "）：" + e.Message + "\n改用直接改写资源本体。");
                n = ApplyOnAsset(path);
            }

            log.AppendLine("  · " + path + "　" + n + " 个" + (saved ? "" : "（回退写入）"));
            return n;
        }
        catch (Exception e)
        {
            log.AppendLine("  × " + path + "　处理失败：" + e.Message);
            return 0;
        }
        finally
        {
            if (prefab != null)
            {
                try { PrefabUtility.UnloadPrefabContents(prefab); }
                catch { /* 已卸载则忽略 */ }
            }
        }
    }

    /// <summary>回退方案：直接在 .prefab 资源本体上改，靠 SetDirty + SaveAssets 落盘。</summary>
    int ApplyOnAsset(string path)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null) return 0;

        List<TMP_Text> texts = new List<TMP_Text>();
        Collect(asset, _includeInactive, _filter, _target, texts);
        int n = 0;
        for (int i = 0; i < texts.Count; i++)
            if (ApplyFontNoUndo(texts[i], _target)) n++;
        if (n > 0) EditorUtility.SetDirty(asset);
        return n;
    }

    // ============================================================
    // 小工具
    // ============================================================

    static string NameOf(TMP_FontAsset f) { return f == null ? "（空）" : f.name; }

    // ============================================================
    // 字体体检 / 修复
    // ============================================================

    /// <summary>
    /// 字体能不能安全地赋给 TMP 组件。空壳资源（无图集且无字符表）赋值会直接抛异常。
    /// </summary>
    public static bool IsFontHealthy(TMP_FontAsset f, out string reason)
    {
        reason = "";
        if (f == null) { reason = "没有指定字体"; return false; }

        SerializedObject so = new SerializedObject(f);
        SerializedProperty atlases = so.FindProperty("m_AtlasTextures");
        SerializedProperty chars = so.FindProperty("m_CharacterTable");

        bool hasAtlas = false;
        if (atlases != null && atlases.isArray)
            for (int i = 0; i < atlases.arraySize; i++)
            {
                SerializedProperty e = atlases.GetArrayElementAtIndex(i);
                if (e != null && e.objectReferenceValue != null) { hasAtlas = true; break; }
            }
        int charCount = (chars != null && chars.isArray) ? chars.arraySize : 0;

        if (!hasAtlas && charCount == 0) { reason = "资源是空壳：没有图集纹理，字符表也是空的（多半是生成后没补过字符）"; return false; }
        if (!hasAtlas) { reason = "缺少图集纹理（m_AtlasTextures 未分配）"; return false; }

        // TMP 用字体自身的默认材质渲染；材质丢了，TMPText 会临时 new 一个
        // "TextMeshPro/Mobile/Distance Field" 材质（没有图集 → 文字全空白），
        // 而这个临时材质被销毁后，TMP 的 Inspector 还会去读它 → MissingReferenceException。
        SerializedProperty matProp = so.FindProperty("material");
        Material mat = matProp != null ? matProp.objectReferenceValue as Material : f.material;
        if (IsDead(mat))
            reason = "缺少默认材质（字体没有 material，或材质已被销毁）";
        else
        {
            Texture2D atlas = (f.atlasTextures != null && f.atlasTextures.Length > 0) ? f.atlasTextures[0] : null;
            if (atlas != null && mat.GetTexture(TMPro.ShaderUtilities.ID_MainTex) != atlas)
                reason = "默认材质上没有挂图集纹理";
        }
        return string.IsNullOrEmpty(reason);
    }

    /// <summary>Unity 对象是否已失效（null，或已被 Destroy 但引用还在）。</summary>
    static bool IsDead(UnityEngine.Object o)
    {
        if (o == null) return true;
        try { string n = o.name; return false; }
        catch (MissingReferenceException) { return true; }
        catch (Exception) { return true; }
    }

    static int CharCountOf(TMP_FontAsset f)
    {
        if (f == null) return 0;
        SerializedProperty chars = new SerializedObject(f).FindProperty("m_CharacterTable");
        return (chars != null && chars.isArray) ? chars.arraySize : 0;
    }

    /// <summary>
    /// 修复空壳字体：切成动态图集模式 → 重建图集 → 用常用字补字符 → 把图集和
    /// 默认材质一起存进 .asset。
    ///
    /// 典型症状（都是同一个根因：字体没有自己的默认材质 / 没有图集）：
    ///   · UnassignedReferenceException: The variable material of TMP_FontAsset has not been assigned.
    ///   · MissingReferenceException: The object of type 'Material' has been destroyed.
    ///   · TMPText 上的材质变成 "TextMeshPro/Mobile/Distance Field (Material)"，文字全空白 ——
    ///     那是 TMP 临时 new 出来的材质（名字就是 shader 名），既没挂图集也没落盘。
    /// </summary>
    public static string RepairFontAsset(TMP_FontAsset f)
    {
        if (f == null) return "没有指定字体。";

        try
        {
            // 1) 动态图集：缺字符时自动往图集里加
            SerializedObject so = new SerializedObject(f);
            SerializedProperty pop = so.FindProperty("m_AtlasPopulationMode");
            if (pop != null) pop.intValue = (int)TMPro.AtlasPopulationMode.Dynamic;
            SerializedProperty multi = so.FindProperty("m_IsMultiAtlasTexturesEnabled");
            if (multi != null) multi.boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 2) 图集纹理整个丢了（m_AtlasTextures 是 fileID: 0）：先补一张空图，
            //    再把已有字符表重新光栅化一遍，否则 TMP 补字时会空引用。
            bool atlasMissing = f.atlasTextures == null || f.atlasTextures.Length == 0 || IsDead(f.atlasTextures[0]);
            if (atlasMissing) RebuildAtlas(f);

            // 3) 补字符
            string chars = LoadRepairChars();
            int before = CharCountOf(f);
            f.TryAddCharacters(chars);
            int after = CharCountOf(f);

            // 4) 把新生成的图集纹理持久化到 .asset，并把引用写回 m_AtlasTextures
            //    （只 AddObjectToAsset 不写回的话，序列化里仍然是 fileID: 0，等于没图集）
            int savedTex = PersistAtlases(f);

            // 5) 生成 / 修复字体自己的默认材质，并落盘成子资源
            int matFixed = PersistDefaultMaterial(f);

            string why;
            bool ok = IsFontHealthy(f, out why);
            return string.Format("修复【{0}】：补入字符 {1} 个（字符表 {2} → {3}），新存图集 {4} 张，{5}。\n结果：{6}",
                f.name, chars.Length, before, after, savedTex,
                matFixed > 0 ? "已生成默认材质" : "默认材质已存在",
                ok ? "字体已可用，可以重新执行替换。" : "仍未通过体检（" + why + "），建议用 Window/TextMeshPro/Font Asset Creator 重新生成。");
        }
        catch (Exception e)
        {
            return "修复【" + f.name + "】失败：" + e.Message +
                   "\n建议改用 Window/TextMeshPro/Font Asset Creator 重新生成字体资源。";
        }
    }

    /// <summary>
    /// 图集纹理整个丢了时重建图集：塞一张空 Alpha8 纹理，然后调用 TMP 内部的
    /// UpdateFontAssetData（保留字符表 → 清空表 → 把所有字重新画进新图集）。
    /// </summary>
    static void RebuildAtlas(TMP_FontAsset f)
    {
        int w = f.atlasWidth > 0 ? f.atlasWidth : 1024;
        int h = f.atlasHeight > 0 ? f.atlasHeight : 1024;

        Texture2D tex = new Texture2D(0, 0, TextureFormat.Alpha8, false);
        tex.name = f.name + " Atlas";
        f.atlasTextures = new Texture2D[] { tex };

        MethodInfo mi = typeof(TMP_FontAsset).GetMethod("UpdateFontAssetData", BindingFlags.NonPublic | BindingFlags.Instance);
        if (mi != null) mi.Invoke(f, null);
        else f.ClearFontAssetData(true);

        Debug.Log("[凡人烟火] 重建图集（" + w + "x" + h + "）：" + f.name);
    }

    /// <summary>
    /// 保证字体有可用的默认材质：没有（或已失效）就新建并挂到 .asset 上，
    /// 有的话补齐图集纹理和关键参数，最后把引用写回序列化数据。
    /// 少了这一步，TMPText 只能临时 new 材质凑合 —— 文字不显示、赋值时还会抛
    /// UnassignedReferenceException。
    /// </summary>
    public static int PersistDefaultMaterial(TMP_FontAsset f)
    {
        if (f == null) return 0;
        string path = AssetDatabase.GetAssetPath(f);
        if (string.IsNullOrEmpty(path)) return 0;

        Texture2D atlas = (f.atlasTextures != null && f.atlasTextures.Length > 0) ? f.atlasTextures[0] : null;
        if (atlas == null) return 0;

        Material mat = null;
        try { mat = f.material; } catch { mat = null; }
        bool isNew = false;

        if (IsDead(mat))
        {
            Shader shader = Shader.Find("TextMeshPro/Mobile/Distance Field");
            if (shader == null) shader = Shader.Find("TextMeshPro/Distance Field");
            if (shader == null) { Debug.LogWarning("[凡人烟火] 找不到 TMP 的 Distance Field shader。"); return 0; }

            mat = new Material(shader);
            mat.name = f.name + " Material";
            isNew = true;
        }

        // 关键参数：图集 + 缩放 + 描边权重，缺了文字照样画不出来
        mat.SetTexture(TMPro.ShaderUtilities.ID_MainTex, atlas);
        mat.SetFloat(TMPro.ShaderUtilities.ID_TextureWidth, f.atlasWidth);
        mat.SetFloat(TMPro.ShaderUtilities.ID_TextureHeight, f.atlasHeight);
        if (mat.HasProperty(TMPro.ShaderUtilities.ID_GradientScale))
            mat.SetFloat(TMPro.ShaderUtilities.ID_GradientScale, f.atlasPadding + 1);
        if (mat.HasProperty(TMPro.ShaderUtilities.ID_WeightNormal))
            mat.SetFloat(TMPro.ShaderUtilities.ID_WeightNormal, f.normalStyle);
        if (mat.HasProperty(TMPro.ShaderUtilities.ID_WeightBold))
            mat.SetFloat(TMPro.ShaderUtilities.ID_WeightBold, f.boldStyle);

        if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(mat)))
        {
            try { AssetDatabase.AddObjectToAsset(mat, f); }
            catch (Exception ex) { Debug.LogWarning("[凡人烟火] 材质挂到 " + path + " 失败：" + ex.Message); }
        }

        // 把引用显式写回序列化数据，否则文件里还是 material: {fileID: 0}
        SerializedObject so = new SerializedObject(f);
        SerializedProperty p = so.FindProperty("material");
        if (p != null)
        {
            p.objectReferenceValue = mat;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorUtility.SetDirty(f);
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        // 重新导入一次，清掉内存里那个已销毁的临时材质引用
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        return isNew ? 1 : 0;
    }

    /// <summary>
    /// 把字体当前所有图集纹理挂到 .asset 上并写回 m_AtlasTextures 引用，
    /// 返回处理成功的图集数量。缺了这步，图集不会落盘，字体等于没有图。
    /// </summary>
    public static int PersistAtlases(TMP_FontAsset f)
    {
        if (f == null) return 0;
        string assetPath = AssetDatabase.GetAssetPath(f);
        if (string.IsNullOrEmpty(assetPath)) return 0;

        SerializedObject so = new SerializedObject(f);
        SerializedProperty atlases = so.FindProperty("m_AtlasTextures");
        if (atlases == null || !atlases.isArray) return 0;

        int saved = 0;
        for (int i = 0; i < atlases.arraySize; i++)
        {
            SerializedProperty e = atlases.GetArrayElementAtIndex(i);
            if (e == null) continue;
            Texture2D tex = e.objectReferenceValue as Texture2D;
            if (tex == null) continue;

            if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(tex)))
            {
                try { AssetDatabase.AddObjectToAsset(tex, f); }
                catch (Exception ex) { Debug.LogWarning("[凡人烟火] 图集挂到 " + assetPath + " 失败：" + ex.Message); }
            }
            e.objectReferenceValue = tex;   // 关键：把引用显式写回序列化数据
            saved++;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(f);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return saved;
    }

    /// <summary>修复用的字符集：优先 Font/chinese_characters.txt，再兜底常用字。</summary>
    public static string LoadRepairChars()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append(" !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~");
        sb.Append("　，。、；：？！“”‘’（）《》【】…—·￥%℃×÷±≈∞");

        string path = "Assets/Font/chinese_characters.txt";
        if (File.Exists(path))
        {
            try { sb.Append(File.ReadAllText(path).Replace("\r", "").Replace("\n", "")); }
            catch (Exception e) { Debug.LogWarning("[凡人烟火] 读取 " + path + " 失败：" + e.Message); }
        }

        // 去重
        HashSet<char> seen = new HashSet<char>();
        StringBuilder outSb = new StringBuilder();
        string raw = sb.ToString();
        for (int i = 0; i < raw.Length; i++)
            if (seen.Add(raw[i])) outSb.Append(raw[i]);
        return outSb.ToString();
    }

    static string GetPath(Transform t)
    {
        if (t == null) return "";
        string p = t.name;
        while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
        return p;
    }
}

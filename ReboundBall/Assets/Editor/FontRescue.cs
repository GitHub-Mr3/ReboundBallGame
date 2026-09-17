using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/*
 * 字体急救工具
 *
 * 症状：场景 / 预制体里的 m_fontAsset 指着一个已经不在磁盘上的 GUID（字体文件被删或没提交），
 * TMP 文本全部变成 Missing，UI 上一个字都不显示。
 *
 * 两条路：
 *   1) 重建同 GUID 的字体 —— 用系统 simhei.ttf 重新生成 Assets/Font/SIMHEI SDF.asset，
 *      并把 .meta 的 GUID 写回丢失的那个值，场景和预制体不用动，引用自动全部复活；
 *   2) 重定向引用 —— 直接改 .scene / .prefab 文本，把所有指向丢失 GUID 的 m_fontAsset
 *      换成另一个可用字体的 GUID（适合原字体已经找不回来的情况）。
 *
 * 另外提供"单独修某个字体的图集"（补字符 + 图集落盘）。
 */
public class FontRescue : EditorWindow
{
    const string SystemSimHei = @"C:\Windows\Fonts\simhei.ttf";
    const string DestTtf = "Assets/Font/SIMHEI.TTF";
    const string DestAsset = "Assets/Font/SIMHEI SDF.asset";

    static readonly string[] ScanExt = { ".prefab", ".scene", ".unity", ".asset" };
    static readonly string[] SkipDirs = { "\\TextMesh Pro\\", "\\WX-WASM-SDK-V2\\", "\\UIArt\\" };

    // m_fontAsset: {fileID: 11400000, guid: xxxxxxxx, type: 2}
    static readonly Regex FontRefRx = new Regex(@"(m_fontAsset: \{fileID: -?\d+, guid: )([0-9a-fA-F]{32})(, type: \d+\})");

    class MissingRef
    {
        public string guid;
        public int count;
        public List<string> files = new List<string>();
    }

    TMP_FontAsset _fixTo;
    List<MissingRef> _missing = new List<MissingRef>();
    Vector2 _scroll;
    string _status = "";
    int _pending;   // 1 = 扫描，2 = 重定向，3 = 重建

    // ============================================================
    // 入口
    // ============================================================

    [MenuItem("MrTool/字体急救（修复丢失的字体引用）", false, 37)]
    static void Open()
    {
        FontRescue w = GetWindow<FontRescue>("字体急救");
        w.minSize = new Vector2(620, 560);
        w.Show();
    }

    void OnEnable()
    {
        _fixTo = FindHealthyFont();
        _pending = 1;   // 打开就扫一遍
    }

    /// <summary>默认挑一个可用的字体作为重定向目标。</summary>
    static TMP_FontAsset FindHealthyFont()
    {
        string[] guids = AssetDatabase.FindAssets("t:TMP_FontAsset");
        for (int i = 0; i < guids.Length; i++)
        {
            string p = AssetDatabase.GUIDToAssetPath(guids[i]);
            TMP_FontAsset fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(p);
            string why;
            if (fa != null && TMPFontReplacer.IsFontHealthy(fa, out why)) return fa;
        }
        for (int i = 0; i < guids.Length; i++)
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guids[i]));
        return null;
    }

    // ============================================================
    // 界面
    // ============================================================

    void OnGUI()
    {
        EditorGUILayout.LabelField("字体急救", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "字体文件被删 / 没提交时，场景和预制体里的 m_fontAsset 会指向一个不存在的 GUID，TMP 文本全部变空。\n" +
            "这里扫出所有断链引用，然后：\n" +
            "· 重建同 GUID 字体：用系统 simhei.ttf 重新生成 SIMHEI SDF 并把 GUID 还原，引用自动复活；\n" +
            "· 重定向引用：直接改写 .scene / .prefab 文本，把丢失的 GUID 换成下面选的字体。", MessageType.Info);

        EditorGUILayout.Space();

        EditorGUILayout.LabelField("断链的字体引用", EditorStyles.boldLabel);
        if (_missing.Count == 0)
            EditorGUILayout.LabelField("      暂无（点上面扫描确认一下）", EditorStyles.miniLabel);
        else
        {
            for (int i = 0; i < _missing.Count; i++)
            {
                MissingRef m = _missing[i];
                EditorGUILayout.LabelField("  × GUID " + m.guid + "　被引用 " + m.count + " 次", EditorStyles.boldLabel);
                for (int k = 0; k < m.files.Count && k < 5; k++)
                    EditorGUILayout.LabelField("       " + m.files[k], EditorStyles.miniLabel);
                if (m.files.Count > 5) EditorGUILayout.LabelField("       等 " + m.files.Count + " 个文件", EditorStyles.miniLabel);
            }
        }

        EditorGUILayout.Space();

        EditorGUILayout.LabelField("重定向目标字体", EditorStyles.boldLabel);
        _fixTo = (TMP_FontAsset)EditorGUILayout.ObjectField("换成这个字体", _fixTo, typeof(TMP_FontAsset), false);

        EditorGUILayout.Space();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("重新扫描", GUILayout.Height(26))) _pending = 1;
        EditorGUI.BeginDisabledGroup(_fixTo == null || _missing.Count == 0);
        if (GUILayout.Button("重定向丢失引用 → 该字体", GUILayout.Height(26))) _pending = 2;
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();

        EditorGUI.BeginDisabledGroup(_missing.Count == 0);
        if (GUILayout.Button("用系统 simhei.ttf 重建 SIMHEI SDF 并还原 GUID（推荐）", GUILayout.Height(26))) _pending = 3;
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("单个字体修图集", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("修补选中字体的图集（补字符 + 落盘）", GUILayout.Height(22)))
        {
            TMP_FontAsset sel = Selection.activeObject as TMP_FontAsset;
            if (_fixTo != null && sel == null) sel = _fixTo;
            if (sel == null) { _status = "请先在 Project 里选中一个 TMP 字体资源。"; }
            else
            {
                TMP_FontAsset f = sel;
                EditorApplication.delayCall += delegate
                {
                    string msg = TMPFontReplacer.RepairFontAsset(f);
                    Debug.Log("[凡人烟火] " + msg);
                    _status = msg;
                    Repaint();
                };
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
        EditorGUILayout.TextArea(_status, EditorStyles.textArea, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();

        if (_pending != 0)
        {
            int p = _pending;
            _pending = 0;
            EditorApplication.delayCall += delegate { Execute(p); };
        }
    }

    void Execute(int what)
    {
        try
        {
            switch (what)
            {
                case 1:
                    _missing = ScanMissing();
                    _status = BuildScanReport();
                    break;
                case 2:
                    _status = RedirectAll(_fixTo);
                    _missing = ScanMissing();
                    break;
                case 3:
                    _status = RebuildSimHei();
                    _missing = ScanMissing();
                    break;
            }
        }
        catch (Exception e)
        {
            _status = "出错了：\n" + e;
            Debug.LogException(e);
        }
        finally { EditorUtility.ClearProgressBar(); }
        Repaint();
    }

    string BuildScanReport()
    {
        if (_missing.Count == 0) return "没有发现断链的字体引用，UI 应该能正常显示。若还是空白，检查字体资源本身是否有图集（用下面的「修补选中字体的图集」）。";
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("发现 " + _missing.Count + " 个丢失的字体资源：");
        for (int i = 0; i < _missing.Count; i++)
        {
            sb.AppendLine("  × GUID " + _missing[i].guid + "　引用 " + _missing[i].count + " 处　涉及 " + _missing[i].files.Count + " 个文件");
            for (int k = 0; k < _missing[i].files.Count && k < 20; k++)
                sb.AppendLine("       " + _missing[i].files[k]);
            if (_missing[i].files.Count > 20) sb.AppendLine("       等 " + _missing[i].files.Count + " 个文件");
        }
        sb.AppendLine();
        sb.AppendLine("建议：先试「重建 SIMHEI SDF 并还原 GUID」；重建不了再选一个可用字体做「重定向」。" );
        return sb.ToString();
    }

    // ============================================================
    // 扫描
    // ============================================================

    static List<MissingRef> ScanMissing()
    {
        Dictionary<string, MissingRef> map = new Dictionary<string, MissingRef>();
        List<string> files = CollectFiles();

        for (int i = 0; i < files.Count; i++)
        {
            if (i % 50 == 0 && EditorUtility.DisplayCancelableProgressBar("扫描字体引用", files[i], (float)i / Mathf.Max(1, files.Count)))
                break;

            string text;
            try { text = File.ReadAllText(files[i]); }
            catch { continue; }
            if (text.IndexOf("m_fontAsset", StringComparison.Ordinal) < 0) continue;

            for (Match m = FontRefRx.Match(text); m.Success; m = m.NextMatch())
            {
                string guid = m.Groups[2].Value;
                if (IsGuidAlive(guid)) continue;

                MissingRef r;
                if (!map.TryGetValue(guid, out r)) { r = new MissingRef { guid = guid }; map[guid] = r; }
                r.count++;
                string rel = ToProjectPath(files[i]);
                if (!r.files.Contains(rel)) r.files.Add(rel);
            }
        }
        EditorUtility.ClearProgressBar();

        List<MissingRef> list = new List<MissingRef>();
        foreach (KeyValuePair<string, MissingRef> kv in map) list.Add(kv.Value);
        list.Sort(delegate (MissingRef a, MissingRef b) { return b.count.CompareTo(a.count); });
        return list;
    }

    static bool IsGuidAlive(string guid)
    {
        string p = AssetDatabase.GUIDToAssetPath(guid);
        return !string.IsNullOrEmpty(p) && (File.Exists(p) || Directory.Exists(p));
    }

    static List<string> CollectFiles()
    {
        List<string> files = new List<string>();
        string root = Application.dataPath;
        Stack<string> dirs = new Stack<string>();
        dirs.Push(root);
        while (dirs.Count > 0)
        {
            string dir = dirs.Pop();
            bool skip = false;
            for (int i = 0; i < SkipDirs.Length; i++)
                if (dir.IndexOf(SkipDirs[i], StringComparison.OrdinalIgnoreCase) >= 0) { skip = true; break; }
            if (skip) continue;

            string[] fs;
            try { fs = Directory.GetFiles(dir); }
            catch { continue; }
            for (int i = 0; i < fs.Length; i++)
            {
                string ext = Path.GetExtension(fs[i]).ToLowerInvariant();
                if (Array.IndexOf(ScanExt, ext) >= 0) files.Add(fs[i]);
            }
            string[] ds;
            try { ds = Directory.GetDirectories(dir); }
            catch { continue; }
            for (int i = 0; i < ds.Length; i++) dirs.Push(ds[i]);
        }
        return files;
    }

    static string ToProjectPath(string abs)
    {
        string p = abs.Replace("\\", "/");
        string dataPath = Application.dataPath.Replace("\\", "/");
        if (p.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase)) return "Assets" + p.Substring(dataPath.Length);
        return p;
    }

    // ============================================================
    // 方案一：重建同 GUID 的 SIMHEI SDF
    // ============================================================

    string RebuildSimHei()
    {
        if (_missing.Count == 0) return "没有需要修复的引用。";
        string missingGuid = _missing[0].guid;

        StringBuilder log = new StringBuilder();

        if (!File.Exists(SystemSimHei))
            return "系统里没有 " + SystemSimHei + "。\n请手动把 simhei.ttf 放进 Assets/Font/SIMHEI.TTF 后重试，或改用「重定向丢失引用」。";

        try
        {
            if (!Directory.Exists("Assets/Font")) Directory.CreateDirectory("Assets/Font");

            File.Copy(SystemSimHei, DestTtf, true);
            AssetDatabase.ImportAsset(DestTtf, ImportAssetOptions.ForceSynchronousImport);
            Font font = AssetDatabase.LoadAssetAtPath<Font>(DestTtf);
            if (font == null) return "复制过来的 TTF 没被识别成 Font：" + DestTtf;
            log.AppendLine("源字体：" + DestTtf);

            if (File.Exists(DestAsset)) AssetDatabase.DeleteAsset(DestAsset);

            TMP_FontAsset fa = TMP_FontAsset.CreateFontAsset(font, 64, 6, GlyphRenderMode.SDFAA, 2048, 2048);
            if (fa == null) return "TMP 字体创建失败。";
            fa.name = "SIMHEI SDF";
            AssetDatabase.CreateAsset(fa, DestAsset);
            AssetDatabase.SaveAssets();
            log.AppendLine("已生成：" + DestAsset);

            // 补常用字 + 图集落盘
            SetDynamic(fa);
            string chars = TMPFontReplacer.LoadRepairChars();
            fa.TryAddCharacters(chars);
            TMPFontReplacer.PersistAtlases(fa);
            TMPFontReplacer.PersistDefaultMaterial(fa);

            // 还原 GUID，让老引用自动指向它
            bool ok = SetAssetGuid(DestAsset, missingGuid);
            log.AppendLine(ok
                ? "GUID 已还原为 " + missingGuid + "，场景 / 预制体引用自动恢复。"
                : "GUID 还原未立即生效（" + missingGuid + "）。请关闭并重开 Unity；不行的话改用「重定向丢失引用」。");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        catch (Exception e)
        {
            return "重建失败：" + e.Message;
        }

        log.AppendLine();
        log.AppendLine("提示：如果 UI 仍是空白，重开一次 Unity 让新的字体资源重新导入。");
        Debug.Log("[凡人烟火] " + log);
        return log.ToString();
    }

    static void SetDynamic(TMP_FontAsset fa)
    {
        SerializedObject so = new SerializedObject(fa);
        SerializedProperty pop = so.FindProperty("m_AtlasPopulationMode");
        if (pop != null) pop.intValue = 1;   // Dynamic
        SerializedProperty multi = so.FindProperty("m_IsMultiAtlasTexturesEnabled");
        if (multi != null) multi.boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>改写 .meta 里的 GUID 并强制重新导入。</summary>
    static bool SetAssetGuid(string assetPath, string guid)
    {
        string metaPath = assetPath + ".meta";
        if (!File.Exists(metaPath)) return false;

        string text = File.ReadAllText(metaPath);
        text = Regex.Replace(text, "guid: [0-9a-fA-F]{32}", "guid: " + guid);
        File.WriteAllText(metaPath, text);

        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        AssetDatabase.Refresh();

        string check = AssetDatabase.GUIDToAssetPath(guid);
        return string.Equals(check, assetPath, StringComparison.OrdinalIgnoreCase);
    }

    // ============================================================
    // 方案二：直接改写引用文本
    // ============================================================

    string RedirectAll(TMP_FontAsset to)
    {
        if (to == null) return "请先指定要换成哪个字体。";

        string toGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(to));
        if (string.IsNullOrEmpty(toGuid)) return "取不到目标字体的 GUID。";

        HashSet<string> missingSet = new HashSet<string>();
        for (int i = 0; i < _missing.Count; i++) missingSet.Add(_missing[i].guid);
        if (missingSet.Count == 0) return "没有需要重定向的引用。";

        List<string> files = CollectFiles();
        int fileCount = 0, refCount = 0;
        StringBuilder log = new StringBuilder();

        for (int i = 0; i < files.Count; i++)
        {
            if (i % 50 == 0 && EditorUtility.DisplayCancelableProgressBar("重定向字体引用", files[i], (float)i / Mathf.Max(1, files.Count)))
            { log.AppendLine("（已取消）"); break; }

            string text;
            try { text = File.ReadAllText(files[i]); }
            catch { continue; }
            if (text.IndexOf("m_fontAsset", StringComparison.Ordinal) < 0) continue;

            int n = 0;
            string replaced = FontRefRx.Replace(text, delegate (Match m)
            {
                if (!missingSet.Contains(m.Groups[2].Value)) return m.Value;
                n++;
                return m.Groups[1].Value + toGuid + m.Groups[3].Value;
            });

            if (n == 0) continue;
            try { File.WriteAllText(files[i], replaced); }
            catch (Exception e) { log.AppendLine("  × 写入失败 " + ToProjectPath(files[i]) + "：" + e.Message); continue; }

            fileCount++;
            refCount += n;
            log.AppendLine("  · " + ToProjectPath(files[i]) + "　" + n + " 处");
        }

        AssetDatabase.Refresh();
        log.Insert(0, string.Format("重定向完成：{0} 处引用 / {1} 个文件 → 【{2}】\n\n", refCount, fileCount, to.name));
        log.AppendLine("\n提示：UIBuilder.SDFAssetPath 也要跟着改成新字体路径，运行时新建的文本才不会缺字体。");
        Debug.Log("[凡人烟火] " + log);
        return log.ToString();
    }
}

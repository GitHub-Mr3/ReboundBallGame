using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/*
 * 中文文本导出工具
 * 扫描工程内的脚本(.cs)、预制体(.prefab)、场景(.unity/.scene)与资源(.asset)，
 * 提取其中出现的所有中文文本（脚本字符串字面量 + TMP/UGUI 的 m_text 字段），
 * 去重后导出到自定义的目录与文件名（可覆盖已有文件，也可新建）。两种输出模式：
 *
 *   1) 文本条目：每行一条去重后的文本（内部换行/制表符转义为 \n / \t）；
 *   2) 字符集  ：只保留出现过的字符，每个字符仅出现一次；默认只取非 ASCII 字符、
 *               按 Unicode 码位升序拼成一行，可直接作为 TMP 字体子集的字符集文件。
 *
 * 去重规则：文本级去重统一去掉首尾空白，可选忽略空格/换行差异（保留空白更少的写法）。
 */
public class ChineseTextExporter : EditorWindow
{
    enum OutputMode { Text = 0, Chars = 1 }

    const string PrefKeyDir = "MrTool.ChineseTextExporter.Dir";
    const string PrefKeyFile = "MrTool.ChineseTextExporter.File";
    const string DefaultFolderName = "ChineseTextExport";
    const string DefaultFileName = "ChineseText.txt";

    string _outputDir;
    string _fileName = DefaultFileName;
    OutputMode _mode = OutputMode.Chars;
    bool _includeScripts = true;
    bool _includeEditorScripts = false;
    bool _includePrefabsScenes = true;
    bool _includeAssets = false;
    bool _sortResult = false;
    bool _ignoreWhitespace = true;
    bool _charsNonAsciiOnly = true;
    bool _charsSortByCodePoint = true;
    bool _charsOnePerLine = false;

    Vector2 _scroll;
    string _status = "";

    static string ProjectRoot { get { return Directory.GetParent(Application.dataPath).FullName; } }
    static string DefaultDir { get { return Path.Combine(ProjectRoot, DefaultFolderName); } }

    [MenuItem("MrTool/导出中文文本（去重）", false, 40)]
    static void Open()
    {
        ChineseTextExporter w = GetWindow<ChineseTextExporter>("中文文本导出");
        w.minSize = new Vector2(560, 460);
        w.Show();
    }

    void OnEnable()
    {
        _outputDir = EditorPrefs.GetString(PrefKeyDir, DefaultDir);
        if (string.IsNullOrEmpty(_outputDir)) _outputDir = DefaultDir;

        _fileName = EditorPrefs.GetString(PrefKeyFile, DefaultFileName);
        if (string.IsNullOrEmpty(_fileName)) _fileName = DefaultFileName;
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("导出游戏中的中文文本 / 字符集（去重）", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "扫描脚本字符串字面量，以及预制体/场景中 TMP、UGUI 的文本字段。\n" +
            "· 文本条目：每行一条去重文本；\n" +
            "· 字符集：全文件每个字符只出现一次（适合做字体子集）。", MessageType.Info);

        EditorGUILayout.Space();

        // ---------------- 输出位置 ----------------
        EditorGUILayout.BeginHorizontal();
        _outputDir = EditorGUILayout.TextField("输出目录", _outputDir);
        if (GUILayout.Button("选择…", GUILayout.Width(64)))
        {
            string start = Directory.Exists(_outputDir) ? _outputDir : ProjectRoot;
            string picked = EditorUtility.OpenFolderPanel("选择导出目录", start, "");
            if (!string.IsNullOrEmpty(picked))
            {
                _outputDir = CleanDir(picked);
                SavePrefs();
            }
        }
        if (GUILayout.Button("默认", GUILayout.Width(56)))
        {
            _outputDir = DefaultDir;
            _fileName = DefaultFileName;
            SavePrefs();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        _fileName = EditorGUILayout.TextField("输出文件名", _fileName);
        if (GUILayout.Button("另存为…", GUILayout.Width(64)))
        {
            string start = Directory.Exists(_outputDir) ? _outputDir : ProjectRoot;
            string picked = EditorUtility.SaveFilePanel("选择导出文件（可直接选已有文件覆盖）",
                start, string.IsNullOrEmpty(_fileName) ? DefaultFileName : _fileName, "txt");
            if (!string.IsNullOrEmpty(picked))
            {
                _outputDir = CleanDir(Path.GetDirectoryName(picked));
                _fileName = Path.GetFileName(picked);
                SavePrefs();
            }
        }
        EditorGUILayout.EndHorizontal();

        string previewPath = ResolveOutputPath();
        if (string.IsNullOrEmpty(previewPath))
            EditorGUILayout.LabelField("请填写输出目录与文件名。", EditorStyles.miniLabel);
        else if (File.Exists(previewPath))
            EditorGUILayout.LabelField("将覆盖已有文件：" + previewPath, EditorStyles.miniLabel);
        else
            EditorGUILayout.LabelField("将新建文件：" + previewPath, EditorStyles.miniLabel);

        EditorGUILayout.Space();

        string[] modeNames = { "文本条目（每行一条文本）", "字符集（每个字符只出现一次）" };
        _mode = (OutputMode)EditorGUILayout.Popup("输出模式", (int)_mode, modeNames);

        EditorGUILayout.Space();

        _includeScripts = EditorGUILayout.Toggle("脚本 (*.cs)", _includeScripts);
        _includeEditorScripts = EditorGUILayout.Toggle("  包含 Editor 目录脚本", _includeEditorScripts);
        _includePrefabsScenes = EditorGUILayout.Toggle("预制体 / 场景 (*.prefab, *.unity, *.scene)", _includePrefabsScenes);
        _includeAssets = EditorGUILayout.Toggle("资源 (*.asset)", _includeAssets);

        EditorGUILayout.Space();

        if (_mode == OutputMode.Chars)
        {
            _charsNonAsciiOnly = EditorGUILayout.Toggle("仅非 ASCII 字符（码位 ≥ U+0080）", _charsNonAsciiOnly);
            _charsSortByCodePoint = EditorGUILayout.Toggle("按 Unicode 码位升序", _charsSortByCodePoint);
            _charsOnePerLine = EditorGUILayout.Toggle("每行一个字符", _charsOnePerLine);
            _ignoreWhitespace = EditorGUILayout.Toggle("文本去重时忽略空格差异", _ignoreWhitespace);
            EditorGUILayout.LabelField("      关闭「每行一个字符」即输出单行字符集", EditorStyles.miniLabel);
        }
        else
        {
            _sortResult = EditorGUILayout.Toggle("结果按字典序排序", _sortResult);
            _ignoreWhitespace = EditorGUILayout.Toggle("忽略空白差异去重（空格/换行）", _ignoreWhitespace);
            EditorGUILayout.LabelField("      「玩 法 说 明」与「玩法说明」会合并为一条", EditorStyles.miniLabel);
        }

        EditorGUILayout.Space();

        if (GUILayout.Button("扫描并导出", GUILayout.Height(30)))
        {
            _status = DoExport();
            Repaint();
        }

        EditorGUILayout.Space();
        _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
        EditorGUILayout.TextArea(_status, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    /// <summary>供 -executeMethod 调用：字符集模式，输出到默认目录的默认文件名。</summary>
    public static void ExportToDefaultDirectory()
    {
        int fileCount;
        Collector collector = Collect(true, false, true, false, true, true, out fileCount);
        List<string> lines = BuildOutput(collector, OutputMode.Chars, false, true, false);
        string path = WriteOut(lines, Path.Combine(DefaultDir, DefaultFileName));
        Debug.Log("[中文文本导出] 完成：" + path + "（字符数 " + collector.CharUnits.Count + "）");
    }

    string DoExport()
    {
        string outPath = ResolveOutputPath();
        if (string.IsNullOrEmpty(outPath))
            return "请先填写输出目录与文件名。";

        bool any = _includeScripts || _includePrefabsScenes || _includeAssets;
        if (!any) return "请至少勾选一种要扫描的文件类型。";

        int fileCount;
        Collector collector = Collect(_includeScripts, _includeEditorScripts, _includePrefabsScenes,
            _includeAssets, _ignoreWhitespace, _charsNonAsciiOnly, out fileCount);

        List<string> lines = BuildOutput(collector, _mode, _sortResult, _charsSortByCodePoint, _charsOnePerLine);

        bool existed = File.Exists(outPath);
        try
        {
            WriteOut(lines, outPath);
        }
        catch (Exception e)
        {
            return "写入失败：" + e.Message;
        }

        EditorPrefs.SetString(PrefKeyDir, _outputDir);
        EditorPrefs.SetString(PrefKeyFile, _fileName);

        StringBuilder sb = new StringBuilder();
        sb.Append("扫描文件数：").Append(fileCount).Append('\n');
        if (_mode == OutputMode.Chars)
            sb.Append("去重后字符数：").Append(collector.CharUnits.Count).Append('\n');
        else
            sb.Append("去重后中文文本条数：").Append(collector.Ordered.Count).Append('\n');
        sb.Append(existed ? "已覆盖已有文件：" : "已新建文件：").Append(outPath);
        Debug.Log("[中文文本导出] " + sb.ToString().Replace("\n", " | "));
        return sb.ToString();
    }

    // ============================================================
    // 输出路径
    // ============================================================

    /// <summary>取最终输出文件全路径；文件名缺扩展名时补 .txt。返回 null 表示路径不合法。</summary>
    string ResolveOutputPath()
    {
        if (string.IsNullOrEmpty(_outputDir)) return null;

        string name = (_fileName ?? "").Trim();
        if (name.Length == 0) name = DefaultFileName;
        if (!name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)) name += ".txt";

        try
        {
            return Path.GetFullPath(Path.Combine(_outputDir.Trim(), name));
        }
        catch
        {
            return null;
        }
    }

    static string CleanDir(string dir)
    {
        if (string.IsNullOrEmpty(dir)) return dir;
        return dir.Replace('\\', '/').TrimEnd('/');
    }

    void SavePrefs()
    {
        EditorPrefs.SetString(PrefKeyDir, _outputDir);
        EditorPrefs.SetString(PrefKeyFile, _fileName);
    }

    // ============================================================
    // 扫描
    // ============================================================

    static Collector Collect(bool scripts, bool editorScripts, bool prefabsScenes, bool assets,
        bool ignoreWhitespace, bool nonAsciiOnly, out int fileCount)
    {
        string root = Path.Combine(ProjectRoot, "Assets");
        HashSet<string> extSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (scripts) extSet.Add(".cs");
        if (prefabsScenes) { extSet.Add(".prefab"); extSet.Add(".unity"); extSet.Add(".scene"); }
        if (assets) extSet.Add(".asset");

        Collector collector = new Collector(ignoreWhitespace, nonAsciiOnly);
        fileCount = 0;

        string[] allFiles = new string[0];
        try { if (Directory.Exists(root)) allFiles = Directory.GetFiles(root, "*", SearchOption.AllDirectories); }
        catch { allFiles = new string[0]; }

        foreach (string file in allFiles)
        {
            string ext = Path.GetExtension(file);
            if (!extSet.Contains(ext)) continue;

            string norm = file.Replace('\\', '/');
            if (ext.Equals(".cs", StringComparison.OrdinalIgnoreCase) && !editorScripts && norm.Contains("/Editor/"))
                continue;

            string text;
            try { text = File.ReadAllText(file, Encoding.UTF8); }
            catch { continue; }

            fileCount++;
            if (ext.Equals(".cs", StringComparison.OrdinalIgnoreCase)) ScanCSharp(text, collector);
            else ScanYaml(text, collector);
        }

        return collector;
    }

    /// <summary>把收集结果转成待写入的行。</summary>
    static List<string> BuildOutput(Collector collector, OutputMode mode, bool sortText,
        bool sortChars, bool onePerLine)
    {
        if (mode == OutputMode.Text)
        {
            List<string> texts = new List<string>(collector.Ordered);
            if (sortText) texts.Sort(StringComparer.Ordinal);
            return texts;
        }

        List<string> units = new List<string>(collector.CharUnits);
        if (sortChars) units.Sort(StringComparer.Ordinal);

        List<string> lines = new List<string>();
        if (onePerLine)
        {
            lines.AddRange(units);
        }
        else
        {
            StringBuilder sb = new StringBuilder(units.Count);
            for (int i = 0; i < units.Count; i++) sb.Append(units[i]);
            lines.Add(sb.ToString());
        }
        return lines;
    }

    /// <summary>写入指定文件（存在则覆盖），必要时创建其所在目录。</summary>
    static string WriteOut(List<string> lines, string outPath)
    {
        string dir = Path.GetDirectoryName(outPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        File.WriteAllText(outPath, string.Join("\n", lines.ToArray()), new UTF8Encoding(false));
        return outPath;
    }

    // ============================================================
    // C# 字符串字面量扫描
    // ============================================================

    static void ScanCSharp(string code, Collector collector)
    {
        int n = code.Length;
        for (int i = 0; i < n; i++)
        {
            char c = code[i];

            // 行注释
            if (c == '/' && i + 1 < n && code[i + 1] == '/')
            {
                i += 2;
                while (i < n && code[i] != '\n') i++;
                continue;
            }
            // 块注释
            if (c == '/' && i + 1 < n && code[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < n && !(code[i] == '*' && code[i + 1] == '/')) i++;
                i++;
                continue;
            }
            // 字符字面量
            if (c == '\'')
            {
                i++;
                while (i < n && code[i] != '\'')
                {
                    if (code[i] == '\\') i++;
                    i++;
                }
                continue;
            }
            // 字符串（普通 / 逐字 / 插值）
            if (c == '"' || c == '@' || c == '$')
            {
                int j = i;
                bool verbatim = false;
                while (j < n && (code[j] == '@' || code[j] == '$'))
                {
                    if (code[j] == '@') verbatim = true;
                    j++;
                }
                if (j >= n || code[j] != '"') continue;

                StringBuilder raw = new StringBuilder();
                j++;
                while (j < n)
                {
                    char ch = code[j];
                    if (verbatim)
                    {
                        if (ch == '"')
                        {
                            if (j + 1 < n && code[j + 1] == '"') { raw.Append('"'); j += 2; continue; }
                            j++;
                            break;
                        }
                        raw.Append(ch); j++;
                    }
                    else
                    {
                        if (ch == '\\')
                        {
                            raw.Append(ch);
                            if (j + 1 < n) { raw.Append(code[j + 1]); j += 2; }
                            else j++;
                            continue;
                        }
                        if (ch == '"') { j++; break; }
                        raw.Append(ch); j++;
                    }
                }
                i = j - 1;
                collector.Add(verbatim ? raw.ToString() : Unescape(raw.ToString()));
            }
        }
    }

    // ============================================================
    // 预制体 / 场景 / 资源（YAML）文本字段扫描
    // ============================================================

    static void ScanYaml(string text, Collector collector)
    {
        int start = 0;
        while (start < text.Length)
        {
            int nl = text.IndexOf('\n', start);
            int end = nl < 0 ? text.Length : nl;
            string line = text.Substring(start, end - start);
            start = end + 1;

            string t = line.TrimStart();
            int keyLen;
            if (t.StartsWith("m_text:")) keyLen = "m_text:".Length;
            else if (t.StartsWith("m_Text:")) keyLen = "m_Text:".Length;
            else continue;

            string val = t.Substring(keyLen).Trim();
            if (val.Length == 0) continue;

            if (val.Length >= 2 && val[0] == '"' && val[val.Length - 1] == '"')
                val = Unescape(val.Substring(1, val.Length - 2));
            else if (val.Length >= 2 && val[0] == '\'' && val[val.Length - 1] == '\'')
                val = val.Substring(1, val.Length - 2).Replace("''", "'");
            // 其它情况为 YAML 普通标量（如数字），直接使用

            collector.Add(val);
        }
    }

    // ============================================================
    // 去重收集器
    // ============================================================

    /// <summary>收集去重后的文本与去重后的字符，可选忽略空白差异（保留空白更少的写法）。</summary>
    class Collector
    {
        /// <summary>去重后的文本，按首次出现顺序。</summary>
        public readonly List<string> Ordered = new List<string>();
        /// <summary>去重后的字符（代理对算一个单位），按首次出现顺序。</summary>
        public readonly List<string> CharUnits = new List<string>();

        readonly bool _ignoreWhitespace;
        readonly bool _nonAsciiOnly;
        readonly Dictionary<string, int> _index = new Dictionary<string, int>(StringComparer.Ordinal);
        readonly List<int> _spaceCounts = new List<int>();
        readonly HashSet<string> _charSeen = new HashSet<string>(StringComparer.Ordinal);

        public Collector(bool ignoreWhitespace, bool nonAsciiOnly)
        {
            _ignoreWhitespace = ignoreWhitespace;
            _nonAsciiOnly = nonAsciiOnly;
        }

        public void Add(string s)
        {
            if (string.IsNullOrEmpty(s)) return;

            s = s.Trim();
            if (!ContainsCjk(s)) return;

            string key = _ignoreWhitespace ? StripWhitespace(s) : s;
            if (key.Length == 0) return;

            int spaces = CountWhitespace(s);
            int idx;
            if (_index.TryGetValue(key, out idx))
            {
                // 已存在同内容条目：保留空白更少（更紧凑）的写法
                if (spaces < _spaceCounts[idx])
                {
                    Ordered[idx] = Normalize(s);
                    _spaceCounts[idx] = spaces;
                }
                return;
            }

            _index[key] = Ordered.Count;
            _spaceCounts.Add(spaces);
            Ordered.Add(Normalize(s));
            AddChars(s);
        }

        /// <summary>收集字符串中所有需要导出的字符（每字符只记一次）。</summary>
        void AddChars(string s)
        {
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                string unit;
                if (char.IsSurrogatePair(s, i))
                {
                    unit = s.Substring(i, 2);
                    i++;
                }
                else if (char.IsSurrogate(c))
                {
                    continue; // 落单的代理项
                }
                else
                {
                    if (!IsCharWanted(c)) continue;
                    unit = c.ToString();
                }

                if (_charSeen.Add(unit)) CharUnits.Add(unit);
            }
        }

        bool IsCharWanted(char c)
        {
            if (char.IsControl(c)) return false;
            if (_nonAsciiOnly) return c >= '\u0080';
            return c != ' ';
        }
    }

    // ============================================================
    // 工具
    // ============================================================

    /// <summary>统一换行，并把换行/制表符转义，保证每条只占一行。</summary>
    static string Normalize(string s)
    {
        s = s.Replace("\r\n", "\n").Replace('\r', '\n');
        s = s.Replace("\n", "\\n").Replace("\t", "\\t");
        return s;
    }

    /// <summary>去掉全部空白字符（含全角空格），用于忽略空白差异的去重。</summary>
    static string StripWhitespace(string s)
    {
        StringBuilder sb = new StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
            if (!char.IsWhiteSpace(s[i])) sb.Append(s[i]);
        return sb.ToString();
    }

    static int CountWhitespace(string s)
    {
        int n = 0;
        for (int i = 0; i < s.Length; i++)
            if (char.IsWhiteSpace(s[i])) n++;
        return n;
    }

    static bool ContainsCjk(string s)
    {
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c >= '\u4e00' && c <= '\u9fff') return true; // CJK 统一表意文字
            if (c >= '\u3400' && c <= '\u4dbf') return true; // 扩展 A
            if (c >= '\uf900' && c <= '\ufaff') return true; // 兼容表意文字
        }
        return false;
    }

    /// <summary>解码 C# / YAML 通用反斜杠转义。</summary>
    static string Unescape(string s)
    {
        if (s.IndexOf('\\') < 0) return s;
        StringBuilder sb = new StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c != '\\' || i + 1 >= s.Length) { sb.Append(c); continue; }
            char e = s[++i];
            switch (e)
            {
                case 'n': sb.Append('\n'); break;
                case 'r': sb.Append('\r'); break;
                case 't': sb.Append('\t'); break;
                case 'b': sb.Append('\b'); break;
                case 'f': sb.Append('\f'); break;
                case 'v': sb.Append('\v'); break;
                case 'a': sb.Append('\a'); break;
                case '0': sb.Append('\0'); break;
                case '\\': sb.Append('\\'); break;
                case '"': sb.Append('"'); break;
                case '\'': sb.Append('\''); break;
                case '/': sb.Append('/'); break;
                case 'u': AppendHex(sb, s, ref i, 4); break;
                case 'U': AppendHex(sb, s, ref i, 8); break;
                case 'x': AppendHex(sb, s, ref i, 0); break;
                default: sb.Append(e); break;
            }
        }
        return sb.ToString();
    }

    static void AppendHex(StringBuilder sb, string s, ref int i, int len)
    {
        if (len == 0) // \x：1~4 位十六进制
        {
            int j = i + 1, val = 0, cnt = 0;
            while (j < s.Length && cnt < 4 && IsHex(s[j])) { val = val * 16 + HexVal(s[j]); j++; cnt++; }
            if (cnt > 0) sb.Append((char)val);
            i = j - 1;
            return;
        }

        if (i + len >= s.Length) { sb.Append('?'); return; }
        int v = 0;
        for (int k = 1; k <= len; k++)
        {
            if (!IsHex(s[i + k])) { sb.Append('?'); return; }
            v = v * 16 + HexVal(s[i + k]);
        }
        if (len == 8)
        {
            if (v >= 0 && v <= 0x10FFFF) sb.Append(char.ConvertFromUtf32(v));
        }
        else sb.Append((char)v);
        i += len;
    }

    static bool IsHex(char c)
    {
        return (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
    }

    static int HexVal(char c)
    {
        if (c >= '0' && c <= '9') return c - '0';
        if (c >= 'a' && c <= 'f') return c - 'a' + 10;
        return c - 'A' + 10;
    }
}

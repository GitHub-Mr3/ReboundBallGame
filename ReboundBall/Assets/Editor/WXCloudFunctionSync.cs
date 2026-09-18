#if UNITY_EDITOR
using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 微信小游戏转换工具在导出时会先 DelectDir(导出目录/minigame) 再重新生成，
/// 因此放在 minigame 内的 cloudfunctions 目录与 project.config.json 里的
/// cloudfunctionRoot 配置每次导出都会丢失。
/// 本脚本负责导出后自动还原：把外层不会被清理的源码拷回项目，并重新注入配置项。
/// </summary>
[InitializeOnLoad]
public static class WXCloudFunctionSync
{
    /// <summary>微信转换面板里的「游戏导出目录」(DST)，按你的实际路径修改。</summary>
    private const string ExportRoot = @"E:\WechatGame";

    /// <summary>ExportRoot 下永久保存云函数源码的目录，不会被导出流程清理。</summary>
    private const string SrcFolder = "cloudfunctions-src";

    private const string MiniGameFolder = "minigame";
    private const string CloudFolder = "cloudfunctions";
    private const string CloudRootValue = "cloudfunctions/";

    private const string MenuPath = "微信小游戏/同步云开发配置";

    private static double _nextTick;
    private static DateTime _lastWriteTime;

    static WXCloudFunctionSync()
    {
        EditorApplication.update += AutoSyncTick;
    }

    /// <summary>导出后每秒检测一次 project.config.json 是否被重写，缺配置就自动补。</summary>
    private static void AutoSyncTick()
    {
        if (EditorApplication.timeSinceStartup < _nextTick) return;
        _nextTick = EditorApplication.timeSinceStartup + 1.0;

        var cfgPath = ConfigPath;
        if (!File.Exists(cfgPath)) return;

        var writeTime = File.GetLastWriteTimeUtc(cfgPath);
        if (writeTime == _lastWriteTime) return;
        _lastWriteTime = writeTime;

        if (!File.ReadAllText(cfgPath).Contains(CloudFolder)) Sync();
    }

    [MenuItem(MenuPath)]
    public static void Sync()
    {
        var cfgPath = ConfigPath;
        if (!File.Exists(cfgPath))
        {
            Debug.LogWarning($"[WXCloud] 未找到 {cfgPath}，请确认 ExportRoot 配置正确，或先在 Unity 完成一次导出。");
            return;
        }

        CopyCloudFunctions();
        InjectCloudRoot(cfgPath);

        Debug.Log($"[WXCloud] 云开发配置已还原：{TargetFolder} / {cfgPath}");
    }

    private static void CopyCloudFunctions()
    {
        var src = Path.Combine(ExportRoot, SrcFolder);
        if (!Directory.Exists(src))
        {
            Debug.LogWarning($"[WXCloud] 缺少云函数源码目录：{src}");
            return;
        }

        var dst = TargetFolder;
        Directory.CreateDirectory(dst);
        foreach (var file in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
        {
            var relative = file.Substring(src.Length + 1);
            var target = Path.Combine(dst, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.Copy(file, target, true);
        }
    }

    private static void InjectCloudRoot(string cfgPath)
    {
        var json = File.ReadAllText(cfgPath);
        var entry = $"\"cloudfunctionRoot\": \"{CloudRootValue}\"";

        if (json.Contains("cloudfunctionRoot"))
        {
            json = Regex.Replace(json, "\"cloudfunctionRoot\"\\s*:\\s*\"[^\"]*\"", entry);
        }
        else
        {
            json = Regex.Replace(json, "(\"appid\"\\s*:\\s*\"[^\"]*\",)", $"$1\n  {entry},");
        }

        File.WriteAllText(cfgPath, json);
    }

    private static string ConfigPath => Path.Combine(ExportRoot, MiniGameFolder, "project.config.json");
    private static string TargetFolder => Path.Combine(ExportRoot, MiniGameFolder, CloudFolder);
}
#endif

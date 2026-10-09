using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 音频管理器 - 纯代码单例，无需挂载到任何物体
/// 直接通过名称播放音频，首次访问时自动创建
/// </summary>
public class AudioMgr:SingletonBase<AudioMgr>
{
 

    private GameObject rootObject;
    private AudioSource bgmSource;
    private List<AudioSource> sfxSources = new List<AudioSource>();

    private Dictionary<string, AudioClip> bgmDict = new Dictionary<string, AudioClip>();
    private Dictionary<string, AudioClip> sfxDict = new Dictionary<string, AudioClip>();

    private float _bgmVolume = 0.7f;
    private float _sfxVolume = 1f;
    private int sfxIndex = 0;

    /// <summary>
    /// 音效源池大小（同时可叠加播放的音效数量）
    /// </summary>
    public int sfxPoolSize = 5;

    // ==================== 音量属性 ====================

    public float BGMVolume
    {
        get => _bgmVolume;
        set
        {
            _bgmVolume = Mathf.Clamp01(value);
            if (bgmSource != null) bgmSource.volume = _bgmVolume;
        }
    }

    public float SFXVolume
    {
        get => _sfxVolume;
        set
        {
            _sfxVolume = Mathf.Clamp01(value);
            foreach (var source in sfxSources)
                source.volume = _sfxVolume;
        }
    }

    // ==================== 构造 ====================

    private AudioMgr()
    {
        BGMVolume=  StorageMgr.GetFloat(GameData.AudioVolumData, 0.5f);
        InitRoot();
        InitAudioSources();
    }

    /// <summary>
    /// 创建根物体并保持跨场景
    /// </summary>
    private void InitRoot()
    {
        rootObject = new GameObject("[AudioManager]");
        Object.DontDestroyOnLoad(rootObject);
    }

    /// <summary>
    /// 初始化音频源
    /// </summary>
    private void InitAudioSources()
    {
        bgmSource = rootObject.AddComponent<AudioSource>();
        bgmSource.loop = true;
        bgmSource.playOnAwake = false;
        bgmSource.volume = _bgmVolume;

        for (int i = 0; i < sfxPoolSize; i++)
        {
            AudioSource source = rootObject.AddComponent<AudioSource>();
            source.loop = false;
            source.playOnAwake = false;
            source.volume = _sfxVolume;
            sfxSources.Add(source);
        }
    }

    // ==================== 注册音频 ====================

    /// <summary>
    /// 注册背景音乐
    /// </summary>
    public void RegisterBGM(string name, AudioClip clip)
    {
        if (string.IsNullOrEmpty(name) || clip == null)
        {
            Debug.LogWarning($"[AudioManager] 注册 BGM 失败: name={name}, clip={(clip == null ? "null" : "ok")}");
            return;
        }
        bgmDict[name] = clip;
    }

    /// <summary>
    /// 注册音效
    /// </summary>
    public void RegisterSFX(string name, AudioClip clip)
    {
        if (string.IsNullOrEmpty(name) || clip == null)
        {
            Debug.LogWarning($"[AudioManager] 注册 SFX 失败: name={name}, clip={(clip == null ? "null" : "ok")}");
            return;
        }
        sfxDict[name] = clip;
    }

    /// <summary>
    /// 批量注册（可用于初始化）
    /// </summary>
    public void RegisterBGM(Dictionary<string, AudioClip> dict)
    {
        foreach (var kv in dict) RegisterBGM(kv.Key, kv.Value);
    }

    public void RegisterSFX(Dictionary<string, AudioClip> dict)
    {
        foreach (var kv in dict) RegisterSFX(kv.Key, kv.Value);
    }

    /// <summary>
    /// 从 Resources 文件夹自动加载并注册
    /// 路径示例: Resources/Audio/BGM/main_menu.mp3 → "main_menu"
    /// </summary>
    public void RegisterBGMFromResources(string path)
    {
        AudioClip[] clips = Resources.LoadAll<AudioClip>(path);
        foreach (var clip in clips) RegisterBGM(clip.name, clip);
    }

    public void RegisterSFXFromResources(string path)
    {
        AudioClip[] clips = Resources.LoadAll<AudioClip>(path);
        foreach (var clip in clips) RegisterSFX(clip.name, clip);
    }

    // ==================== BGM 控制 ====================

    /// <summary>
    /// 播放背景音乐
    /// </summary>
    public void PlayBGM(string name, bool loop = true)
    {
        if (!bgmDict.TryGetValue(name, out AudioClip clip))
        {
            Debug.LogWarning($"[AudioManager] 未找到 BGM: {name}");
            return;
        }

        if (bgmSource.clip == clip && bgmSource.isPlaying) return;

        bgmSource.clip = clip;
        bgmSource.loop = loop;
        bgmSource.volume = _bgmVolume;
        bgmSource.Play();
    }

    public void StopBGM() => bgmSource.Stop();
    public void PauseBGM() => bgmSource.Pause();
    public void ResumeBGM() => bgmSource.UnPause();

    // ==================== 音效播放 ====================

    /// <summary>
    /// 播放音效
    /// </summary>
    public void PlaySFX(string name)
    {
        if (!sfxDict.TryGetValue(name, out AudioClip clip))
        {
            Debug.LogWarning($"[AudioManager] 未找到音效: {name}");
            return;
        }

        AudioSource source = sfxSources[sfxIndex];
        sfxIndex = (sfxIndex + 1) % sfxSources.Count;

        source.clip = clip;
        source.volume = _sfxVolume;
        source.Play();
    }

    /// <summary>
    /// 播放音效并指定音量系数（0~1）
    /// </summary>
    public void PlaySFX(string name, float volumeScale)
    {
        if (!sfxDict.TryGetValue(name, out AudioClip clip))
        {
            Debug.LogWarning($"[AudioManager] 未找到音效: {name}");
            return;
        }

        AudioSource source = sfxSources[sfxIndex];
        sfxIndex = (sfxIndex + 1) % sfxSources.Count;

        source.clip = clip;
        source.volume = _sfxVolume * Mathf.Clamp01(volumeScale);
        source.Play();
    }

    /// <summary>
    /// 在指定世界坐标播放 3D 音效
    /// </summary>
    public void PlaySFXAtPoint(string name, Vector3 position)
    {
        if (!sfxDict.TryGetValue(name, out AudioClip clip))
        {
            Debug.LogWarning($"[AudioManager] 未找到音效: {name}");
            return;
        }
        AudioSource.PlayClipAtPoint(clip, position, _sfxVolume);
    }

    // ==================== 全局控制 ====================

    /// <summary>
    /// 停止全部音频
    /// </summary>
    public void StopAll()
    {
        bgmSource.Stop();
        foreach (var source in sfxSources) source.Stop();
    }

    /// <summary>
    /// 全局静音
    /// </summary>
    public void MuteAll(bool mute)
    {
        AudioListener.volume = mute ? 0f : 1f;
    }
}
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioSourceMono : MonoBehaviour
{
    private AudioSource _audioSource;
    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        var defaultValue = StorageMgr.GetFloat(GameData.AudioVolumData, 0.5f);
        _audioSource.volume = defaultValue;
    }
}
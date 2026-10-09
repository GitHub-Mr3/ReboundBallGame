using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioSourceMono : MonoBehaviour
{
    [SerializeField] private AudioClip bgmClip;
    [SerializeField] private AudioClip dogClip;
    [SerializeField] private AudioClip obstacleClip;
    
    [SerializeField] private AudioClip failClip;
    [SerializeField] private AudioClip rewardClip;
    [SerializeField] private AudioClip uiclickClip;


    private void Awake()
    {
        AudioMgr.Instance.RegisterBGM(GameData.BGMusic, bgmClip);
        AudioMgr.Instance.RegisterSFX(GameData.DogSFX, dogClip);
        AudioMgr.Instance.RegisterSFX(GameData.ObstacleSFX, obstacleClip);
        
        AudioMgr.Instance.RegisterSFX(GameData.FailSFX, failClip);
        AudioMgr.Instance.RegisterSFX(GameData.RewardSFX, rewardClip);
        AudioMgr.Instance.RegisterSFX(GameData.UiClickSFX, uiclickClip);
        DontDestroyOnLoad(gameObject);
    }

 
}
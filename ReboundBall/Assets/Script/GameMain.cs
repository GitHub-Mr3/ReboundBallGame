using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameMain : MonoBehaviour
{
    private Button Btn_StarGame;

    private void Awake()
    {
        Btn_StarGame = transform.Find("Btn_StarGame").GetComponent<Button>();
    }

    // Start is called before the first frame update
    void Start()
    {
        Btn_StarGame.onClick.AddListener(OnClickStarGame);
    }

    void OnClickStarGame()
    {
        SceneManager.LoadSceneAsync("GameScence");
    }

    // Update is called once per frame
    void Update()
    {
    }
}
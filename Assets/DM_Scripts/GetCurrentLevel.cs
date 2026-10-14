using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public class GetCurrentLevel : MonoSingleton<GetCurrentLevel>
{

    public Text CurrentLevel;

    public void refreshText()
    {
        CurrentLevel.text = "Level" + " " + MonoSingleton<PlayerDataManager>.Instance.CurrentLevelNo;
    }

    private void Start()
    {
        CurrentLevel.text = "Level" +" " + MonoSingleton<PlayerDataManager>.Instance.CurrentLevelNo;

    }

    public void OnPressAllLevel()
    {
       // SoundSFX.Play(SFXIndex.ButtonClick);
        MonoSingleton<GameDataLoadManager>.Instance.MoveToLobbyScene();
    }

 
}

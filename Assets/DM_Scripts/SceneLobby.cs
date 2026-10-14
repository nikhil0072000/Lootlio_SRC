
/*
 * Created on 2023
 *
 * Copyright (c) 2023 dotmobstudio
 * Support : dotmobstudio@gmail.com
 */
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
//using UnityEngine.Purchasing;
using UnityEngine.UI;

public class SceneLobby : SceneClass
{
	

	public ScrollRect LevelBallScrollRect;

	public GameObject BaseEpisodeListItem;

	public GameObject ObjNoAdsButton;

	public RectTransform RTLevelBallScrollPanel;

	private List<GameObject> listObj = new List<GameObject>();

	private void SetLevelBall(GameObject obj, int level)
	{
		if (!(obj == null))
		{
			try
			{
				obj.name = level.ToString();
				obj.transform.Find("TextLevel").GetComponent<Text>().text = level.ToString();
				if (level < MonoSingleton<PlayerDataManager>.Instance.CurrentLevelNo)
				{
					obj.transform.Find("Current").gameObject.SetActive(value: false);
					obj.transform.Find("Disabled").gameObject.SetActive(value: false);
				}
				else if (level == MonoSingleton<PlayerDataManager>.Instance.CurrentLevelNo)
				{
					obj.transform.Find("Current").gameObject.SetActive(value: true);
					obj.transform.Find("Disabled").gameObject.SetActive(value: false);
				}
				else
				{
					obj.transform.Find("Current").gameObject.SetActive(value: false);
					obj.transform.Find("Disabled").gameObject.SetActive(value: true);
				}
			}
			catch (Exception)
			{
			}
		}
	}

	public override void Awake()
	{
		base.Awake();
		
		



	}

	private void PopupClosed()
	{
		//Resume game
	}


private void Start()
	{
		//base.Start();

		//Debug.Log("CURREN LEVEL " + MonoSingleton<PlayerDataManager>.Instance.CurrentLevelNo);

		//ObjNoAdsButton.SetActive(value: true);

		

		SetLevelBallScrollView(fullScreen: false);
	}



	public void OnClickWatchAds()
	{
		SoundSFX.Play(SFXIndex.ButtonClick);
		// Rewarded ads removed with the Gley MobileAds package.
		EasyUI.Toast.Toast.Show("Ads are currently unavailable.", 3f, EasyUI.Toast.ToastColor.Yellow);
	}


	private void CompleteMethod(bool completed)
	{
		if (completed == true)
		{
			//Debug.Log("Chay vao say");
			//MonoSingleton<PlayerDataManager>.Instance.IncreaseCoin(10);
			//base.OnEventClose();
			PopupRewardCoins popupRewardCoins = MonoSingleton<PopupManager>.Instance.Open(PopupType.PopupRewardCoins, enableBackCloseButton: true) as PopupRewardCoins;
		}
		else
		{

			Debug.Log("No Reward");
		}
	}


	

	public void OnPressCurentLevel()
    {
		SoundSFX.Play(SFXIndex.ButtonClick);
		MapData.main = new MapData(MonoSingleton<PlayerDataManager>.Instance.CurrentLevelNo);
		MonoSingleton<PlayerDataManager>.Instance.lastPlayedLevel = MonoSingleton<PlayerDataManager>.Instance.CurrentLevelNo;
		SoundSFX.Play(SFXIndex.ButtonClick);
		MonoSingleton<SceneControlManager>.Instance.LoadScene(SceneType.Game, SceneChangeEffect.Color);
		GameMain.CompleteGameStart();
	}

	private void SetLevelBallScrollView(bool fullScreen)
	{
		Vector2 offsetMin = RTLevelBallScrollPanel.offsetMin;
		if (fullScreen)
		{
			offsetMin.y = 0f;
		}
		else
		{
			offsetMin.y = 96f;
		}
		RTLevelBallScrollPanel.offsetMin = offsetMin;
	}

	private void OnEnable()
	{
		
	}

	private void LoadLevelBallList()
	{
		RemoveLevelBall();
		GameObject gameObject = BaseEpisodeListItem;
		MonoSingleton<GameDataLoadManager>.Instance.LobbyLoadedLevelBallCount = ServerDataTable.MAX_LEVEL;

		//DebugColor.LogJC("MAX LEVEL : " + ServerDataTable.MAX_LEVEL);
		for (int i = 0; i < ServerDataTable.MAX_LEVEL / 20; i++)
		{
			if (i > 0)
			{
				gameObject = UnityEngine.Object.Instantiate(BaseEpisodeListItem);
				gameObject.transform.SetParent(LevelBallScrollRect.content.transform, worldPositionStays: false);
			}
			listObj.Add(gameObject);
			gameObject.GetComponent<UIEpisodeItemList>().SetData(i + 1);
		}
	}

	public override IEnumerator OnSceneShow()
	{
		Application.targetFrameRate = GlobalSetting.FPS;
		MonoSingleton<UIManager>.Instance.SetCoinCurrencyMenuLayer(isPopupOverLayer: false);
		SoundManager.PlayConnection("Lobby");
		LoadLevelBallList();
		yield return null;
		listObj[(MonoSingleton<PlayerDataManager>.Instance.CurrentLevelNo - 1) / 20].GetComponent<UIEpisodeItemList>().OnPressButton(doNotTween: true);
		Canvas.ForceUpdateCanvases();
		LevelBallScrollRect.content.transform.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, (MonoSingleton<PlayerDataManager>.Instance.CurrentLevelNo - 1) / 20 * 142);
		
		MonoSingleton<PlayerDataManager>.Instance.LoadLastDailyBonusDate();

		//TODO: Open Spin when start
		//if (AppEventCommonParameters.IsDifferentDay(MonoSingleton<PlayerDataManager>.Instance.lastRecvDailyBonusDateTime))
		//{
		//	MonoSingleton<PopupManager>.Instance.Open(PopupType.PopupEventDailySpinReward);
		//}


    }

	

	public override void OnSceneHideStart()
	{
	}

	private void RemoveLevelBall()
	{
		if (listObj == null)
		{
			return;
		}
		for (int i = 0; i < listObj.Count; i++)
		{
			if ((bool)listObj[i])
			{
				UnityEngine.Object.Destroy(listObj[i]);
			}
		}
		listObj.Clear();
	}

	public override void OnSceneHideEnd()
	{
		MonoSingleton<UIManager>.Instance.HideCoinCurrentMenuLayer();
	}

	public void OnPressEpisodeListItem(GameObject obj)
	{
		int result = 0;
		int.TryParse(obj.name, out result);
		if (result <= 0)
		{
		}
	}



	public void OnPressNoAds()
	{
	}

	

	public void OnPressDailySpin()
	{
		MonoSingleton<PopupManager>.Instance.Open(PopupType.PopupEventDailySpinReward);
	}
}

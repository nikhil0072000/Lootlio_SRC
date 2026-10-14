/*
 * Created on 2023
 *
 * Copyright (c) 2023 dotmobstudio
 * Support : dotmobstudio@gmail.com
 *
 * NOTE: In-app purchases were removed with the Gley EasyIAP package.
 * This popup is kept so existing scene references survive; buying and
 * restoring show an "unavailable" message instead of calling a store.
 */
using DG.Tweening;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using EasyUI.Toast;

public class PopupShopCoin : Popup
{
    public static bool isNewUser;

    public Button WatchAds;

    public Text[] listPrice;

    public Button RestoreButton;

    public Image ImageBackBlocking;

    private void Awake()
    {
        // Store initialization removed with the Gley EasyIAP package.
        GetListPrice();
    }

    public override void Start()
    {
        base.Start();
        if ((bool)RestoreButton)
        {
            RestoreButton.gameObject.SetActive(false);
        }
    }

    private void GetListPrice()
    {
        if (listPrice == null)
        {
            return;
        }
        for (int i = 0; i < listPrice.Length; i++)
        {
            if ((bool)listPrice[i])
            {
                listPrice[i].text = string.Empty;
            }
        }
    }

    public void Restore()
    {
        Toast.Show("Purchases are currently unavailable.", 3f, ToastColor.Yellow);
    }

    public void MakeBuyProduct(int indexProduct)
    {
        Toast.Show("Purchases are currently unavailable.", 3f, ToastColor.Yellow);
    }

    public void OnClickWatchAds()
    {
        // Rewarded ads removed with the Gley MobileAds package.
        Toast.Show("Ads are currently unavailable.", 3f, ToastColor.Yellow);
    }

    private void CompleteMethod(bool completed)
    {
        if (completed == true)
        {
            base.OnEventClose();
            PopupRewardCoins popupRewardCoins = MonoSingleton<PopupManager>.Instance.Open(PopupType.PopupRewardCoins, enableBackCloseButton: true) as PopupRewardCoins;
        }
        else
        {
            Debug.Log("No Reward");
        }
    }

    public override void OnEventClose()
    {
        base.OnEventClose();
        UIManager.holdOnUpdateCoin = false;
        MonoSingleton<UIManager>.Instance.SetCoinCurrencyMenuLayer(isPopupOverLayer: false);
        if (MonoSingleton<PopupManager>.Instance.CurrentPopupType == PopupType.PopupInGameItemStore || (MonoSingleton<SceneControlManager>.Instance.CurrentSceneType == SceneType.Game && (MonoSingleton<PopupManager>.Instance.CurrentPopupType == PopupType.PopupGameOver || MonoSingleton<PopupManager>.Instance.CurrentPopupType == PopupType.PopupGameOverCollectInfo)))
        {
            MonoSingleton<UIManager>.Instance.SetCoinCurrencyMenuLayer(isPopupOverLayer: true);
        }
        else if (MonoSingleton<SceneControlManager>.Instance.CurrentSceneType == SceneType.Game)
        {
            MonoSingleton<UIManager>.Instance.HideCoinCurrentMenuLayer();
        }
        PeriodEventData.CheckAndOpenPopup();
    }

    private void OnDestroy()
    {
    }
}

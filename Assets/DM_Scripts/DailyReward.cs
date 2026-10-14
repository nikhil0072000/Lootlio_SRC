using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using EasyUI.Toast;

public class DailyReward : MonoBehaviour
{
  //  public int rewardValues = -1;
    //private int selectedRewardItemDataIndex = 3;

    private dailySpinItemData[] itemData = new dailySpinItemData[8]
    {
        new dailySpinItemData(ServerItemIndex.Coin, 10 , 1),
        new dailySpinItemData(ServerItemIndex.BoosterCandyPack, 2, 2),
        new dailySpinItemData(ServerItemIndex.Coin, 7, 120),
        new dailySpinItemData(ServerItemIndex.BoosterVBomb, 3, 30),
        new dailySpinItemData(ServerItemIndex.Coin, 4, 220),
        new dailySpinItemData(ServerItemIndex.BoosterHBomb, 1, 30),
        new dailySpinItemData(ServerItemIndex.Coin, 2, 517),
        new dailySpinItemData(ServerItemIndex.BoosterHammer, 1, 80)
    };

    // Start is called before the first frame update
    void Start()
    {
        // Daily-rewards calendar removed with the Gley package.
    }

    private void CalendarButtonClicked(int dayNumber, int rewardValue, Sprite rewardSprite)
    {

        switch (dayNumber)
        {
            case 1:
                RewardItemDaily(0, rewardValue);
              
                break;
            case 2:
                RewardItemDaily(0, rewardValue);
               
                break;
            case 3:
                RewardItemDaily(1, rewardValue);
               
                break;
            case 4:
                RewardItemDaily(3, rewardValue);
                break;
            case 5:
                RewardItemDaily(5, rewardValue);
                break;
            case 6:
                RewardItemDaily(7, rewardValue);
                break;
            default:
               // RewardItemDaily(7, rewardValue);
                break;
        }
    }


    private void RewardItemDaily(int selectedRewardItemDataIndex , int rewardValue )
    {

        itemData[selectedRewardItemDataIndex].rewardCount = rewardValue;

        MonoSingleton<PlayerDataManager>.Instance.RewardServerItem(itemData[selectedRewardItemDataIndex].rewardIndex, rewardValue, AppEventManager.ItemEarnedBy.Daily_Spin_Bonus, -1, holdOnUpdateCoin: true);
        PopupRewardItems popupRewardItems = MonoSingleton<PopupManager>.Instance.Open(PopupType.PopupRewardItems, enableBackCloseButton: true, null, null, null, holdEventOK: false, holdEventNegative: false, isReserve: false, enableOverlapPopup: true) as PopupRewardItems;
        if ((bool)popupRewardItems)
        {
            popupRewardItems.SetData(itemData[selectedRewardItemDataIndex].rewardIndex, itemData[selectedRewardItemDataIndex].rewardCount);
        }
    }

    public void ShowRewardPannel()
    {
        // Daily-rewards calendar removed with the Gley package.
        SoundSFX.Play(SFXIndex.SlidePopupShow);
    }
}

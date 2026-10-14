using UnityEngine;
using UnityEngine.UI;

public class UpdateCoin : MonoBehaviour
{
	private Text text;

	private void Start()
	{
		text = GetComponent<Text>();
	}

	private void LateUpdate()
	{
		if ((bool)text && !UIManager.holdOnUpdateCoin)
		{
			text.text = Utils.GetCurrencyNumberString(MonoSingleton<PlayerDataManager>.Instance.Coin);
		}
	}
}

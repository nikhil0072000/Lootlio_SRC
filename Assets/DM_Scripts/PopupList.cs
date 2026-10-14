using System.Collections.Generic;
using UnityEngine;

public class PopupList : MonoBehaviour
{
	public Dictionary<PopupType, GameObject> dicPopupList = new Dictionary<PopupType, GameObject>();

	public List<GameObject> listPopupObject = new List<GameObject>();

	public List<PopupType> listPopupType = new List<PopupType>();

	private void Awake()
	{
		for (int i = 0; i < listPopupType.Count; i++)
		{
			dicPopupList.Add(listPopupType[i], listPopupObject[i]);
		}
	}

	public GameObject GetPopup(PopupType type)
	{
		if (dicPopupList.ContainsKey(type))
		{
			return dicPopupList[type];
		}
		return null;
	}
}

// using UnityEngine;

// public class SupportNotchScreenAnchoredPosition : MonoBehaviour
// {
// 	public bool SupportOrientation;

// 	public float yPosition;

// 	private Vector2 orgAnchoredPosition;

// 	private bool isNotchScreen;

// 	private bool isPortraitView;

// 	private void Start()
// 	{
// 		orgAnchoredPosition = GetComponent<RectTransform>().anchoredPosition;
// 		if (UIManager.IsNotchScreen())
// 		{
// 			SetPosition();
// 			isNotchScreen = true;
// 			isPortraitView = true;
// 		}
// 	}

// 	private void SetPosition()
// 	{
// 		//Debug.Log("Set Notch Screen Anchored Position");
// 		Vector2 anchoredPosition = GetComponent<RectTransform>().anchoredPosition;
// 		anchoredPosition.y = yPosition;
// 		GetComponent<RectTransform>().anchoredPosition = anchoredPosition;
// 	}
// }

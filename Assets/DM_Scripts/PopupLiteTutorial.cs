
/*
 * Created on 2023
 *
 * Copyright (c) 2023 dotmobstudio
 * Support : dotmobstudio@gmail.com
 */
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PopupLiteTutorial : Popup
{
	private List<string> tutorialDesc = new List<string>();

	private void Awake()
	{
		tutorialDesc.Add("Make match 3 same Emojis!");
		tutorialDesc.Add("Match the Emojis within the Jail!");
		tutorialDesc.Add("Match the Emojis around the Blocks.");
		tutorialDesc.Add("Match the Emojis on the Tile.");
		tutorialDesc.Add("Match the Emojis on the Gold to spread it.");
		tutorialDesc.Add("Bring All Items down to the bottom of the board.");
		tutorialDesc.Add("Find the Golden behind the Tiles!");
		tutorialDesc.Add("Match the Emojis & Find the Golden!");



	}

	//private string[] tutorialDesc = new string[8]
	//{
	//	"Make match 3 same Emojis!",
	//	"Match the Emojis within the Jail!",
	//	"Match the Emojis around the Blocks.",
	//	"Match the Emojis on the Tile.",
	//	"Match the Emojis on the Gold to spread it.",
	//	"Bring All Items down to the bottom of the board.",
	//	"Find the Golden  behind the Tiles!",
	//	"Match the Emojis & Find the Golden!"
	//};

	private string[] tutorialTitle = new string[0];

	public GameObject[] ObjTutorailGroups;

	public Text TextTutorialTitle;

	public Text TextTutorialDesc;

	public void SetData(int tutorialIndex)
	{
		ObjTutorailGroups[tutorialIndex].SetActive(value: true);
		TextTutorialDesc.text = tutorialDesc[tutorialIndex];
	}

    
}

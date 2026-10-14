using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LangController : Popup
{

    [SerializeField] private Dropdown dropdownLang;
    // Start is called before the first frame update
    void Start()
    {
        dropdownLang.onValueChanged.AddListener(delegate
        {
            DropdownValueChanged(dropdownLang);
        });

        // Load saved value
        int savedValue = PlayerPrefs.GetInt("SelectedDropdownValue", 0); // default value is 0
        dropdownLang.value = savedValue;

    }

    void DropdownValueChanged(Dropdown change)
    {
        // Save selected value
        PlayerPrefs.SetInt("SelectedDropdownValue", change.value);
        PlayerPrefs.Save();

        // You can perform other actions based on the selected value if needed
        Debug.Log("Selected Dropdown Value: " + change.value);
    }

	void RefreshText()
    {
        GetCurrentLevel.Instance.refreshText();
    }

	public void GetDropDowValue()
	{
		//int indexDropDow = dropdownLang.value;

		Debug.Log("DROPDOW LANG :" + dropdownLang.value);

		// Localization removed with the Gley package: English only.
		RefreshText();
	}

}

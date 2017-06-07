//Author and copyright owner: Matrix Inception Inc.
//Date: 2016-10-31
//This script is attached to each key, and prompts an action when a key is selected.
//

using UnityEngine;
using System.Collections;

public class KeyboardGG : MonoBehaviour {

    public GameObject KeyboardOne;
    KeyboardMain keyboardMain;

    //inputString keys track of typed message
    string inputString; 
    string keyName;
    int keySoundIndex;
    AudioSource audioSource;
    

	// Use this for initialization
	void Start () {
        keyboardMain = KeyboardOne.GetComponent<KeyboardMain>();      
        audioSource = KeyboardOne.GetComponent<AudioSource>();
    }
	
	// Update is called once per frame
	void Update () {

    }

    void OnSelect()
    {
        keySoundIndex = (int)Mathf.Round(Random.Range(-0.49f, keyboardMain.keySounds.Length - 0.51f));
        audioSource.clip = keyboardMain.keySounds[keySoundIndex];
        audioSource.loop = false;
        audioSource.Play();

        keyName = gameObject.name;
        inputString = keyboardMain.InputDisplay.GetComponent<TextMesh>().text;

        switch (keyName)
        {
            case "keyBackspace":
                if (inputString.Length > 0) {
                    //check whether backspace should remove a line
                    if(inputString.Length>1 && inputString.Substring(inputString.Length-2,2)== System.Environment.NewLine)
                    {
                        keyboardMain.InputDisplay.transform.position += new Vector3(0, -0.07f, 0);
                        inputString = inputString.Substring(0, inputString.Length - 2);
                    }
                    else { 
                        inputString = inputString.Substring(0, inputString.Length - 1);
                    }
                }
                break;
            case "keyShift":
                keyboardMain.OnShift();
                break;
            case "keySpace":
                inputString += " ";
                break;
            case "keyReturn":
                inputString += System.Environment.NewLine;
                keyboardMain.InputDisplay.transform.position += new Vector3(0, 0.07f, 0);
                break;
            case "keyDone":
                keyboardMain.OnDone();
                break;
            default:
                if (keyboardMain.ShiftOn)
                {
                    inputString += keyName.Substring(4, 1);
                }
                else
                {
                    inputString += keyName.Substring(3, 1);
                }
                break;
        }

        keyboardMain.InputDisplay.GetComponent<TextMesh>().text = inputString;
        inputString = null;
    }


}

//Author and copyright owner: Matrix Inception Inc.
//Date: 2016-10-31
//This script controls higher level functions of the keyboard, namely Shift, Show / Hide, and Move / Pin.

using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Windows.Speech;

public class KeyboardMain : MonoBehaviour {

    public AnnotationHandling target;
    public GameObject InputDisplay;
    public bool ShiftOn;
    public bool IsDone;
    public GameObject keyboardUpper;
    public GameObject keyboardLower;
    public GameObject keyboardSet;
    public GameObject keyDone;
    public bool IsMoving;
    public AudioClip[] keySounds;
    public TextMesh instructions;
    public TextMesh inputText;
    public GameObject cursor;

    KeywordRecognizer keywordRecognizer = null;
    Dictionary<string, System.Action> keywords = new Dictionary<string, System.Action>();

    // Use this for initialization
    void Start () {
        keyboardUpper.SetActive(ShiftOn);
        keyboardLower.SetActive(!ShiftOn);
        keyboardSet.SetActive(!IsDone);

        keywords.Add("Move Keyboard", () =>
            {
                IsMoving = true;
            }
        );
        keywords.Add("Pin Keyboard", () =>
            {
                IsMoving = false;
            }
        );
        // Tell the KeywordRecognizer about our keywords.
        keywordRecognizer = new KeywordRecognizer(keywords.Keys.ToArray());

        // Register a callback for the KeywordRecognizer and start recognizing!
        keywordRecognizer.OnPhraseRecognized += KeywordRecognizer_OnPhraseRecognized;
        keywordRecognizer.Start();
        inputText.text = PlayerPrefs.GetString("server address");
        if (inputText.text == "")
        {
            inputText.text = "184.189.226.223";
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (IsMoving) {
            transform.position = Vector3.Lerp(transform.position, Camera.main.transform.position + Camera.main.transform.forward * 1.5f + transform.right * (-0.39f), 0.1f );
            transform.LookAt(Camera.main.transform.position+ Camera.main.transform.forward * 4+ transform.right * (-0.39f)); 
        }


    }




    public void OnShift()
    {
        ShiftOn = !ShiftOn;
        keyboardUpper.SetActive(ShiftOn);
        keyboardLower.SetActive(!ShiftOn);
    }

    //The green square is the "Done" key, and it's kept as a separate key from the rest of the keyboard. 
    //Once selected it shows or hides the keyboard. Additional scripts can be added here to submit the message.
    public void OnDone()
    {
        IsDone = !IsDone;
        keyboardSet.SetActive(!IsDone);
        if (IsDone) {
            keyDone.transform.position += keyDone.transform.right * (-(0.726f+0.06f)) + keyDone.transform.up * (0.245f+0.1f);
            string serveraddr = inputText.text;
            
            instructions.text = "Connecting...";

            PlayerPrefs.SetString("server address", serveraddr);
            PlayerPrefs.Save();
           
            target.ConnectToServer(serveraddr);
         
        }
        else
        {
            target.ResetEverything();
            instructions.text = "Enter Server Address:";
        }
    }

    public void Deactivate()
    {
        UnityThread.executeInUpdate(() => {
            cursor.SetActive(false);
            this.gameObject.SetActive(false);
        });
    }

    public void ResetKeyboard()
    {
        UnityThread.executeInUpdate(() => {
            cursor.SetActive(true);
            this.gameObject.SetActive(true);
            if (GetComponent<TextMesh>() == null)
            {
                Debug.Log("textmesh is null");
            }
            instructions.text = "Try Connecting Again:";
            if (keyDone == null)
            {
                Debug.Log("keydone is null");
            }

            IsDone = false;
            keyboardUpper.SetActive(ShiftOn);
            keyboardLower.SetActive(!ShiftOn);
            keyboardSet.SetActive(true);

            keyDone.SetActive(true);
            keyDone.transform.position += keyDone.transform.right * (0.726f + 0.06f) + keyDone.transform.up * (-(0.245f + 0.1f));
            
        });
    }

    private void KeywordRecognizer_OnPhraseRecognized(PhraseRecognizedEventArgs args)
    {
        System.Action keywordAction;
        if (keywords.TryGetValue(args.text, out keywordAction))
        {
            keywordAction.Invoke();
        }
    }
}

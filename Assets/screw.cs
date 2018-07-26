using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using KMHelper;

public class Screw : MonoBehaviour {

    public class ModSettingsJSON
    {
        public bool enableColorblindMode;
        public string note;
    }

    public KMAudio Audio;
    public KMBombModule Module;
    public KMBombInfo Info;
    public KMSelectable[] holes, btn;
    public KMModSettings modSettings;
    public GameObject cross_screw, screwdriver;
    public GameObject[] colorblindText;
    public MeshRenderer[] outlines;
    public TextMesh[] btnText;
    public TextMesh screenText;
    public Texture[] outlineTexture;

    private static int _moduleIdCounter = 1;
    private int _moduleId = 0;

    private int screwLoc = 1, screwAns, btnAns, stageCnt = 1;
    private readonly int[] outline_order = { 0, 0, 0, 0, 0, 0 }, button_order = { 0, 0, 0, 0 };
    private readonly string[] colorText = { "Blue", "Green", "Magenta", "Red", "White", "Yellow" };
    private readonly float[] holeXPos = { -0.06f, 0f, 0.06f, -0.06f, 0f, 0.06f }, holeZPos = { -0.02f, -0.02f, -0.02f, -0.06f, -0.06f, -0.06f };
    private bool _lightsOn = false, _isSolved = false, _screwInsert = true, _coroutineRunning = false, isColorBlind = false;

    void Start () {
        _moduleId = _moduleIdCounter++;
        Module.OnActivate += Init;
    }
	
    private void Awake()
    {
        for (int i = 0; i < 4; i++)
        {
            int j = i;
            btn[i].OnInteract += delegate ()
            {
                HandlePress(j);
                return false;
            };
        }
        for (int i = 0; i < 6; i++)
        {
            int j = i;
            holes[i].OnInteract += delegate ()
            {
                HandleScrew(j);
                return false;
            };
        }
    }

    void Init()
    {
        //check for color blind mode first!
        isColorBlind = ColorBlindCheck();

        //enable helper texts
        if (isColorBlind)
        {
            Debug.LogFormat("[Screw #{0}] Colorblind mode enabled, showing colors of screw holes in text.", _moduleId);

            for (int i = 0; i < 6; i++)
            {
                colorblindText[i].SetActive(true);
            }
        }

        //random outlines
        int[] select = { 0, 0, 0, 0, 0, 0 };
        int rand;
        for (int i = 0; i < 6; i++)
        {
            rand = -1;
            while (rand == -1)
            {
                rand = Random.Range(0, 6);
                if (select[rand] == 0)
                {
                    select[rand] = 1;
                    outline_order[i] = rand;

                    outlines[i].material.mainTexture = outlineTexture[rand];
                    if (isColorBlind) colorblindText[i].GetComponent<TextMesh>().text = colorText[rand][0].ToString();
                }
                else rand = -1;
            }
        }
        Debug.LogFormat("[Screw #{0}] Order of outlines: TOP {1} {2} {3} BOTTOM {4} {5} {6}", _moduleId
            , colorText[outline_order[0]], colorText[outline_order[1]], colorText[outline_order[2]]
            , colorText[outline_order[3]], colorText[outline_order[4]], colorText[outline_order[5]]);
        
        GenerateStage(1);
        _lightsOn = true;
    }

    void GenerateStage(int stage)
    {
        int[] select = { 0, 0, 0, 0 };
        int rand, pos;
        string[] locations = { "top-left", "top-middle", "top-right", "bottom-left", "bottom-middle", "bottom-right" };

        Debug.LogFormat("[Screw #{0}] Stage {1} of 3", _moduleId, stage);

        //random buttons
        for (int i = 0; i < 4; i++)
        {
            rand = -1;
            while (rand == -1)
            {
                rand = Random.Range(0, 4);
                if (select[rand] == 0)
                {
                    select[rand] = 1;
                    button_order[i] = rand;
                    btnText[i].text = "" + (char)(rand + 65);
                }
                else rand = -1;
            }
        }
        Debug.LogFormat("[Screw #{0}] Order of buttons: {1} {2} {3} {4}", _moduleId
            , btnText[0].text, btnText[1].text, btnText[2].text, btnText[3].text);
        
        //determine screw position
        if (stage == 1)
        {
            pos = Info.GetBatteryCount() + Info.GetOffIndicators().Count();
            Debug.LogFormat("[Screw #{0}] Screw position = Battery count + Unlit indicator count: {1}", _moduleId, pos);
        }
        else if (stage == 2)
        {
            pos = Info.GetSerialNumberNumbers().Last() + Info.GetOnIndicators().Count();
            Debug.LogFormat("[Screw #{0}] Screw position = Last digit of serial + Lit indicator count: {1}", _moduleId, pos);
        }
        else
        {
            pos = Info.GetPortCount() + Info.GetBatteryHolderCount();
            Debug.LogFormat("[Screw #{0}] Screw position = Amount of ports + Battery holder count: {1}", _moduleId, pos);
        }

        while (pos > 6)
        {
            pos -= 6;
            Debug.LogFormat("[Screw #{0}] Substract 6 to {1}.", _moduleId, pos);
        }
        if (pos == 0)
        {
            pos = 1;
            Debug.LogFormat("[Screw #{0}] Position is 0, set to 1.", _moduleId);
        }
        if (screwLoc == pos)
        {
            pos++;
            Debug.LogFormat("[Screw #{0}] Screw is already in this hole, set to {1}.", _moduleId, pos);
        }
        while(pos > 6)
        {
            pos -= 6;
            Debug.LogFormat("[Screw #{0}] Substract 6 to {1}.", _moduleId, pos);
        }
        if (pos == 0)
        {
            pos = 1;
            Debug.LogFormat("[Screw #{0}] Position is 0, set to 1.", _moduleId);
        }

        screwAns = pos;
        Debug.LogFormat("[Screw #{0}] Screw must be in {1} {2} hole (position {3})", _moduleId, locations[screwAns - 1], colorText[outline_order[screwAns - 1]].ToLower(), screwAns);

        FindBtn(pos);
        Debug.LogFormat("[Screw #{0}] Must push button {1} at position {2}", _moduleId, (char)(button_order[btnAns] + 65), btnAns + 1);

        screenText.text = stage.ToString();
    }

    void FindBtn(int pos)
    {
        int mPos = System.Array.IndexOf(outline_order, 2) + 1, yPos = System.Array.IndexOf(outline_order, 5) + 1;

        //for color R, Y, G
        if (outline_order[pos-1] % 2 == 1)
        {
            //top row
            if(pos < 4)
            {
                if (pos == Info.GetBatteryHolderCount())
                {
                    btnAns = 3;
                    Debug.LogFormat("[Screw #{0}] Position in the row = Battery holder: 4th position.", _moduleId);
                }
                else if (System.Array.IndexOf(button_order, 0) == 0 || System.Array.IndexOf(button_order, 0) == 2)
                {
                    btnAns = System.Array.IndexOf(button_order, 2);
                    Debug.LogFormat("[Screw #{0}] A in 1st/3rd: Press C.", _moduleId);
                }
                else if (Info.IsIndicatorPresent("CLR") || Info.IsIndicatorPresent("FRK") || Info.IsIndicatorPresent("TRN"))
                {
                    btnAns = 2;
                    Debug.LogFormat("[Screw #{0}] CLR, FRK, or TRN: 3rd position.", _moduleId);
                }
                else if (System.Array.IndexOf(outline_order, 0) < 3)
                {
                    btnAns = 0;
                    Debug.LogFormat("[Screw #{0}] Same row as blue: 1st position.", _moduleId);
                }
                else
                {
                    btnAns = System.Array.IndexOf(button_order, 1);
                    Debug.LogFormat("[Screw #{0}] Otherwise: Press B.", _moduleId);
                }
            }
            //bottom row
            else
            {
                if (pos - 3 == Info.GetPorts().Distinct().Count())
                {
                    btnAns = 1;
                    Debug.LogFormat("[Screw #{0}] Position in the row = Port types: 2nd position.", _moduleId);
                }
                else if (pos - 3 == Info.GetBatteryCount())
                {
                    btnAns = System.Array.IndexOf(button_order, 3);
                    Debug.LogFormat("[Screw #{0}] Position in the row = Battery count: Press D.", _moduleId);
                }
                else if (pos - 3 != System.Array.IndexOf(outline_order, 4) + 1)
                {
                    btnAns = System.Array.IndexOf(button_order, 0);
                    Debug.LogFormat("[Screw #{0}] Not opposite to white: Press A.", _moduleId);
                }
                else if ((pos == 4 && mPos == 5) || (pos == 5 && (mPos == 4 || mPos == 6)) || (pos == 6 && mPos == 5))
                {
                    btnAns = System.Array.IndexOf(button_order, 2);
                    Debug.LogFormat("[Screw #{0}] Adjacent to magenta: Press C.", _moduleId);
                }
                else
                {
                    btnAns = 0;
                    Debug.LogFormat("[Screw #{0}] Otherwise: 1st position.", _moduleId);
                }
            }
        }
        //for color B, M, W
        else
        {
            //top row
            if(pos < 4)
            {
                if (pos == Info.GetPorts().Distinct().Count())
                {
                    btnAns = System.Array.IndexOf(button_order, 3);
                    Debug.LogFormat("[Screw #{0}] Position in the row = Port types: Press D.", _moduleId);
                }
                else if (System.Array.IndexOf(button_order, 2) == 1 || System.Array.IndexOf(button_order, 2) == 3)
                {
                    btnAns = System.Array.IndexOf(button_order, 1);
                    Debug.LogFormat("[Screw #{0}] C in 2nd/4th: Press B.", _moduleId);
                }
                else if (Info.IsIndicatorPresent("CAR") || Info.IsIndicatorPresent("FRQ") || Info.IsIndicatorPresent("SND"))
                {
                    btnAns = 3;
                    Debug.LogFormat("[Screw #{0}] CAR, FRQ, or SND: 4th position.", _moduleId);
                }
                else if (System.Array.IndexOf(outline_order, 3) < 3)
                {
                    btnAns = 1;
                    Debug.LogFormat("[Screw #{0}] Same row as red: 2nd position.", _moduleId);
                }
                else
                {
                    btnAns = System.Array.IndexOf(button_order, 0);
                    Debug.LogFormat("[Screw #{0}] Otherwise: Press A.", _moduleId);
                }
            }
            //bottom row
            else
            {
                if (pos - 3 == Info.GetPortPlateCount())
                {
                    btnAns = 1;
                    Debug.LogFormat("[Screw #{0}] Position in the row = Port plate count: 2nd position.", _moduleId);
                }
                else if (pos - 3 == Info.GetIndicators().Count())
                {
                    btnAns = System.Array.IndexOf(button_order, 0);
                    Debug.LogFormat("[Screw #{0}] Position in the row = Indicator count: Press A.", _moduleId);
                }
                else if ((pos == 4 && yPos == 5) || (pos == 5 && (yPos == 4 || yPos == 6)) || (pos == 6 && yPos == 5))
                {
                    btnAns = System.Array.IndexOf(button_order, 2);
                    Debug.LogFormat("[Screw #{0}] Adjacent to yellow: Press C.", _moduleId);
                }
                else if (pos - 3 != System.Array.IndexOf(outline_order, 1) + 1)
                {
                    btnAns = System.Array.IndexOf(button_order, 3);
                    Debug.LogFormat("[Screw #{0}] Not opposite to green: Press D.", _moduleId);
                }
                else
                {
                    btnAns = 3;
                    Debug.LogFormat("[Screw #{0}] Otherwise: 4th position.", _moduleId);
                }
            }
        }
    }

    bool ColorBlindCheck()
    {
        try
        {
            ModSettingsJSON settings = JsonConvert.DeserializeObject<ModSettingsJSON>(modSettings.Settings);
            if (settings != null)
                return settings.enableColorblindMode;
            else
                return false;
        }
        catch (JsonReaderException e)
        {
            Debug.LogFormat("[Screw #{0}] JSON reading failed with error {1}, assuming colorblind mode is disabled.", _moduleId, e.Message);
            return false;
        }
    }

    void HandlePress(int n)
    {
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, btn[n].transform);
        btn[n].AddInteractionPunch();

        if (_screwInsert && !_coroutineRunning && _lightsOn && !_isSolved)
        {
            Debug.LogFormat("[Screw #{0}] Screw at position {1}, expected {2}.", _moduleId, screwLoc, screwAns);
            Debug.LogFormat("[Screw #{0}] Pushed button {1} at {2}, expected {3} at {4}.", _moduleId, (char)(button_order[n] + 65), n + 1, (char)(button_order[btnAns] + 65), btnAns + 1);

            if (screwLoc == screwAns && n == btnAns)
            {
                if(stageCnt == 3)
                {
                    Debug.LogFormat("[Screw #{0}] Stage 3 passed! Module passed!",_moduleId);
                    Module.HandlePass();
                    _isSolved = true;
                    screenText.text = string.Empty;
                }
                else
                {
                    Debug.LogFormat("[Screw #{0}] Stage {1} passed! Proceed to next stage.", _moduleId, stageCnt);
                    stageCnt++;
                    GenerateStage(stageCnt);
                }
            }
            else
            {
                Debug.LogFormat("[Screw #{0}] Answer incorrect! Strike!", _moduleId);
                Module.HandleStrike();
            }
        }
    }

    void HandleScrew(int n)
    {
        //unscrew
        if (_screwInsert == true && n + 1 == screwLoc && !_coroutineRunning && _lightsOn && !_isSolved)
        {
            Audio.PlaySoundAtTransform("screwdriver_sound", holes[n].transform);
            StartCoroutine("ScrewOut");
        }

        //screw
        if(_screwInsert == false && !_coroutineRunning && _lightsOn && !_isSolved)
        {
            screwLoc = n + 1;
            Audio.PlaySoundAtTransform("screwdriver_sound", holes[n].transform);
            StartCoroutine("ScrewIn");
            Debug.LogFormat("[Screw #{0}] Screw in to hole {1}", _moduleId, screwLoc);
        }
    }

    IEnumerator ScrewOut()
    {
        float smooth = 75f, time = 0.5f;
        float rotateDelta = 1f / (time * smooth), transformDelta = 0.04f / (time * smooth);
        float rotateCurrent = 0f, transformCurrent = -0.02f;

        _coroutineRunning = true;
        cross_screw.gameObject.transform.localPosition = new Vector3(holeXPos[screwLoc - 1], transformCurrent, holeZPos[screwLoc - 1]);
        //screwdriver.GetComponent<MeshRenderer>().enabled = true;

        for (int i = 0; i <= time * smooth; i++)
        {
            cross_screw.gameObject.transform.localPosition = new Vector3(holeXPos[screwLoc - 1], transformCurrent, holeZPos[screwLoc - 1]);
            cross_screw.gameObject.transform.Rotate(Vector3.up, -15);
            rotateCurrent += rotateDelta;
            transformCurrent += transformDelta;
            yield return new WaitForSeconds(time / smooth);
        }
        //screwdriver.GetComponent<MeshRenderer>().enabled = false;
        cross_screw.GetComponent<MeshRenderer>().enabled = false;
        _coroutineRunning = false;
        _screwInsert = false;
        yield return null;
    }

    IEnumerator ScrewIn()
    {
        float smooth = 75f, time = 0.5f;
        float rotateDelta = 1f / (time * smooth), transformDelta = 0.04f / (time * smooth);
        float rotateCurrent = 0f, transformCurrent = 0.02f;

        _coroutineRunning = true;
        cross_screw.gameObject.transform.localPosition = new Vector3(holeXPos[screwLoc - 1], transformCurrent, holeZPos[screwLoc - 1]);
        //screwdriver.GetComponent<MeshRenderer>().enabled = true;
        cross_screw.GetComponent<MeshRenderer>().enabled = true;

        for (int i = 0; i <= time * smooth; i++)
        {
            cross_screw.gameObject.transform.localPosition = new Vector3(holeXPos[screwLoc - 1], transformCurrent, holeZPos[screwLoc - 1]);
            cross_screw.gameObject.transform.Rotate(Vector3.up, 15);
            rotateCurrent += rotateDelta;
            transformCurrent -= transformDelta;
            yield return new WaitForSeconds(time / smooth);
        }
        //screwdriver.GetComponent<MeshRenderer>().enabled = false;
        _coroutineRunning = false;
        _screwInsert = true;
        yield return null;
    }

#pragma warning disable 414
    private readonly string TwitchHelpMessage = @"Unscrew with “!{0} unscrew”. Put the screw in the 3rd hole with “!{0} screw 3” or “!{0} screw tr”. Press a button with “!{0} press A” (label) or “!{0} press 1” (position).";
#pragma warning restore 414

    KMSelectable[] ProcessTwitchCommand(string command)
    {
        var select = new List<KMSelectable>();
        command = command.ToLowerInvariant().Trim();

        if(command.Equals("unscrew"))
        {
            return new[] { holes[screwLoc - 1] };
        }

        if(Regex.IsMatch(command, @"^screw [a-zA-Z1-6]+$"))
        {
            command = command.Substring(6).Trim();

            switch(command)
            {
                case "1": case "tl": case "lt": case "topleft": case "lefttop": select.Add(holes[0]); break;
                case "2": case "tm": case "mt": case "topmiddle": case "middletop": select.Add(holes[1]); break;
                case "3": case "tr": case "rt": case "topright": case "righttop": select.Add(holes[2]); break;
                case "4": case "bl": case "lb": case "buttomleft": case "leftbuttom": select.Add(holes[3]); break;
                case "5": case "bm": case "mb": case "bottommiddle": case "middlebottom": select.Add(holes[4]); break;
                case "6": case "br": case "rb": case "bottomright": case "rightbottom": select.Add(holes[5]); break;
                default: return null;
            }
            return select.ToArray();
        }

        if(Regex.IsMatch(command, @"^press [a-zA-Z1-4]+$"))
        {
            command = command.Substring(6).Trim();

            switch(command)
            {
                case "1": select.Add(btn[0]); break;
                case "2": select.Add(btn[1]); break;
                case "3": select.Add(btn[2]); break;
                case "4": select.Add(btn[3]); break;
                case "a": select.Add(btn[System.Array.IndexOf(button_order, 0)]); break;
                case "b": select.Add(btn[System.Array.IndexOf(button_order, 1)]); break;
                case "c": select.Add(btn[System.Array.IndexOf(button_order, 2)]); break;
                case "d": select.Add(btn[System.Array.IndexOf(button_order, 3)]); break;
                default: return null;
            }
            return select.ToArray();
        }
        return null;
    }
}

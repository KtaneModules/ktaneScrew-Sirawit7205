using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using KMHelper;

public class screw : MonoBehaviour {

    private static int _moduleIdCounter = 1;
    private int _moduleId = 0;

    public KMAudio Audio;
    public KMBombModule Module;
    public KMBombInfo Info;
    public KMSelectable[] holes, btn;
    public GameObject cross_screw, screwdriver;
    public MeshRenderer[] outlines;
    public TextMesh[] btnText;
    public Texture[] outlineTexture;

    private int screwLoc = 1, screwAns, btnAns, stageCnt = 1;
    private int[] outline_order = { 0, 0, 0, 0, 0, 0 }, button_order = { 0, 0, 0, 0 };
    private string[] color_text = { "Blue", "Green", "Magenta", "Red", "White", "Yellow" };
    private float[] holeXPos = { -0.06f, 0f, 0.06f, -0.06f, 0f, 0.06f }, holeZPos = { -0.02f, -0.02f, -0.02f, -0.06f, -0.06f, -0.06f };
    private bool _lightsOn = false, _isSolved = false, _screwInsert = true, _coroutineRunning = false;

    // Use this for initialization
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
                handlePress(j);
                return false;
            };
        }
        for (int i = 0; i < 6; i++)
        {
            int j = i;
            holes[i].OnInteract += delegate ()
            {
                handleScrew(j);
                return false;
            };
        }
    }

    void Init()
    {
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
                }
                else rand = -1;
            }
        }
        Debug.LogFormat("[Screw #{0}] Order of outlines: TOP {1} {2} {3} BOTTOM {4} {5} {6}", _moduleId
            , color_text[outline_order[0]], color_text[outline_order[1]], color_text[outline_order[2]]
            , color_text[outline_order[3]], color_text[outline_order[4]], color_text[outline_order[5]]);

        generateStage(1);
        _lightsOn = true;
    }

    void generateStage(int stage)
    {
        int[] select = { 0, 0, 0, 0 };
        int rand, pos;

        Debug.LogFormat("[Screw #{0}] Stage {1} of 5", _moduleId, stage);

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
            pos = Info.GetBatteryCount();
        else if (stage == 2)
            pos = Info.GetSerialNumberNumbers().Last();
        else if (stage == 3)
            pos = Info.GetPortCount();
        else if (stage == 4)
            pos = Info.GetOnIndicators().Count();
        else
            pos = Info.GetOffIndicators().Count();

        if (pos > 6)
            pos %= 6;
        if (pos == 0)
            pos = 1;
        if (screwLoc == pos)
            pos++;
        if (pos > 6)
            pos %= 6;
        if (pos == 0)
            pos = 1;

        screwAns = pos;
        Debug.LogFormat("[Screw #{0}] Screw must be at position {1}.", _moduleId, screwAns);

        findBtn(pos);
        Debug.LogFormat("[Screw #{0}] Must push button {1} at position {2}", _moduleId, (char)(button_order[btnAns] + 65), btnAns + 1);
    }

    void findBtn(int pos)
    {
        int mPos = System.Array.IndexOf(outline_order, 2) + 1, yPos = System.Array.IndexOf(outline_order, 5) + 1;

        //for color R, Y, G
        if (outline_order[pos-1] % 2 == 1)
        {
            //top row
            if(pos < 4)
            {
                if (pos == Info.GetBatteryHolderCount())
                    btnAns = 3;
                else if (System.Array.IndexOf(button_order, 0) == 0 || System.Array.IndexOf(button_order, 0) == 2)
                    btnAns = System.Array.IndexOf(button_order, 2);
                else if (Info.IsIndicatorPresent("CLR") || Info.IsIndicatorPresent("FRK") || Info.IsIndicatorPresent("TRN"))
                    btnAns = 2;
                else if (System.Array.IndexOf(outline_order, 0) < 3)
                    btnAns = 0;
                else
                    btnAns = System.Array.IndexOf(button_order, 1);
            }
            //bottom row
            else
            {
                if (pos - 3 == Info.GetPorts().Distinct().Count())
                    btnAns = 1;
                else if (pos - 3 == Info.GetBatteryCount())
                    btnAns = System.Array.IndexOf(button_order, 3);
                else if (pos - 3 != System.Array.IndexOf(outline_order, 4) + 1)
                    btnAns = System.Array.IndexOf(button_order, 0);
                else if ((pos == 4 && mPos == 5) || (pos == 5 && (mPos == 4 || mPos == 6)) || (pos == 6 && mPos == 4))
                    btnAns = System.Array.IndexOf(button_order, 2);
                else
                    btnAns = 0;
            }
        }
        //for color B, M, W
        else
        {
            //top row
            if(pos < 4)
            {
                if (pos == Info.GetPorts().Distinct().Count())
                    btnAns = System.Array.IndexOf(button_order, 3);
                else if (System.Array.IndexOf(button_order, 2) == 1 || System.Array.IndexOf(button_order, 2) == 3)
                    btnAns = System.Array.IndexOf(button_order, 1);
                else if (Info.IsIndicatorPresent("CAR") || Info.IsIndicatorPresent("FRQ") || Info.IsIndicatorPresent("SND"))
                    btnAns = 3;
                else if (System.Array.IndexOf(outline_order, 3) < 3)
                    btnAns = 1;
                else
                    btnAns = System.Array.IndexOf(button_order, 0);
            }
            //bottom row
            else
            {
                if (pos - 3 == Info.GetPortPlateCount())
                    btnAns = 1;
                else if (pos - 3 == Info.GetIndicators().Count())
                    btnAns = System.Array.IndexOf(button_order, 0);
                else if ((pos == 4 && yPos == 5) || (pos == 5 && (yPos == 4 || yPos == 6)) || (pos == 6 && yPos == 4))
                    btnAns = System.Array.IndexOf(button_order, 2);
                else if (pos - 3 != System.Array.IndexOf(outline_order, 1) + 1)
                    btnAns = System.Array.IndexOf(button_order, 3);
                else
                    btnAns = 3;
            }
        }
    }

    void handlePress(int n)
    {
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, btn[n].transform);
        btn[n].AddInteractionPunch();

        if (_screwInsert && !_coroutineRunning && _lightsOn && !_isSolved)
        {
            Debug.LogFormat("[Screw #{0}] Screw at position {1}, expected {2}.", _moduleId, screwLoc, screwAns);
            Debug.LogFormat("[Screw #{1}] Pushed button {1} at {2}, expected {3} at {4}.", _moduleId, (char)(button_order[n] + 65), n, (char)(button_order[btnAns] + 65), btnAns);

            if (screwLoc == screwAns && n == btnAns)
            {
                if(stageCnt == 5)
                {
                    Debug.LogFormat("[Screw #{0}] Stage 5 passed! Module passed!",_moduleId);
                    Module.HandlePass();
                    _isSolved = true;
                }
                else
                {
                    Debug.LogFormat("[Screw #{0}] Stage {1} passed! Proceed to next stage.", _moduleId, stageCnt);
                    stageCnt++;
                    generateStage(stageCnt);
                }
            }
            else
            {
                Debug.LogFormat("[Screw #{0}] Answer incorrect! Strike!", _moduleId);
                Module.HandleStrike();
            }
        }
    }

    void handleScrew(int n)
    {
        //unscrew
        if (_screwInsert == true && n + 1 == screwLoc && !_coroutineRunning && _lightsOn && !_isSolved)
        {
            Audio.PlaySoundAtTransform("screwdriver_sound", holes[n].transform);
            StartCoroutine("screwOut");
            Debug.LogFormat("[Screw #{0}] Unscrew from hole {1}", _moduleId, screwLoc);
        }

        //screw
        if(_screwInsert == false && !_coroutineRunning && _lightsOn && !_isSolved)
        {
            screwLoc = n + 1;
            Audio.PlaySoundAtTransform("screwdriver_sound", holes[n].transform);
            StartCoroutine("screwIn");
            Debug.LogFormat("[Screw #{0}] Screw in to hole {1}", _moduleId, screwLoc);
        }
    }

    IEnumerator screwOut()
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

    IEnumerator screwIn()
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

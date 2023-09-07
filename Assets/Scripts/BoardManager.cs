using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Gpt4All;
using Gpt4All.Samples;

public class BoardManager : MonoBehaviour
{
    public static BoardManager Instance { get; set; }
    private bool[,] allowedMoves { get; set; }

    private const float TILE_SIZE = 1.0f;
    private const float TILE_OFFSET = 0.5f;

    private int selectionX = -1;
    private int selectionY = -1;

    public List<GameObject> chessmanPrefabs;
    private List<GameObject> activeChessman;

    private Quaternion whiteOrientation = Quaternion.Euler(0, 270, 0);
    private Quaternion blackOrientation = Quaternion.Euler(0, 90, 0);

    public Chessman[,] Chessmans { get; set; }
    private Chessman selectedChessman;

    public bool isWhiteTurn = true;

    private Material previousMat;
    public Material selectedMat;

    public int[] EnPassantMove { set; get; }

    public LlmManager manager;
    public ChatSample chatHolder;

    bool printToGameLog = false;

    private string _previousText;

    private void Awake()
    {
        manager.OnResponseUpdated += OnResponseHandler;
    }

    private void OnResponseHandler(string response)
    {

        if (printToGameLog)
        {
            //The story provided by the LLM is sent to the BattleLog to be shown to the player
            chatHolder.UpdateGameLog(response, _previousText);
        }
    }

    // Use this for initialization
    void Start()
    {
        Instance = this;
        SpawnAllChessmans();
        EnPassantMove = new int[2] { -1, -1 };
    }

    // Update is called once per frame
    void Update()
    {
        UpdateSelection();

        if (Input.GetMouseButtonDown(0))
        {
            if (selectionX >= 0 && selectionY >= 0)
            {
                if (selectedChessman == null)
                {
                    // Select the chessman
                    SelectChessman(selectionX, selectionY);
                }
                else
                {
                    // Move the chessman
                    MoveChessman(selectionX, selectionY);
                }
            }
        }

        if (Input.GetKey("escape"))
            Application.Quit();
    }


    private void SelectChessman(int x, int y)
    {
        if (Chessmans[x, y] == null) return;

        if (Chessmans[x, y].isWhite != isWhiteTurn) return;

        bool hasAtLeastOneMove = false;

        allowedMoves = Chessmans[x, y].PossibleMoves();
        for (int i = 0; i < 8; i++)
        {
            for (int j = 0; j < 8; j++)
            {
                if (allowedMoves[i, j])
                {
                    hasAtLeastOneMove = true;
                    i = 8;
                    break;
                }
            }
        }

        if (!hasAtLeastOneMove)
            return;

        selectedChessman = Chessmans[x, y];
        previousMat = selectedChessman.GetComponent<MeshRenderer>().material;
        selectedMat.mainTexture = previousMat.mainTexture;
        selectedChessman.GetComponent<MeshRenderer>().material = selectedMat;

        BoardHighlights.Instance.HighLightAllowedMoves(allowedMoves);
    }

    //Old CalculateVictoryFunction
    private bool calculateVictory()
    {
        var rng = new System.Random();

        var outcome = rng.Next(0, 100) < 50;
        Debug.Log("calculateVictory() = "+ outcome.ToString());
        return outcome;
    }

    //Alternative CalculateVictory Asynchronous Function
    private async Task<bool> calculateVictoryAsync(string AttackerName,string DefenderName, string AttackerModifier, string DefenderModifier)
    {
        string fightPrompt = "### Instruction: The " + AttackerName + " attacks the" + DefenderName + ". Taking into account the context of the fight, determine who the winner is. If " + AttackerName + " wins say " + AttackerName + ". If " + DefenderName + " wins say " + DefenderName + ". Do not say anything else.";
        Debug.Log(fightPrompt);
        printToGameLog = false;

        _previousText = chatHolder.output.text;
        chatHolder.UpdateGameLog("\n ... \n", _previousText);

        //The player is notified of the fight before the LLM is queried
        string battleDescription = "The "+ AttackerName + " has launched an attack on the "+ DefenderName+". Please stand by as the battle takes place!";
        chatHolder.SetShortDescription(battleDescription);

        //The LLM is queried with the instruction to determine the outcome of the fight
        string outcomeString = await manager.Prompt(fightPrompt) ;

        //A prompt is prepared for the LLM to generate a description of the fight, taking into account any modifiers for each character
        string OutcomeDescriptionPrompt = "### Instruction: Write a story in two sentences with the following plot: "+ AttackerModifier + DefenderModifier+ "The " + AttackerName + " launched an attack on the " + DefenderName;
        bool outcome = true;


        Debug.Log("Outcome String: " + outcomeString);

        if (outcomeString.StartsWith(DefenderName))
        {
            OutcomeDescriptionPrompt += " but the " + DefenderName + " won the fight and killed the "+ AttackerName+".";
            outcome = false;
        }
        else
        {
            OutcomeDescriptionPrompt += " and the " + AttackerName + " won the fight and killed the " + DefenderName+".";
            outcome = true;
        }

        Debug.Log(OutcomeDescriptionPrompt);

        _previousText = chatHolder.output.text;
        printToGameLog = true;

        //The LLM is prompted with the full description of the fight, and asked to retell the story
        string outcomeDescriptionString = await manager.Prompt(OutcomeDescriptionPrompt);
        Debug.Log(outcomeDescriptionString);

        //The battle is over and the LLM is finished inferencing
        battleDescription = "The war continues...";
        chatHolder.SetShortDescription(battleDescription);

        return outcome;
    }

    private async void MoveChessman(int x, int y)
    {
        if (allowedMoves[x, y])
        {
            Chessman c = Chessmans[x, y];
            var victory = true;

            if (c != null && c.isWhite != isWhiteTurn)
            {
                // Capture a piece

                if (c.GetType() == typeof(King))
                {
                    // End the game
                    EndGame();
                    return;
                }

                string AttackerName = selectedChessman.GetComponent<Chessman>().characterName;
                string DefenderName = c.GetComponent<Chessman>().characterName;

                string AttackerModifier = selectedChessman.GetComponent<Chessman>().characterModifier;
                string DefenderModifer = c.GetComponent<Chessman>().characterModifier;

                victory = await calculateVictoryAsync(AttackerName, DefenderName, AttackerModifier, DefenderModifer);
                if (victory)
                {
                    foreach(GameObject activePiece in activeChessman)
                    {
                        //Each active character checks if the losing character was a friend
                        activePiece.GetComponent<Chessman>().ApplyDeathReaction(c.characterID, c.characterName);
                    }
                    activeChessman.Remove(c.gameObject);
                    Destroy(c.gameObject);
                }
                else
                {
                    foreach (GameObject activePiece in activeChessman)
                    {
                        //Each active character checks if the losing character was a friend
                        activePiece.GetComponent<Chessman>().ApplyDeathReaction(selectedChessman.characterID, selectedChessman.characterName);
                    }
                    activeChessman.Remove(selectedChessman.gameObject);
                    Destroy(selectedChessman.gameObject);
                }
            }
            if (x == EnPassantMove[0] && y == EnPassantMove[1])
            {
                if (isWhiteTurn)
                    c = Chessmans[x, y - 1];
                else
                    c = Chessmans[x, y + 1];

                activeChessman.Remove(c.gameObject);
                Destroy(c.gameObject);
            }
            EnPassantMove[0] = -1;
            EnPassantMove[1] = -1;
            if (selectedChessman.GetType() == typeof(Pawn))
            {
                if (y == 7) // White Promotion
                {
                    activeChessman.Remove(selectedChessman.gameObject);
                    Destroy(selectedChessman.gameObject);
                    SpawnChessman(1, x, y, true, "Queen", selectedChessman.characterID);
                    selectedChessman = Chessmans[x, y];
                }
                else if (y == 0) // Black Promotion
                {
                    activeChessman.Remove(selectedChessman.gameObject);
                    Destroy(selectedChessman.gameObject);
                    SpawnChessman(7, x, y, false, "Queen", selectedChessman.characterID);
                    selectedChessman = Chessmans[x, y];
                }
                EnPassantMove[0] = x;
                if (selectedChessman.CurrentY == 1 && y == 3)
                    EnPassantMove[1] = y - 1;
                else if (selectedChessman.CurrentY == 6 && y == 4)
                    EnPassantMove[1] = y + 1;
            }

            if (victory)
            {
                Chessmans[selectedChessman.CurrentX, selectedChessman.CurrentY] = null;
                selectedChessman.transform.position = GetTileCenter(x, y);
                selectedChessman.SetPosition(x, y);
                Chessmans[x, y] = selectedChessman;
            }
            isWhiteTurn = !isWhiteTurn;
        }

        selectedChessman.GetComponent<MeshRenderer>().material = previousMat;

        BoardHighlights.Instance.HideHighlights();
        selectedChessman = null;
    }

    private void UpdateSelection()
    {
        if (!Camera.main) return;

        RaycastHit hit;
        if (Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out hit, 50.0f, LayerMask.GetMask("ChessPlane")))
        {
            selectionX = (int)hit.point.x;
            selectionY = (int)hit.point.z;
        }
        else
        {
            selectionX = -1;
            selectionY = -1;
        }
    }

    private void SpawnChessman(int index, int x, int y, bool isWhite,string charName,int charID)
    {
        Vector3 position = GetTileCenter(x, y);
        GameObject go;

        if (isWhite)
        {
            go = Instantiate(chessmanPrefabs[index], position, whiteOrientation) as GameObject;
        }
        else
        {
            go = Instantiate(chessmanPrefabs[index], position, blackOrientation) as GameObject;
        }

        go.transform.SetParent(transform);
        Chessmans[x, y] = go.GetComponent<Chessman>();
        Chessmans[x, y].SetPosition(x, y);
        //Each character has a name and a unique ID.
        Chessmans[x, y].characterName = charName;
        Chessmans[x, y].characterID = charID;
        activeChessman.Add(go);
    }

    private Vector3 GetTileCenter(int x, int y)
    {
        Vector3 origin = Vector3.zero;
        origin.x += (TILE_SIZE * x) + TILE_OFFSET;
        origin.z += (TILE_SIZE * y) + TILE_OFFSET;

        return origin;
    }

    private void SpawnAllChessmans()
    {
        activeChessman = new List<GameObject>();
        Chessmans = new Chessman[8, 8];

        /////// White ///////

        // King
        SpawnChessman(0, 3, 0, true, "King of England",0);

        // Queen
        SpawnChessman(1, 4, 0, true, "Queen of England",1);

        // Rooks
        SpawnChessman(2, 0, 0, true, "Archer of the English Army",2);
        SpawnChessman(2, 7, 0, true, "Archer of the English Army", 3);

        // Bishops
        SpawnChessman(3, 2, 0, true, "English Bishop", 4);
        SpawnChessman(3, 5, 0, true, "English Bishop", 5);

        // Knights
        SpawnChessman(4, 1, 0, true, "Knight of England",6);
        SpawnChessman(4, 6, 0, true, "Knight of England", 7);

        // Pawns
        for (int i = 0; i < 8; i++)
        {
            SpawnChessman(5, i, 1, true, "English foot soldier",8+i);
        }


        /////// Black ///////

        // King
        SpawnChessman(6, 4, 7, false, "King of France",16);

        // Queen
        SpawnChessman(7, 3, 7, false, "Queen of France", 17);

        // Rooks
        SpawnChessman(8, 0, 7, false, "Archer of the French Army", 18);
        SpawnChessman(8, 7, 7, false, "Archer of the French Army", 19);

        // Bishops
        SpawnChessman(9, 2, 7, false, "French Bishop", 20);
        SpawnChessman(9, 5, 7, false, "French Bishop", 21);

        // Knights
        SpawnChessman(10, 1, 7, false, "French Chevalier", 22);
        SpawnChessman(10, 6, 7, false, "French Chevalier", 23);

        // Pawns
        for (int i = 0; i < 8; i++)
        {
            SpawnChessman(11, i, 6, false, "French foot soldier", 24+i);
        }
    }

    private void EndGame()
    {
        if (isWhiteTurn)
            Debug.Log("White wins");
        else
            Debug.Log("Black wins");

        foreach (GameObject go in activeChessman)
        {
            Destroy(go);
        }

        isWhiteTurn = true;
        BoardHighlights.Instance.HideHighlights();
        SpawnAllChessmans();
    }
}



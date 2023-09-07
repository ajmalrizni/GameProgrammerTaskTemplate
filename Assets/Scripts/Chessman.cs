using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class Chessman : MonoBehaviour
{

    public int CurrentX { set; get; }
    public int CurrentY { set; get; }

    public bool isWhite;

    public string characterName = "Pawn";

    public int characterID;

    public List<int> friendList;

    public string characterModifier = "";

    private void Start()
    {
        int IDOffset = 0;
        if (!isWhite)
        {
            IDOffset = 16;
        }

        var rng = new System.Random();
        for (int i = 0; i < 16; i++)
        {
            //There is a 70% chance each character is friends with another character in the same army.
            var friendCheck = rng.Next(0, 100) < 70;
            if (friendCheck)
            {
                friendList.Add(IDOffset+i);
            }
        }
    }

    public void SetPosition(int x, int y)
    {
        CurrentX = x;
        CurrentY = y;
    }

    public virtual bool[,] PossibleMoves()
    {
        return new bool[8, 8];
    }

    public void ApplyDeathReaction(int deathID,string deathName)
    {
        foreach(int friend in friendList)
        {
            if (deathID == friend)
            {
                //If a character's friend has died, their modifier is updated to take this into account.
                characterModifier = "The "+ characterName+ " was enraged by the death of their friend, the " + deathName+". ";
            }
        }
    }

    public bool Move(int x, int y, ref bool[,] r)
    {
        if (x >= 0 && x < 8 && y >= 0 && y < 8)
        {
            Chessman c = BoardManager.Instance.Chessmans[x, y];
            if (c == null)
                r[x, y] = true;
            else
            {
                if (isWhite != c.isWhite)
                    r[x, y] = true;
                return true;
            }
        }
        return false;
    }
}

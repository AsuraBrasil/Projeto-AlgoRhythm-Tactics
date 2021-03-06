﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ObjectType = Tile.ObjectType;

public class Unit : MonoBehaviour {

    //Reference
    [HideInInspector]
    public static LevelManager lm;

    public enum ActionType
    {
        Ready,                                                              //Ready to take an aciton, when start a turn;
        Waiting,                                                                   //After taking any action in the current turn.
        None
    }

    //Determina para qual a direção que o personageme stá olhando
    public enum Facing
    {
        Up = 1,                                                              //Looking to the Tile at X+1
        Right,                                                                  //Looking to the Tile at Y+1
        Down,                                                                   //Looking to the Tile at X-1
        Left                                                                    //Looking to the Tile at Y-1
    }

    public enum UnitType
    {
        Player = 1,
        Enemy,
        Guest
    }

    //Status Attributes
    public string characterName;
    public int level;
    public int healthCurrent;
    public int healthMax;
    public int attack;
    public int defense;
    public int magicpow;
    public int resistance;
    public int accuracy;
    public int evasion;
    public int actionPoints;                                                    //Determines how much points the unit has to use command skills;
    public int movementPoints;                                                  //Impacts how many tiles it can walk in a turn
    public int jumpHeight;                                                      //Impacts the difference of height that the character can overcome
    public int speed;                                                           //Impacts turn order
    public int luck;                                                            //For every 10 in Luck, the characters gains one for critical rate;
    public List<Command.Buff> buffs;
    public List<Command.Debuff> debuffs;


    //HUD Attirbutes
    public Sprite portraitSprite;                                                      //Used to load the current Units protrait in the HUD.

    //Management Attributes
    public UnitType unitType;
    public bool onTurn;
    public Vector2 coord;                                                       //The position of the Unit in the board
    public ActionType action = ActionType.None;
    public bool moved, attacked, interacted;                                    //Determines if the character already done some of those actions
    public Facing facingDirection;

    public List<MovableTile> tilesToMove;                                       //Used for setuping the Tiles for where it can move
    [Range(1, 10)]
    public int movementSpeed;                                                   //Speed of the Animation of the movement, less is faster;


    //Internal
    bool configuring = false;                                                   //Determines if some configuraiton inside Player is ocurring;


    //Common Unit Methods
    public void Refresh()
    {
        onTurn = false;
        moved = false;
        attacked = false;
        lm.board.FindTile(coord).CellDispose();

    }

    /*
    public void StartTurn()
    {
        //Buffs like Regen happens here
    }
    */

    //Modded Attributes
    //Calculates Buffs and Debuffs
    //Calculates Equipments if a Player


    public void MovementSetup()
    {
        Debug.Log("Mover foi selecionado");
        //action = ActionType.Move;

        if (!configuring)
        {
            configuring = true;
            tilesToMove = new List<MovableTile>();
            List<Tile> alreadyListed = new List<Tile>
            {
                lm.board.FindTile(coord)                                           //Adiciona o Tile ond eo jogador está.
            };

            //Varre os Tiles proximos e adiciona a lista de tiles aonde pode se mover;
            for (int i = 1; i <= movementPoints; i++)
            {
                if (i == 1)
                {
                    List<Tile> tilesAround = lm.board.TilesAround(coord, jumpHeight);
                    foreach (Tile t in tilesAround)
                    {
                        if (!t.isBlocked)
                        {
                            tilesToMove.Add(new MovableTile(t, i));
                            alreadyListed.Add(t);
                        }
                    }
                }
                else
                {
                    List<MovableTile> temp = new List<MovableTile>();
                    foreach (MovableTile mT in tilesToMove)
                    {
                        //Verifica os Tiles ao redor dos Tiles verificados na iteração anterior
                        if (mT.cost == i - 1)
                        {
                            List<Tile> tilesAround = lm.board.TilesAround(mT.tile.coord, jumpHeight);
                            foreach (Tile t in tilesAround)
                            {
                                if (!alreadyListed.Contains(t) && !t.isBlocked)
                                {
                                    temp.Add(new MovableTile(t, i, mT.tile));
                                    alreadyListed.Add(t);
                                }
                            }
                        }
                    }
                    //Adiciona todos elementos da lista temporária a lista principal
                    tilesToMove.AddRange(temp);
                }
            }

            //Configura os Tiles aonde pode andar, para receberem o click
            foreach (MovableTile mT in tilesToMove)
            {
                mT.tile.CellMovable();
            }
            configuring = false;
        }
    }

    public MovableTile FindTileToMove(Tile tile)
    {
        foreach (MovableTile mT in tilesToMove)
        {
            if (mT.tile == tile)
            {
                return mT;
            }
        }
        return null;
    }

    public void Moved(Vector2 _coord)
    {
        Tile t = lm.board.FindTile(coord);
        t.CellDispose();
        t.RemoveObject(ObjectType.Unit);

        moved = true;

        if (attacked)
            action = ActionType.Waiting;
        else
            action = ActionType.Ready;

        foreach (MovableTile mT in tilesToMove)
        {
            mT.tile.CellDispose();
        }

        lm.board.FindTile(_coord).AddObject(gameObject, ObjectType.Unit);
        coord = _coord;
        tilesToMove.Clear();
    }

    public void CancelUnitMovement()
    {
        if(tilesToMove != null && tilesToMove.Count > 0)
        {
            foreach (MovableTile mT in tilesToMove)
            {
                mT.tile.CellDispose();
            }
            tilesToMove.Clear();
        }
    }

}

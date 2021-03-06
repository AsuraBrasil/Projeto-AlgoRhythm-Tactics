﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ActionType = Unit.ActionType;
using TargetType = Command.TargetType;

//Elemento principal do sistema de batalha
public class LevelManager : MonoBehaviour
{
    public enum Status
    {
        Moving,
        Acting,
        Selecting,
        Waiting
    }

    [HideInInspector]
    public static LevelManager _instance;

    [HideInInspector]
    public Board board;                                                         //Reference to our board

    [HideInInspector]
    public HUDManager hud;                                                      //Reference to our HUD

    [HideInInspector]
    public TurnManager turn;                                                    //Reference to our Turn Manager

    [HideInInspector]
    public ActionProgramManager actionProgram;                                  //Reference to our Turn Manager


    public string mapName;
    public List<GameObject> playersPrefabs;
    public Unit turnPlayer;
    public bool isRunning;
    public Status currentStatus;

    //
    public GameObject turnHighlightArrow;

    List<Tile> tilesTargeted;

    void Awake()
    {
        if (_instance == null)
            _instance = this;
        else
            Destroy(this);

        board = GameObject.FindWithTag("Board").GetComponent<Board>();
        currentStatus = Status.Waiting;
        tilesTargeted = new List<Tile>();
        isRunning = false;
    }

    void Start()
    {
        //Inicializa o tabuleiro
        board.Setup(mapName);

        //Posiciona o Personagem 1
        board.PlayerPlacement(playersPrefabs[0], new Vector2(0, 0));

        //Posiciona o Personagem 2
        board.PlayerPlacement(playersPrefabs[1], new Vector2(4, 3));

        NextPlayerTurn();
    }

    public void AddPlayer(Player p)
    {
        p.action = ActionType.Waiting;
        turn.AddPlayerToTurnList(p);

    }

    public void NextPlayerTurn()
    {
        //Desabilita a função de tomar um turno do jogador que acabou de jogar
        if (turnPlayer != null)
            turnPlayer.Refresh();

        hud.HideMenuPanel();
        hud.HideCancelPanel();

        turn.SetupNextTurn();
    }

    public void HideHighlightArrow()
    {
        if(turnHighlightArrow.activeInHierarchy)
        {
            turnHighlightArrow.SetActive(false);
        }
    }

    public void CancelButtonClick()
    {
        if(!isRunning)
        {
            switch (currentStatus)
            {
                case Status.Selecting:
                case Status.Waiting:
                    Debug.Log("Nenhuma ação está em andamento para ser cancelada. Status: " + ((currentStatus == Status.Waiting)? "Aguardando uma ação." : "Selecionando uma ação."));
                    break;
                case Status.Moving:
                    CancelMovement();
                    break;
                case Status.Acting:
                    hud.HideActionPanel();
                    CancelAction();
                    break;
            }
            ReturnToSelectionMenu();
        }
    }

    public void ReturnToSelectionMenu()
    {
        hud.HideCancelPanel();
        hud.ActivateMenuPanel();
    }

    #region Movement Setup
    //Jogador aperta no botão de se Movimentar e libera as funções de Movimento.
    public void MoveButtonClick()
    {
        if (turnPlayer.moved)
            return;

        turnPlayer.MovementSetup();
        hud.HideMenuPanel();
        hud.ActivateCancelPanel();
        currentStatus = Status.Moving;
    }


    //Recebe o Tile para qual o personagem irá se movimenatr e cria o caminho a ser traçado usando uma pilha.
    public void MoveCharacterToTile(Tile destination)
    {
        isRunning = true;
        hud.HideCancelPanel();

        //Cria pilha dos Tiles do caminho com Backtracking através do
        //previousTile de cada MovableTile
        Stack<MovableTile> path = new Stack<MovableTile>();
        MovableTile temp = turnPlayer.FindTileToMove(destination);

        while (temp != null)
        {
            path.Push(temp);
            temp = turnPlayer.FindTileToMove(temp.previousTile);
        }

        //Movimenta o Personagem
        StartCoroutine(MoveCharacterToPath(path));
    }

    //Gerencia o tempo da animação e chama a função de conclusão do Movimento
    IEnumerator MoveCharacterToPath(Stack<MovableTile> path)
    {
        Tile toMove = null;

        while (path.Count > 0)
        {
            toMove = path.Pop().tile;
            Debug.Log(toMove.coord);
            Vector3 currentPos = turnPlayer.gameObject.transform.position;
            Vector3 destinationPos = new Vector3(toMove.gameObject.transform.position.x, toMove.gameObject.transform.position.y + 1.5f + (toMove.height - 1) * 0.25f, toMove.gameObject.transform.position.z);

            float timeOfMovement = (float)turnPlayer.movementSpeed / 10f;

            //Verifica se há uma diferência de altura do tile atual em comparação ao próximo tile
            if (System.Math.Abs(currentPos.y - destinationPos.y) < float.Epsilon)
            {
                StartCoroutine(MoveUnit(turnPlayer.gameObject, currentPos, destinationPos, timeOfMovement));
            }
            else
            {
                //Calculates a midway point
                Vector3 midwayPoint;
                if (destinationPos.y > currentPos.y)
                {
                    midwayPoint = new Vector3(currentPos.x, currentPos.y + (destinationPos.y - currentPos.y) / 1.25f, currentPos.z);
                    Vector3 midwayPoint2 = new Vector3(currentPos.x + (destinationPos.x - currentPos.x) / 3, destinationPos.y + 0.1f, currentPos.z + (destinationPos.z - currentPos.z) / 3);
                    StartCoroutine(MoveUnit(turnPlayer.gameObject, new Vector3[] { currentPos, midwayPoint, midwayPoint2, destinationPos }, timeOfMovement / 4));
                }
                else
                {
                    midwayPoint = new Vector3(currentPos.x + (destinationPos.x - currentPos.x) / 1.2f, currentPos.y, currentPos.z + (destinationPos.z - currentPos.z) / 1.2f);
                    StartCoroutine(MoveUnit(turnPlayer.gameObject, new Vector3[] { currentPos, midwayPoint, destinationPos }, timeOfMovement / 2.5f));
                }
            }

            yield return new WaitForSeconds(timeOfMovement);
        }

        //Atualiza as coordenadas do personagem que foi movimentado
        turnPlayer.Moved(toMove.coord);

        //TODO: Antes de finalziar o movimento, seria necessário abrr um afunção par apermitir o jogador mudar a direção. 
        //E após isso, na função nova, finalziar o movimento.

        //Finaliza o movimento
        FinishMovement();

        yield break;
    }

    //Without Midpoints movement, just source and destination interpolation
    IEnumerator MoveUnit(GameObject unit, Vector3 source, Vector3 target, float overTime)
    {
        float startTime = Time.time;
        while (Time.time < startTime + overTime)
        {
            float t = (Time.time - startTime) / overTime;
            unit.transform.position = Vector3.Lerp(source, target, t);
            yield return null;
        }

        unit.transform.position = target;
    }

    //Movement with Midway points, generally used for jumping movement
    IEnumerator MoveUnit(GameObject unit, Vector3[] waypoints, float overTime)
    {
        float startTime;
        for (int i = 1; i < waypoints.Length; i++)
        {
            startTime = Time.time;
            while (Time.time < startTime + overTime)
            {
                float t = (Time.time - startTime) / overTime;
                unit.transform.position = Vector3.Lerp(waypoints[i - 1], waypoints[i], t);
                yield return null;
            }

            unit.transform.position = waypoints[i];
        }
    }

    //Realiza algumas configurações após o movimento.
    void FinishMovement()
    {
        if (turnPlayer.action == ActionType.Waiting)
        {
            currentStatus = Status.Waiting;
            NextPlayerTurn();
        }
        else
        {
            ReturnToSelectionMenu();
        }
        isRunning = false;
    }

    //Cancel movement without moving
    void CancelMovement()
    {
        Debug.Log("Mover foi Cancelado.");
        turnPlayer.CancelUnitMovement();
        currentStatus = Status.Selecting;
    }

#endregion

    public void ActionButtonClick()
    {
        if (turnPlayer.attacked)
            return;

        hud.HideMenuPanel();
        hud.ActivateActionPanel();
        hud.ActivateCancelPanel();
        actionProgram.UpdateActionPoints();
        actionProgram.UpdateCommandSet();
        currentStatus = Status.Acting;
    }

    //Cancel action without acting
    void CancelAction()
    {
        Debug.Log("Tomar Ação foi Cancelado.");
        actionProgram.ClearProgram();
        currentStatus = Status.Selecting;
    }

    //Get all cells that will be affected by a Command
    public void GetCommandTargetCells(TargetType _targetType, bool heightCalc)
    {
        tilesTargeted.Clear();

        int h = 0;
        if (heightCalc)
            h = board.FindTile(turnPlayer.coord).height;

        switch (_targetType)
        {
            case TargetType.Self:
                tilesTargeted.Add(board.FindTile(turnPlayer.coord));
                break;

            case TargetType.Front:
                Tile front = board.TargetFrontTile(turnPlayer.facingDirection, turnPlayer.coord);
                if(front != null && !front.isBlocked)
                {
                    if (heightCalc)
                    {
                        if (CalculateTargetHeight(h, front.height))
                            tilesTargeted.Add(front);
                    }
                    else
                        tilesTargeted.Add(front);
                }
                break;

            case TargetType.Triangle:
                if (heightCalc)
                {
                    List<Tile> triangle = board.TargetFrontTriangle(turnPlayer.facingDirection, turnPlayer.coord);
                    foreach (Tile t in triangle)
                    {
                        if (CalculateTargetHeight(h, t.height))
                            tilesTargeted.Add(t);
                    }
                }
                else
                    tilesTargeted = board.TargetFrontTriangle(turnPlayer.facingDirection, turnPlayer.coord);
                break;
        }
    }

    public void TargetCells(TargetType _targetType, bool heightCalc, bool offensive)
    {
        GetCommandTargetCells(_targetType, heightCalc);

        if(offensive)
            foreach(Tile t in tilesTargeted)
            {
                    t.CellOnRedTarget();
            }
        else
            foreach (Tile t in tilesTargeted)
            {
                t.CellOnBlueTarget();
            } 
    }

    public void UntargetCells()
    {
        foreach (Tile t in tilesTargeted)
        {
            t.CellDispose();
            //Se a célula onde estava o alvejamento for a mesma do jogador (Target.Self)
            //Volt a piscar em amarelo.
            if (t.coord == turnPlayer.coord)
                t.CellOnPlayerSelect();
        }
    }

    public bool CalculateTargetHeight(int playerTileHeight, int anotherTileHeight)
    {
        if (Mathf.Abs(playerTileHeight - anotherTileHeight) < turnPlayer.jumpHeight)
            return true;
        
        return false;
    }

    //Wait
    public void WaitButtonClick()
    {
        NextPlayerTurn();
    }


}//END LevelManager

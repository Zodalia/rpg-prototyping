using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class BattleController : MonoBehaviour
{
    [Header("Setup")]
    [SerializeField] private List<UnitDefinition> playerUnits = new();
    [SerializeField] private List<UnitDefinition> enemyUnits = new();

    public BattleState State { get; private set; }

    public event Action StateChanged;
    public event Action<UnitState, List<ActionDefinition>> PlayerInputRequested;
    public event Action<string> BattleEnded;

    private CombatRules _rules;
    private ActionExecutor _executor;
    private EnemyAi _enemyAi;

    private void Awake()
    {
        _rules = new CombatRules();
        _executor = new ActionExecutor();
        _enemyAi = new EnemyAi();
    }

    private void Start()
    {
        StartBattle();
    }

    public void StartBattle()
    {
        State = new BattleState();

        int idCounter = 0;

        foreach (var def in playerUnits)
        {
            var unit = new UnitState($"P{idCounter++}", "Player", def);
            State.Units.Add(unit);
            _rules.InitializeResources(State, unit);
        }

        foreach (var def in enemyUnits)
        {
            var unit = new UnitState($"E{idCounter++}", "Enemy", def);
            State.Units.Add(unit);
            _rules.InitializeResources(State, unit);
        }

        BeginNextTurn();
        StateChanged?.Invoke();
    }

    private void BeginNextTurn()
    {
        if (CheckBattleEnd())
            return;

        State.ActiveUnit = _rules.GetNextActiveUnit(State);
        _rules.OnTurnStart(State, State.ActiveUnit);

        StateChanged?.Invoke();

        if (State.ActiveUnit.Team == "Player")
        {
            var actions = _rules.GetAvailableActions(State, State.ActiveUnit);
            PlayerInputRequested?.Invoke(State.ActiveUnit, actions);
        }
        else
        {
            var aiAction = _enemyAi.ChooseAction(State, State.ActiveUnit, _rules);
            SubmitAction(aiAction);
        }
    }

    public void SubmitAction(ActionExecution execution)
    {
        _executor.Execute(State, execution, _rules);
        _rules.OnTurnEnd(State, execution.Actor);

        StateChanged?.Invoke();

        if (!CheckBattleEnd())
        {
            State.TurnNumber++;
            BeginNextTurn();
        }
    }

    private bool CheckBattleEnd()
    {
        bool anyPlayers = State.GetLivingTeam("Player").Any();
        bool anyEnemies = State.GetLivingTeam("Enemy").Any();

        if (anyPlayers && anyEnemies)
            return false;

        State.IsBattleOver = true;
        State.WinnerTeam = anyPlayers ? "Player" : "Enemy";
        BattleEnded?.Invoke(State.WinnerTeam);
        return true;
    }

    public void NotifyStateChanged() => StateChanged?.Invoke();
}
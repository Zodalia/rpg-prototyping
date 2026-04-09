using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class BattleController : MonoBehaviour
{
    public BattleState State { get; private set; }
    public TurnOrderStrategy TurnOrderStrategy { get; private set; }
    public DeathConditionStrategy DeathConditionStrategy { get; private set; }
    public EnemyAi EnemyAi => _enemyAi;
    public List<TurnEffectDefinition> GlobalTurnEffects { get; private set; } = new();
    public BattleDefinition CurrentBattle { get; private set; }
    public PlayerPartyDefinition CurrentParty { get; private set; }

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

    public void StartBattle(BattleDefinition battle, PlayerPartyDefinition party)
    {
        CurrentBattle = battle;
        CurrentParty = party;

        State = new BattleState();
        State.EventBus = new BattleEventBus();

        TurnOrderStrategy = battle.TurnOrderStrategy;
        DeathConditionStrategy = battle.DeathConditionStrategy;
        GlobalTurnEffects = battle.GlobalTurnEffects ?? new List<TurnEffectDefinition>();

        _rules.DeathCondition = DeathConditionStrategy;

        foreach (var entry in battle.StartingGlobalResources)
        {
            if (entry.Resource != null)
                _rules.InitializeGlobalResource(State, entry.Resource, entry.InitialValue);
        }

        int idCounter = 0;

        foreach (var def in party.Units)
        {
            var unit = new UnitState($"P{idCounter++}", "Player", def);
            State.Units.Add(unit);
            _rules.InitializeResources(State, unit);
        }

        foreach (var def in battle.Enemies)
        {
            var unit = new UnitState($"E{idCounter++}", "Enemy", def);
            State.Units.Add(unit);
            _rules.InitializeResources(State, unit);
        }

        foreach (var poolDef in battle.StartingPools)
        {
            if (poolDef != null)
            {
                var pool = new PoolInstance($"pool-{State.Pools.Count}", poolDef);
                State.Pools.Add(pool);
            }
        }

        BeginNextTurn();
        StateChanged?.Invoke();
    }

    private void BeginNextTurn()
    {
        if (CheckBattleEnd())
            return;

        State.ActiveUnit = TurnOrderStrategy != null
            ? TurnOrderStrategy.SelectNext(State, _rules)
            : _rules.GetNextActiveUnit(State);

        State.EventBus.Raise(new TurnStartedEvent(State.TurnNumber, State.ActiveUnit));
        _rules.OnTurnStart(State, State.ActiveUnit, GlobalTurnEffects);

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
        _rules.OnTurnEnd(State, execution.Actor, GlobalTurnEffects);

        State.EventBus.Raise(new TurnEndedEvent(State.TurnNumber, execution.Actor));
        State.UnitsActedThisRound.Add(execution.Actor.UnitId);

        StateChanged?.Invoke();
    }

    public bool AdvanceTurn()
    {
        if (CheckBattleEnd())
            return false;

        State.TurnNumber++;
        BeginNextTurn();
        return true;
    }

    private bool CheckBattleEnd()
    {
        bool anyPlayers = State.GetLivingTeam("Player").Any();
        bool anyEnemies = State.GetLivingTeam("Enemy").Any();

        if (anyPlayers && anyEnemies)
            return false;

        State.IsBattleOver = true;
        State.WinnerTeam = anyPlayers ? "Player" : "Enemy";
        State.EventBus.Raise(new BattleEndedEvent(State.TurnNumber, State.WinnerTeam));
        BattleEnded?.Invoke(State.WinnerTeam);
        return true;
    }

    public void NotifyStateChanged() => StateChanged?.Invoke();
}
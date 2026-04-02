using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public abstract class TurnOrderStrategy : ScriptableObject
{
    [field: SerializeField] public bool EnforceRounds { get; private set; }

    public UnitState SelectNext(BattleState state, CombatRules rules)
    {
        var candidates = state.LivingUnits.ToList();

        if (EnforceRounds)
        {
            var remaining = candidates
                .Where(u => !state.UnitsActedThisRound.Contains(u.UnitId))
                .ToList();

            if (remaining.Count == 0)
            {
                state.UnitsActedThisRound.Clear();
                remaining = candidates;
            }

            candidates = remaining;
        }

        if (candidates.Count == 0)
            return null;

        var selected = SelectFromCandidates(candidates, state, rules);

        if (selected == null)
        {
            Debug.LogWarning($"[{name}] No unit selected from {candidates.Count} candidates. Falling back to first.");
            selected = candidates[0];
        }

        return selected;
    }

    protected abstract UnitState SelectFromCandidates(
        List<UnitState> candidates, BattleState state, CombatRules rules);

    public virtual ResourceDefinition DisplayResource => null;

    public virtual List<TurnPreviewEntry> GetTurnPreview(
        BattleState state, EnemyAi enemyAi, int count,
        List<TurnEffectDefinition> globalTurnEffects = null,
        ActionDefinition playerHoverAction = null,
        UnitState playerActor = null)
    {
        var result = new List<TurnPreviewEntry>(count);
        if (count == 0) return result;

        var current = state.ActiveUnit;
        if (current == null || !current.IsAlive) return result;

        var snapshot = new ResourceSnapshot(state);
        var simRules = new CombatRules { ActiveSnapshot = snapshot };

        // Slot 0: current active unit
        result.Add(new TurnPreviewEntry(current, GetDisplayValue(snapshot, state, current)));
        SimulateUnitAction(snapshot, state, simRules, enemyAi, current,
            current == playerActor ? playerHoverAction : null);
        ActionSimulator.SimulateAllTurnEffects(snapshot, state, current, TurnTickTiming.TurnEnd, globalTurnEffects);
        snapshot.UnitsActedThisRound.Add(current.UnitId);

        // Slots 1..N
        for (int i = 1; i < count; i++)
        {
            var candidates = state.LivingUnits.ToList();

            if (EnforceRounds)
            {
                var remaining = candidates
                    .Where(u => !snapshot.UnitsActedThisRound.Contains(u.UnitId))
                    .ToList();

                if (remaining.Count == 0)
                {
                    snapshot.UnitsActedThisRound.Clear();
                    remaining = candidates;
                }

                candidates = remaining;
            }

            if (candidates.Count == 0) break;

            var selected = SelectFromCandidates(candidates, state, simRules);
            if (selected == null) selected = candidates[0];

            ActionSimulator.SimulateAllTurnEffects(snapshot, state, selected, TurnTickTiming.TurnStart, globalTurnEffects);
            result.Add(new TurnPreviewEntry(selected, GetDisplayValue(snapshot, state, selected)));
            //SimulateUnitAction(snapshot, state, simRules, enemyAi, selected, null);
            ActionSimulator.SimulateAllTurnEffects(snapshot, state, selected, TurnTickTiming.TurnEnd, globalTurnEffects);
            snapshot.UnitsActedThisRound.Add(selected.UnitId);
        }

        return result;
    }

    private int GetDisplayValue(ResourceSnapshot snapshot, BattleState state, UnitState unit)
    {
        var res = DisplayResource;
        if (res == null) return 0;
        return snapshot.GetResource(state, unit, res.Id);
    }

    private void SimulateUnitAction(
        ResourceSnapshot snapshot, BattleState state, CombatRules simRules,
        EnemyAi enemyAi, UnitState unit, ActionDefinition overrideAction)
    {
        ActionDefinition action;
        List<UnitState> targets;

        if (overrideAction != null)
        {
            action = overrideAction;
            targets = ActionSimulator.ResolveDefaultTargets(state, unit, action);
        }
        else
        {
            var available = simRules.GetAvailableActions(state, unit);
            if (available.Count == 0) return;

            if (unit.Team != "Player" && enemyAi != null)
            {
                var prediction = enemyAi.ChooseAction(state, unit, simRules);
                action = prediction.Action;
                targets = prediction.Targets;
            }
            else
            {
                action = available[0];
                targets = ActionSimulator.ResolveDefaultTargets(state, unit, action);
            }
        }

        ActionSimulator.SimulateResourceEffects(snapshot, state, unit, action, targets);
    }
}

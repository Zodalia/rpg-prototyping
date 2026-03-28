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
}

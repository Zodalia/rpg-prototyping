using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Combat/Turn Order/Resource Rank")]
public sealed class ResourceRankTurnOrder : TurnOrderStrategy
{
    [field: SerializeField] public ResourceDefinition OrderingResource { get; private set; }
    [field: SerializeField] public bool Descending { get; private set; } = true;

    protected override UnitState SelectFromCandidates(
        List<UnitState> candidates, BattleState state, CombatRules rules)
    {
        if (OrderingResource == null)
        {
            Debug.LogWarning($"[{name}] No ordering resource assigned. Picking first candidate.");
            return candidates[0];
        }

        var ordered = Descending
            ? candidates.OrderByDescending(u => rules.GetResourceAmount(state, u, OrderingResource.Id))
            : candidates.OrderBy(u => rules.GetResourceAmount(state, u, OrderingResource.Id));

        return ordered.First();
    }
}

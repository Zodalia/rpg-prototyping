using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Combat/Turn Order/Resource Threshold")]
public sealed class ResourceThresholdTurnOrder : TurnOrderStrategy
{
    [field: SerializeField] public ResourceDefinition ReadinessResource { get; private set; }
    [field: SerializeField] public int Threshold { get; private set; } = 1;

    [Header("Optional: rank eligible units by a secondary resource")]
    [field: SerializeField] public ResourceDefinition RankingResource { get; private set; }
    [field: SerializeField] public bool RankDescending { get; private set; } = true;

    public override ResourceDefinition DisplayResource => ReadinessResource;

    protected override UnitState SelectFromCandidates(
        List<UnitState> candidates, BattleState state, CombatRules rules)
    {
        if (ReadinessResource == null)
        {
            Debug.LogWarning($"[{name}] No readiness resource assigned. Picking first candidate.");
            return candidates[0];
        }

        var eligible = candidates
            .Where(u => rules.GetResourceAmount(state, u, ReadinessResource.Id) >= Threshold)
            .ToList();

        if (eligible.Count == 0)
            return null; // Base class will log warning and fall back.

        if (eligible.Count == 1 || RankingResource == null)
            return eligible[0];

        var ranked = RankDescending
            ? eligible.OrderByDescending(u => rules.GetResourceAmount(state, u, RankingResource.Id))
            : eligible.OrderBy(u => rules.GetResourceAmount(state, u, RankingResource.Id));

        return ranked.First();
    }
}

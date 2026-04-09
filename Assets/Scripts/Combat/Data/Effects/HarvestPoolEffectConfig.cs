using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public sealed class HarvestPoolEffectConfig : EffectConfig
{
    [SerializeField] private string harvestId;

    public string HarvestId => harvestId;

    public override string DisplayName => "Harvest Pool";

    public override void Apply(BattleState state, ActionExecution execution, CombatRules rules)
    {
        var pool = execution.TargetPool;
        if (pool == null || pool.IsDepleted)
            return;

        var entry = pool.Definition.HarvestEntries
            .FirstOrDefault(e => e.HarvestId == harvestId);

        if (entry.Resource == null)
            return;

        // Gain the resource using the entry's configured scope as priority
        var scopePriority = new List<ResourceOwnershipScope> { entry.OwnershipScope };
        rules.GainResource(state, execution.Actor, entry.Resource, entry.Amount, scopePriority);

        // Deplete the pool
        if (pool.Definition.MaxHarvests >= 0)
        {
            pool.RemainingHarvests--;
        }

        state.EventBus?.Raise(new PoolHarvestedEvent(
            state.TurnNumber, execution.Actor, pool, entry.Resource, entry.Amount));

        if (pool.IsDepleted)
        {
            state.EventBus?.Raise(new PoolDepletedEvent(state.TurnNumber, pool));
        }
    }
}

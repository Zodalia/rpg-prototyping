using System;
using UnityEngine;

[Serializable]
public sealed class SpawnPoolEffectConfig : EffectConfig
{
    [SerializeField] private PoolDefinition poolDefinition;
    [SerializeField] private int initialHarvests = -1;

    public PoolDefinition PoolDefinition => poolDefinition;

    public override string DisplayName => "Spawn Pool";

    public override void Apply(BattleState state, ActionExecution execution, CombatRules rules)
    {
        if (poolDefinition == null)
            return;

        int harvests = initialHarvests >= 0 ? initialHarvests : poolDefinition.MaxHarvests;
        string poolId = $"pool-{state.Pools.Count}";
        var instance = new PoolInstance(poolId, poolDefinition, harvests);

        state.Pools.Add(instance);
        state.EventBus?.Raise(new PoolSpawnedEvent(state.TurnNumber, instance));
    }
}

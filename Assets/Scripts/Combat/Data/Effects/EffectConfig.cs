using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public abstract class EffectConfig
{
    [SerializeField] private EffectTargetOverride targetOverride;

    public EffectTargetOverride TargetOverride => targetOverride;

    public abstract string DisplayName { get; }
    public abstract void Apply(BattleState state, ActionExecution execution, CombatRules rules);

    protected List<UnitState> ResolveTargets(BattleState state, ActionExecution execution)
    {
        return targetOverride switch
        {
            EffectTargetOverride.Self => new List<UnitState> { execution.Actor },
            EffectTargetOverride.AllAllies => state.GetAlliesOf(execution.Actor).ToList(),
            EffectTargetOverride.AllEnemies => state.GetEnemiesOf(execution.Actor).ToList(),
            _ => execution.Targets,
        };
    }
}
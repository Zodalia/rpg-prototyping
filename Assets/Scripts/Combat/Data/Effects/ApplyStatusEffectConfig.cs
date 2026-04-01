using System;
using UnityEngine;

[Serializable]
public sealed class ApplyStatusEffectConfig : EffectConfig
{
    [SerializeField] private StatusDefinition status;
    [SerializeField] private int duration = 2;

    public override string DisplayName => "Apply Status";

    public override void Apply(BattleState state, ActionExecution execution, CombatRules rules)
    {
        if (status == null)
            return;

        foreach (var target in ResolveTargets(state, execution))
        {
            var appliedDuration = duration > 0 ? duration : status.DefaultDuration;
            target.Statuses.Add(new StatusInstance(status, appliedDuration, status.TickTiming, status.TrackingScope, target.Team));
            state.EventBus.Raise(new StatusAppliedEvent(state.TurnNumber, target, status, appliedDuration));
        }
    }
}
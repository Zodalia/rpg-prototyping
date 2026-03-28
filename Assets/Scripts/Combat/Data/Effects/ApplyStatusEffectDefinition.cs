using UnityEngine;

[CreateAssetMenu(menuName = "Combat/Effects/Apply Status")]
public sealed class ApplyStatusEffectDefinition : EffectDefinition
{
    [field: SerializeField] public StatusDefinition Status { get; private set; }
    [field: SerializeField] public int Duration { get; private set; } = 2;

    public override void Apply(BattleState state, ActionExecution execution, CombatRules rules)
    {
        if (Status == null)
            return;

        foreach (var target in execution.Targets)
        {
            var duration = Duration > 0 ? Duration : Status.DefaultDuration;
            target.Statuses.Add(new StatusInstance(Status, duration, Status.TickTiming, Status.TrackingScope, target.Team));
            state.Log.Add($"{target.Definition.DisplayName} gains {Status.DisplayName}");
        }
    }
}
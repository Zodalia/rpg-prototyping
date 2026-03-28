public sealed class ActionExecutor
{
    public void Execute(BattleState state, ActionExecution execution, CombatRules rules)
    {
        var actor = execution.Actor;
        var action = execution.Action;

        actor.Mp -= action.MpCost;
        actor.ActionPoints -= action.ApCost;

        if (action.Cooldown > 0)
            actor.Cooldowns[action.Id] = action.Cooldown;

        state.Log.Add($"{actor.Definition.DisplayName} uses {action.DisplayName}");

        foreach (var effect in action.Effects)
        {
            if (effect != null)
                effect.Apply(state, execution, rules);
        }
    }
}
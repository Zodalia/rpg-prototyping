public sealed class ActionExecutor
{
    public void Execute(BattleState state, ActionExecution execution, CombatRules rules)
    {
        var actor = execution.Actor;
        var action = execution.Action;

        foreach (var req in action.ResourceRequirements)
        {
            if (req.Resource == null)
                continue;

            rules.SpendResource(state, actor, req.Resource.Id, req.Amount);
        }

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
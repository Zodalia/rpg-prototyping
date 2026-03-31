using System.Collections.Generic;
using System.Linq;

public static class ActionSimulator
{
    public static void SimulateResourceEffects(
        ResourceSnapshot snapshot, BattleState state, UnitState actor,
        ActionDefinition action, List<UnitState> targets)
    {
        // Simulate resource requirement costs
        foreach (var req in action.ResourceRequirements)
        {
            if (req.Resource == null)
                continue;

            snapshot.SpendResource(state, actor, req.Resource, req.Amount);
        }

        // Simulate resource-affecting effects
        foreach (var effect in action.Effects)
        {
            switch (effect)
            {
                case GainResourceEffectConfig gain when gain.Resource != null:
                    foreach (var target in targets)
                        snapshot.GainResource(state, target, gain.Resource, gain.Amount);
                    break;

                case SpendResourceEffectConfig spend when spend.Resource != null:
                    foreach (var target in targets)
                        snapshot.SpendResource(state, target, spend.Resource, spend.Amount);
                    break;

                case SetResourceEffectConfig set when set.Resource != null:
                    foreach (var target in targets)
                        snapshot.SetResource(state, target, set.Resource, set.Value);
                    break;
            }
        }
    }

    public static List<UnitState> ResolveDefaultTargets(
        BattleState state, UnitState actor, ActionDefinition action)
    {
        return action.Targeting switch
        {
            ActionDefinition.TargetType.Self => new List<UnitState> { actor },
            ActionDefinition.TargetType.SingleEnemy => state.GetEnemiesOf(actor).Take(1).ToList(),
            ActionDefinition.TargetType.AllEnemies => state.GetEnemiesOf(actor).ToList(),
            ActionDefinition.TargetType.SingleAlly => state.GetAlliesOf(actor).Take(1).ToList(),
            ActionDefinition.TargetType.AllAllies => state.GetAlliesOf(actor).ToList(),
            _ => new List<UnitState>()
        };
    }
}

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
            var effectTargets = ResolveEffectTargets(state, actor, targets, effect);

            switch (effect)
            {
                case GainResourceEffectConfig gain when gain.Resource != null:
                    foreach (var target in effectTargets)
                        snapshot.GainResource(state, target, gain.Resource, gain.Amount);
                    break;

                case SpendResourceEffectConfig spend when spend.Resource != null:
                    foreach (var target in effectTargets)
                        snapshot.SpendResource(state, target, spend.Resource, spend.Amount);
                    break;

                case SetResourceEffectConfig set when set.Resource != null:
                    foreach (var target in effectTargets)
                        snapshot.SetResource(state, target, set.Resource, set.Value);
                    break;
            }
        }
    }

    private static List<UnitState> ResolveEffectTargets(
        BattleState state, UnitState actor, List<UnitState> actionTargets, EffectConfig effect)
    {
        return effect.TargetOverride switch
        {
            EffectTargetOverride.Self => new List<UnitState> { actor },
            EffectTargetOverride.AllAllies => state.GetAlliesOf(actor).ToList(),
            EffectTargetOverride.AllEnemies => state.GetEnemiesOf(actor).ToList(),
            _ => actionTargets,
        };
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

    public static void SimulateTurnEffects(
        ResourceSnapshot snapshot, BattleState state, UnitState activeUnit,
        TurnTickTiming timing, List<TurnEffectDefinition> effects, UnitState owner)
    {
        if (effects == null) return;

        foreach (var turnEffect in effects)
        {
            if (turnEffect == null || turnEffect.Timing != timing)
                continue;

            var targets = CombatRules.ResolveTurnEffectTargets(state, owner, turnEffect.TargetScope);
            if (targets.Count == 0)
                continue;

            foreach (var effect in turnEffect.Effects)
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
    }

    public static void SimulateAllTurnEffects(
        ResourceSnapshot snapshot, BattleState state, UnitState activeUnit,
        TurnTickTiming timing, List<TurnEffectDefinition> globalTurnEffects)
    {
        // Unit innate turn effects
        SimulateTurnEffects(snapshot, state, activeUnit, timing,
            activeUnit.Definition.TurnEffects, activeUnit);

        // Status turn effects for all living units
        foreach (var unit in state.LivingUnits)
        {
            foreach (var status in unit.Statuses)
            {
                if (status.Definition.TurnEffects != null)
                    SimulateTurnEffects(snapshot, state, activeUnit, timing,
                        status.Definition.TurnEffects, unit);
            }
        }

        // Global turn effects
        if (globalTurnEffects != null)
            SimulateTurnEffects(snapshot, state, activeUnit, timing,
                globalTurnEffects, activeUnit);
    }
}

using System.Linq;
using System.Collections.Generic;

public sealed class EnemyAi
{
    public ActionExecution ChooseAction(BattleState state, UnitState actor, CombatRules rules)
    {
        var available = rules.GetAvailableActions(state, actor);
        var chosen = available.First();

        if (chosen.Targeting == ActionDefinition.TargetType.Pool)
        {
            var pool = state.ActivePools.FirstOrDefault();
            if (pool != null)
                return new ActionExecution(actor, chosen, pool);
            // Fallback: no pool available, use empty targets
            return new ActionExecution(actor, chosen, new List<UnitState>());
        }

        var targets = ResolveDefaultTargets(state, actor, chosen);
        return new ActionExecution(actor, chosen, targets);
    }

    private List<UnitState> ResolveDefaultTargets(BattleState state, UnitState actor, ActionDefinition action)
    {
        switch (action.Targeting)
        {
            case ActionDefinition.TargetType.Self:
                return new List<UnitState> { actor };

            case ActionDefinition.TargetType.SingleEnemy:
                return new List<UnitState> { state.GetEnemiesOf(actor).First() };

            case ActionDefinition.TargetType.AllEnemies:
                return state.GetEnemiesOf(actor).ToList();

            case ActionDefinition.TargetType.SingleAlly:
                return new List<UnitState> { state.GetAlliesOf(actor).First() };

            case ActionDefinition.TargetType.AllAllies:
                return state.GetAlliesOf(actor).ToList();

            default:
                return new List<UnitState>();
        }
    }
}
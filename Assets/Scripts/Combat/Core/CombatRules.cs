using System.Collections.Generic;
using System.Linq;

public sealed class CombatRules
{
    public UnitState GetNextActiveUnit(BattleState state)
    {
        // Very simple baseline. Replace later with timeline logic if needed.
        return state.LivingUnits
            .OrderByDescending(u => u.Definition.Speed)
            .FirstOrDefault(u => u != state.ActiveUnit);
    }

    public List<ActionDefinition> GetAvailableActions(BattleState state, UnitState unit)
    {
        return unit.Definition.Actions
            .Where(action => CanUseAction(state, unit, action))
            .ToList();
    }

    public bool CanUseAction(BattleState state, UnitState unit, ActionDefinition action)
    {
        if (!unit.IsAlive)
            return false;

        if (unit.HasStun())
            return false;

        if (unit.Mp < action.MpCost)
            return false;

        if (unit.ActionPoints < action.ApCost)
            return false;

        if (unit.Cooldowns.TryGetValue(action.Id, out int cooldown) && cooldown > 0)
            return false;

        return true;
    }

    public int CalculateDamage(UnitState actor, UnitState target, int power)
    {
        int raw = actor.GetAttack() + power - target.GetDefense();
        return raw < 1 ? 1 : raw;
    }

    public void OnTurnStart(BattleState state, UnitState unit)
    {
        unit.ActionPoints = 1;
        TickStatuses(state, unit, TurnTickTiming.TurnStart);
    }

    public void OnTurnEnd(BattleState state, UnitState unit)
    {
        TickStatuses(state, unit, TurnTickTiming.TurnEnd);
        ReduceCooldowns(unit);
    }

    private void TickStatuses(BattleState state, UnitState activeUnit, TurnTickTiming timing)
    {
        foreach (var unit in state.Units.Where(u => u.IsAlive))
        {
            var nextStatuses = new List<StatusInstance>();

            foreach (var status in unit.Statuses)
            {
                if (status.TickTiming != timing)
                {
                    nextStatuses.Add(status);
                    continue;
                }

                if (IsTurnCounted(status, unit, activeUnit))
                {
                    if (status.Definition.DamagePerTick > 0)
                    {
                        unit.Hp -= status.Definition.DamagePerTick;
                        state.Log.Add($"{unit.Definition.DisplayName} takes {status.Definition.DamagePerTick} from {status.Definition.DisplayName}");
                    }

                    status.RemainingTurns--;
                }

                if (status.RemainingTurns > 0)
                {
                    nextStatuses.Add(status);
                }
                else
                {
                    state.Log.Add($"{unit.Definition.DisplayName} loses {status.Definition.DisplayName}");
                }
            }

            unit.Statuses.Clear();
            unit.Statuses.AddRange(nextStatuses);

            if (unit.Hp <= 0)
            {
                unit.Hp = 0;
                unit.IsAlive = false;
                state.Log.Add($"{unit.Definition.DisplayName} is defeated");
            }
        }
    }

    private bool IsTurnCounted(StatusInstance status, UnitState ownerUnit, UnitState activeUnit)
    {
        return status.TrackingScope switch
        {
            TeamTrackingScope.All => true,
            TeamTrackingScope.Self => ownerUnit.UnitId == activeUnit.UnitId,
            TeamTrackingScope.Allies => ownerUnit.Team == activeUnit.Team,
            TeamTrackingScope.Enemies => ownerUnit.Team != activeUnit.Team,
            _ => true,
        };
    }

    private void ReduceCooldowns(UnitState unit)
    {
        var keys = unit.Cooldowns.Keys.ToList();
        foreach (var key in keys)
        {
            unit.Cooldowns[key]--;
            if (unit.Cooldowns[key] <= 0)
                unit.Cooldowns.Remove(key);
        }
    }
}
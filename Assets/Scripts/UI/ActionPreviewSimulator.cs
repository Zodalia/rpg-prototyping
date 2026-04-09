using System.Collections.Generic;
using UnityEngine;

public static class ActionPreviewSimulator
{
    public static List<string> Simulate(BattleState realState, UnitState realActor, ActionDefinition action)
    {
        var (sandbox, actor, target, dummyDef) = CreateSandbox(realState, realActor);

        try
        {
            var targets = ResolvePreviewTargets(action, actor, target);

            var events = new List<BattleEvent>();
            sandbox.EventBus.EventRaised += e => events.Add(e);

            var rules = new CombatRules();
            var executor = new ActionExecutor();
            executor.Execute(sandbox, new ActionExecution(actor, action, targets), rules);

            var lines = new List<string>();
            foreach (var e in events)
            {
                var line = FormatPreviewEvent(e, actor, target);
                if (line != null)
                    lines.Add(line);
            }
            return lines;
        }
        finally
        {
            Object.Destroy(dummyDef);
        }
    }

    private static (BattleState sandbox, UnitState actor, UnitState target, UnitDefinition dummyDef)
        CreateSandbox(BattleState realState, UnitState realActor)
    {
        var sandbox = new BattleState();
        sandbox.EventBus = new BattleEventBus();
        sandbox.TurnNumber = realState.TurnNumber;

        // Clone actor with current stats and resources
        var actor = new UnitState("preview-actor", realActor.Team, realActor.Definition);
        actor.Hp = realActor.Hp;
        foreach (var kvp in realActor.Resources)
            actor.Resources[kvp.Key] = new ResourceInstance(
                kvp.Value.Definition, kvp.Value.CurrentValue, kvp.Value.MaxValue);
        actor.Statuses.AddRange(realActor.Statuses);
        foreach (var kvp in realActor.Cooldowns)
            actor.Cooldowns[kvp.Key] = kvp.Value;

        // Create dummy target on opposite team that cannot die
        var dummyDef = ScriptableObject.CreateInstance<UnitDefinition>();
        string enemyTeam = realActor.Team == "Player" ? "Enemy" : "Player";
        var target = new UnitState("preview-target", enemyTeam, dummyDef);
        target.Hp = int.MaxValue / 2;

        sandbox.Units.Add(actor);
        sandbox.Units.Add(target);

        // Clone team resources
        foreach (var teamKvp in realState.TeamResources)
        {
            var dict = new Dictionary<string, ResourceInstance>();
            foreach (var resKvp in teamKvp.Value)
                dict[resKvp.Key] = new ResourceInstance(
                    resKvp.Value.Definition, resKvp.Value.CurrentValue, resKvp.Value.MaxValue);
            sandbox.TeamResources[teamKvp.Key] = dict;
        }

        // Clone global resources
        foreach (var resKvp in realState.GlobalResources)
            sandbox.GlobalResources[resKvp.Key] = new ResourceInstance(
                resKvp.Value.Definition, resKvp.Value.CurrentValue, resKvp.Value.MaxValue);

        return (sandbox, actor, target, dummyDef);
    }

    private static List<UnitState> ResolvePreviewTargets(
        ActionDefinition action, UnitState actor, UnitState target)
    {
        return action.Targeting switch
        {
            ActionDefinition.TargetType.Self => new List<UnitState> { actor },
            ActionDefinition.TargetType.SingleAlly => new List<UnitState> { actor },
            ActionDefinition.TargetType.AllAllies => new List<UnitState> { actor },
            ActionDefinition.TargetType.SingleEnemy => new List<UnitState> { target },
            ActionDefinition.TargetType.AllEnemies => new List<UnitState> { target },
            _ => new List<UnitState> { target },
        };
    }

    private static string RoleName(UnitState unit, UnitState actor, UnitState target)
    {
        if (unit == target) return "Target";
        if (unit == actor) return "Self";
        return "Unit";
    }

    private static string FormatPreviewEvent(BattleEvent e, UnitState actor, UnitState target)
    {
        return e switch
        {
            DamageDealtEvent dmg =>
                $"{RoleName(dmg.Target, actor, target)} takes {dmg.Amount} damage",
            ResourceChangedEvent res =>
                FormatResourceChanged(res),
            StatusAppliedEvent status =>
                $"{RoleName(status.Target, actor, target)} gains {status.Status.DisplayName} ({status.Duration} turns)",
            _ => null,
        };
    }

    private static string FormatResourceChanged(ResourceChangedEvent res)
    {
        int delta = res.NewValue - res.OldValue;
        if (delta == 0) return null;
        string sign = delta > 0 ? "+" : "";
        return $"{sign}{delta} {res.Resource.DisplayName}";
    }
}

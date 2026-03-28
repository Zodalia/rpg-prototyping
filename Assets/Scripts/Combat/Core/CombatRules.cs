using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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

        foreach (var req in action.ResourceRequirements)
        {
            if (req.Resource == null)
                continue;

            if (GetResourceAmount(state, unit, req.Resource.Id) < req.Amount)
                return false;
        }

        if (unit.Cooldowns.TryGetValue(action.Id, out int cooldown) && cooldown > 0)
            return false;

        return true;
    }

    public int CalculateDamage(UnitState actor, UnitState target, int power)
    {
        int raw = actor.GetAttack() + power - target.GetDefense();
        return raw < 1 ? 1 : raw;
    }

    public void InitializeResources(BattleState state, UnitState unit)
    {
        foreach (var resourceDefinition in unit.Definition.Resources)
        {
            if (resourceDefinition.Resource == null)
                continue;

            InitializeResource(state, unit, resourceDefinition.Resource, resourceDefinition.InitialValue);
        }
    }

    public void OnTurnStart(BattleState state, UnitState unit)
    {
        foreach (var resourceDefinition in unit.Definition.Resources)
        {
            if (resourceDefinition.Resource == null || !resourceDefinition.ResetToValueOnTurnStart)
                continue;

            SetResource(state, unit, resourceDefinition.Resource, resourceDefinition.TurnStartValue);
        }

        TickStatuses(state, unit, TurnTickTiming.TurnStart);
    }

    public void OnTurnEnd(BattleState state, UnitState unit)
    {
        TickStatuses(state, unit, TurnTickTiming.TurnEnd);
        TickResources(state);
        ReduceCooldowns(unit);
    }

    public int GetResourceAmount(BattleState state, UnitState unit, string resourceId)
    {
        // Check unit resources first
        if (unit.Resources.TryGetValue(resourceId, out var unitResource))
            return unitResource.CurrentValue;

        // Check team resources
        if (state.TeamResources.TryGetValue(unit.Team, out var teamResources) &&
            teamResources.TryGetValue(resourceId, out var teamResource))
            return teamResource.CurrentValue;

        // Check global resources
        if (state.GlobalResources.TryGetValue(resourceId, out var globalResource))
            return globalResource.CurrentValue;

        return 0;
    }

    public void GainResource(BattleState state, UnitState unit, ResourceDefinition resource, int amount)
    {
        var resourceId = resource.Id;
        switch (resource.OwnershipScope)
        {
            case ResourceOwnershipScope.Unit:
                if (!unit.Resources.TryGetValue(resourceId, out var unitRes))
                {
                    unitRes = new ResourceInstance(resource, resource.DefaultValue);
                    unit.Resources[resourceId] = unitRes;
                }
                unitRes.CurrentValue += amount;
                break;
            case ResourceOwnershipScope.Team:
                if (!state.TeamResources.TryGetValue(unit.Team, out var teamResDict))
                {
                    teamResDict = new Dictionary<string, ResourceInstance>();
                    state.TeamResources[unit.Team] = teamResDict;
                }
                if (!teamResDict.TryGetValue(resourceId, out var teamRes))
                {
                    teamRes = new ResourceInstance(resource, resource.DefaultValue);
                    teamResDict[resourceId] = teamRes;
                }
                teamRes.CurrentValue += amount;
                break;
            case ResourceOwnershipScope.Global:
                if (!state.GlobalResources.TryGetValue(resourceId, out var globalRes))
                {
                    globalRes = new ResourceInstance(resource, resource.DefaultValue);
                    state.GlobalResources[resourceId] = globalRes;
                }
                globalRes.CurrentValue += amount;
                break;
        }
    }

    public void SpendResource(BattleState state, UnitState unit, string resourceId, int amount)
    {
        // Find the resource and spend
        if (unit.Resources.TryGetValue(resourceId, out var unitRes))
        {
            unitRes.CurrentValue = Mathf.Max(0, unitRes.CurrentValue - amount);
        }
        else if (state.TeamResources.TryGetValue(unit.Team, out var teamResDict) &&
                 teamResDict.TryGetValue(resourceId, out var teamRes))
        {
            teamRes.CurrentValue = Mathf.Max(0, teamRes.CurrentValue - amount);
        }
        else if (state.GlobalResources.TryGetValue(resourceId, out var globalRes))
        {
            globalRes.CurrentValue = Mathf.Max(0, globalRes.CurrentValue - amount);
        }
    }

    public void SetResource(BattleState state, UnitState unit, ResourceDefinition resource, int value)
    {
        var resourceId = resource.Id;
        switch (resource.OwnershipScope)
        {
            case ResourceOwnershipScope.Unit:
                if (!unit.Resources.TryGetValue(resourceId, out var unitRes))
                {
                    unitRes = new ResourceInstance(resource, resource.DefaultValue);
                    unit.Resources[resourceId] = unitRes;
                }
                unitRes.CurrentValue = value;
                break;
            case ResourceOwnershipScope.Team:
                if (!state.TeamResources.TryGetValue(unit.Team, out var teamResDict))
                {
                    teamResDict = new Dictionary<string, ResourceInstance>();
                    state.TeamResources[unit.Team] = teamResDict;
                }
                if (!teamResDict.TryGetValue(resourceId, out var teamRes))
                {
                    teamRes = new ResourceInstance(resource, resource.DefaultValue);
                    teamResDict[resourceId] = teamRes;
                }
                teamRes.CurrentValue = value;
                break;
            case ResourceOwnershipScope.Global:
                if (!state.GlobalResources.TryGetValue(resourceId, out var globalRes))
                {
                    globalRes = new ResourceInstance(resource, resource.DefaultValue);
                    state.GlobalResources[resourceId] = globalRes;
                }
                globalRes.CurrentValue = value;
                break;
        }
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

    private void TickResources(BattleState state)
    {
        // Decay unit resources
        foreach (var unit in state.Units)
        {
            foreach (var resource in unit.Resources.Values)
            {
                resource.Decay();
            }
        }

        // Decay team resources
        foreach (var teamResources in state.TeamResources.Values)
        {
            foreach (var resource in teamResources.Values)
            {
                resource.Decay();
            }
        }

        // Decay global resources
        foreach (var resource in state.GlobalResources.Values)
        {
            resource.Decay();
        }
    }

    private void InitializeResource(BattleState state, UnitState unit, ResourceDefinition resource, int initialValue)
    {
        switch (resource.OwnershipScope)
        {
            case ResourceOwnershipScope.Unit:
                if (!unit.Resources.ContainsKey(resource.Id))
                {
                    unit.Resources[resource.Id] = new ResourceInstance(resource, initialValue);
                }
                break;
            case ResourceOwnershipScope.Team:
                if (!state.TeamResources.TryGetValue(unit.Team, out var teamResources))
                {
                    teamResources = new Dictionary<string, ResourceInstance>();
                    state.TeamResources[unit.Team] = teamResources;
                }

                if (!teamResources.ContainsKey(resource.Id))
                {
                    teamResources[resource.Id] = new ResourceInstance(resource, initialValue);
                }
                break;
            case ResourceOwnershipScope.Global:
                if (!state.GlobalResources.ContainsKey(resource.Id))
                {
                    state.GlobalResources[resource.Id] = new ResourceInstance(resource, initialValue);
                }
                break;
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
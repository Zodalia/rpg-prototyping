using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class CombatRules
{
    public ResourceSnapshot ActiveSnapshot { get; set; }

    public UnitState GetNextActiveUnit(BattleState state)
    {
        // Round-robin fallback when no TurnOrderStrategy is assigned.
        var living = state.LivingUnits.ToList();
        if (living.Count == 0) return null;

        if (state.ActiveUnit == null)
            return living[0];

        int currentIndex = living.IndexOf(state.ActiveUnit);
        int nextIndex = (currentIndex + 1) % living.Count;
        return living[nextIndex];
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

    public void OnTurnStart(BattleState state, UnitState unit, List<TurnEffectDefinition> globalTurnEffects)
    {
        foreach (var resourceDefinition in unit.Definition.Resources)
        {
            if (resourceDefinition.Resource == null || !resourceDefinition.ResetToValueOnTurnStart)
                continue;

            SetResource(state, unit, resourceDefinition.Resource, resourceDefinition.TurnStartValue);
        }

        TickStatuses(state, unit, TurnTickTiming.TurnStart);
        ExecuteAllTurnEffects(state, unit, TurnTickTiming.TurnStart, globalTurnEffects);
    }

    public void OnTurnEnd(BattleState state, UnitState unit, List<TurnEffectDefinition> globalTurnEffects)
    {
        TickStatuses(state, unit, TurnTickTiming.TurnEnd);
        ExecuteAllTurnEffects(state, unit, TurnTickTiming.TurnEnd, globalTurnEffects);
        TickResources(state);
        ReduceCooldowns(unit);
    }

    public int GetResourceAmount(BattleState state, UnitState unit, string resourceId)
    {
        if (ActiveSnapshot != null)
            return ActiveSnapshot.GetResource(state, unit, resourceId);

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
                int oldUnit = unitRes.CurrentValue;
                unitRes.CurrentValue += amount;
                state.EventBus?.Raise(new ResourceChangedEvent(state.TurnNumber, unit, resource, oldUnit, unitRes.CurrentValue));
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
                int oldTeam = teamRes.CurrentValue;
                teamRes.CurrentValue += amount;
                state.EventBus?.Raise(new ResourceChangedEvent(state.TurnNumber, unit, resource, oldTeam, teamRes.CurrentValue));
                break;
            case ResourceOwnershipScope.Global:
                if (!state.GlobalResources.TryGetValue(resourceId, out var globalRes))
                {
                    globalRes = new ResourceInstance(resource, resource.DefaultValue);
                    state.GlobalResources[resourceId] = globalRes;
                }
                int oldGlobal = globalRes.CurrentValue;
                globalRes.CurrentValue += amount;
                state.EventBus?.Raise(new ResourceChangedEvent(state.TurnNumber, unit, resource, oldGlobal, globalRes.CurrentValue));
                break;
        }
    }

    public void SpendResource(BattleState state, UnitState unit, string resourceId, int amount)
    {
        // Find the resource and spend
        if (unit.Resources.TryGetValue(resourceId, out var unitRes))
        {
            int old = unitRes.CurrentValue;
            unitRes.CurrentValue = Mathf.Max(0, unitRes.CurrentValue - amount);
            state.EventBus?.Raise(new ResourceChangedEvent(state.TurnNumber, unit, unitRes.Definition, old, unitRes.CurrentValue));
        }
        else if (state.TeamResources.TryGetValue(unit.Team, out var teamResDict) &&
                 teamResDict.TryGetValue(resourceId, out var teamRes))
        {
            int old = teamRes.CurrentValue;
            teamRes.CurrentValue = Mathf.Max(0, teamRes.CurrentValue - amount);
            state.EventBus?.Raise(new ResourceChangedEvent(state.TurnNumber, unit, teamRes.Definition, old, teamRes.CurrentValue));
        }
        else if (state.GlobalResources.TryGetValue(resourceId, out var globalRes))
        {
            int old = globalRes.CurrentValue;
            globalRes.CurrentValue = Mathf.Max(0, globalRes.CurrentValue - amount);
            state.EventBus?.Raise(new ResourceChangedEvent(state.TurnNumber, unit, globalRes.Definition, old, globalRes.CurrentValue));
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
                int oldUnit = unitRes.CurrentValue;
                unitRes.CurrentValue = value;
                state.EventBus?.Raise(new ResourceChangedEvent(state.TurnNumber, unit, resource, oldUnit, unitRes.CurrentValue));
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
                int oldTeam = teamRes.CurrentValue;
                teamRes.CurrentValue = value;
                state.EventBus?.Raise(new ResourceChangedEvent(state.TurnNumber, unit, resource, oldTeam, teamRes.CurrentValue));
                break;
            case ResourceOwnershipScope.Global:
                if (!state.GlobalResources.TryGetValue(resourceId, out var globalRes))
                {
                    globalRes = new ResourceInstance(resource, resource.DefaultValue);
                    state.GlobalResources[resourceId] = globalRes;
                }
                int oldGlobal = globalRes.CurrentValue;
                globalRes.CurrentValue = value;
                state.EventBus?.Raise(new ResourceChangedEvent(state.TurnNumber, unit, resource, oldGlobal, globalRes.CurrentValue));
                break;
        }
    }

    private void TickStatuses(BattleState state, UnitState activeUnit, TurnTickTiming timing)
    {
        foreach (var unit in state.Units.Where(u => u.IsAlive))
        {
            state.EventBus.BeginGroup();

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
                        state.EventBus.Raise(new StatusTickEvent(state.TurnNumber, unit, status.Definition, status.Definition.DamagePerTick));
                    }

                    status.RemainingTurns--;
                }

                if (status.RemainingTurns > 0)
                {
                    nextStatuses.Add(status);
                }
                else
                {
                    state.EventBus.Raise(new StatusExpiredEvent(state.TurnNumber, unit, status.Definition));
                }
            }

            unit.Statuses.Clear();
            unit.Statuses.AddRange(nextStatuses);

            if (unit.Hp <= 0)
            {
                unit.Hp = 0;
                unit.IsAlive = false;
                state.EventBus.Raise(new UnitDefeatedEvent(state.TurnNumber, unit));
            }

            state.EventBus.EndGroup();
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

    public void InitializeGlobalResource(BattleState state, ResourceDefinition resource, int initialValue)
    {
        if (resource == null) return;
        if (!state.GlobalResources.ContainsKey(resource.Id))
        {
            state.GlobalResources[resource.Id] = new ResourceInstance(resource, initialValue);
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

    // ─────────────────────── Turn Effects ─────────────────────────

    private void ExecuteAllTurnEffects(
        BattleState state, UnitState activeUnit, TurnTickTiming timing,
        List<TurnEffectDefinition> globalTurnEffects)
    {
        // Unit innate turn effects
        ExecuteTurnEffects(state, activeUnit, timing, activeUnit.Definition.TurnEffects, activeUnit);

        // Status turn effects (for all living units)
        foreach (var unit in state.Units.Where(u => u.IsAlive))
        {
            foreach (var status in unit.Statuses)
            {
                if (status.Definition.TurnEffects == null)
                    continue;
                ExecuteTurnEffects(state, activeUnit, timing, status.Definition.TurnEffects, unit);
            }
        }

        // Global turn effects
        if (globalTurnEffects != null)
            ExecuteTurnEffects(state, activeUnit, timing, globalTurnEffects, activeUnit);
    }

    public void ExecuteTurnEffects(
        BattleState state, UnitState activeUnit, TurnTickTiming timing,
        List<TurnEffectDefinition> effects, UnitState owner)
    {
        if (effects == null) return;

        foreach (var turnEffect in effects)
        {
            if (turnEffect == null || turnEffect.Timing != timing)
                continue;

            var targets = ResolveTurnEffectTargets(state, owner, turnEffect.TargetScope);
            if (targets.Count == 0)
                continue;

            var execution = new ActionExecution(owner, null, targets);
            foreach (var effect in turnEffect.Effects)
            {
                if (effect != null)
                    effect.Apply(state, execution, this);
            }
        }
    }

    public static List<UnitState> ResolveTurnEffectTargets(
        BattleState state, UnitState owner, TurnEffectTargetScope scope)
    {
        return scope switch
        {
            TurnEffectTargetScope.Self => owner.IsAlive
                ? new List<UnitState> { owner }
                : new List<UnitState>(),
            TurnEffectTargetScope.AlliesOfOwner => state.GetAlliesOf(owner).ToList(),
            TurnEffectTargetScope.EnemiesOfOwner => state.GetEnemiesOf(owner).ToList(),
            TurnEffectTargetScope.AllUnits => state.LivingUnits.ToList(),
            _ => new List<UnitState>()
        };
    }

    public List<TurnPreviewEntry> GetTurnPreview(BattleState state, int count)
    {
        var living = state.LivingUnits.ToList();
        if (living.Count == 0 || count == 0)
            return new List<TurnPreviewEntry>();

        var result = new List<TurnPreviewEntry>(count);
        int startIndex = state.ActiveUnit != null ? living.IndexOf(state.ActiveUnit) : 0;
        if (startIndex < 0) startIndex = 0;

        for (int i = 0; i < count; i++)
            result.Add(new TurnPreviewEntry(living[(startIndex + i) % living.Count], 0));

        return result;
    }
}
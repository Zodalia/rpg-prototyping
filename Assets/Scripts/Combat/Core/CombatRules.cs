using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class CombatRules
{
    public ResourceSnapshot ActiveSnapshot { get; set; }
    public DeathConditionStrategy DeathCondition { get; set; }

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
            if (req.IsTagBased)
            {
                if (GetTagResourceAmount(state, unit, req.Tag) < req.Amount)
                    return false;
            }
            else
            {
                if (req.Resource == null)
                    continue;

                if (GetResourceAmount(state, unit, req.Resource) < req.Amount)
                    return false;
            }
        }

        if (action.Targeting == ActionDefinition.TargetType.Pool &&
            !state.ActivePools.Any())
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

    public bool CheckAndApplyDeath(BattleState state, UnitState unit, UnitState killer = null)
    {
        if (!unit.IsAlive)
            return true;

        bool dead = DeathCondition != null
            ? DeathCondition.IsDead(unit, state, this)
            : unit.Hp <= 0;

        if (!dead)
            return false;

        unit.Hp = Mathf.Max(0, unit.Hp);
        unit.IsAlive = false;
        state.EventBus?.Raise(new UnitDefeatedEvent(state.TurnNumber, unit, killer));
        return true;
    }

    public void InitializeResources(BattleState state, UnitState unit)
    {
        foreach (var resourceDefinition in unit.Definition.Resources)
        {
            if (resourceDefinition.Resource == null)
                continue;

            InitializeResource(state, unit, resourceDefinition.Resource,
                resourceDefinition.InitialValue, resourceDefinition.MaxValue);
        }
    }

    public void OnTurnStart(BattleState state, UnitState unit, List<TurnEffectDefinition> globalTurnEffects)
    {
        foreach (var resourceDefinition in unit.Definition.Resources)
        {
            if (resourceDefinition.Resource == null || !resourceDefinition.ResetToValueOnTurnStart)
                continue;

            // Reset targets unit-scope pool specifically (turn-start reset is per-unit config).
            SetResource(state, unit, resourceDefinition.Resource, resourceDefinition.TurnStartValue,
                ResourceOwnershipScope.Unit);
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
        // String-only overload: resolve the definition from any existing pool, then aggregate.
        var definition = FindResourceDefinition(state, unit, resourceId);
        if (definition != null)
            return GetResourceAmount(state, unit, definition);

        // No pool exists anywhere — fall back to snapshot if present.
        if (ActiveSnapshot != null)
            return ActiveSnapshot.GetResource(state, unit, resourceId);

        return 0;
    }

    public int GetResourceAmount(BattleState state, UnitState unit, ResourceDefinition resource)
    {
        if (ActiveSnapshot != null)
            return ActiveSnapshot.GetResource(state, unit, resource);

        int total = 0;
        foreach (var scope in resource.AllowedScopes)
        {
            var pool = GetResourcePool(state, unit, resource.Id, scope);
            if (pool != null)
                total += pool.CurrentValue;
        }
        return total;
    }

    public void GainResource(BattleState state, UnitState unit, ResourceDefinition resource, int amount,
        IReadOnlyList<ResourceOwnershipScope> gainPriority = null)
    {
        var priority = gainPriority ?? resource.AllowedScopes;
        int remaining = amount;

        foreach (var scope in priority)
        {
            if (remaining <= 0) break;

            var pool = GetOrCreatePool(state, unit, resource, scope);
            int space = pool.AvailableCapacity;
            int gain = space < remaining ? space : remaining;
            if (gain <= 0) continue;

            int old = pool.CurrentValue;
            pool.CurrentValue += gain;
            remaining -= gain;
            state.EventBus?.Raise(new ResourceChangedEvent(state.TurnNumber, unit, resource, old, pool.CurrentValue, scope));
        }

        if (DeathCondition != null)
            CheckAndApplyDeath(state, unit);
    }

    public void SpendResource(BattleState state, UnitState unit, string resourceId, int amount,
        IReadOnlyList<ResourceOwnershipScope> spendPriority = null)
    {
        var definition = FindResourceDefinition(state, unit, resourceId);
        if (definition != null)
        {
            SpendResource(state, unit, definition, amount, spendPriority);
            return;
        }

        // Fallback for unknown resources: try each scope dictionary in default order.
        SpendFromFirstFound(state, unit, resourceId, amount);
    }

    public void SpendResource(BattleState state, UnitState unit, ResourceDefinition resource, int amount,
        IReadOnlyList<ResourceOwnershipScope> spendPriority = null)
    {
        var priority = spendPriority ?? resource.AllowedScopes;
        int remaining = amount;

        foreach (var scope in priority)
        {
            if (remaining <= 0) break;

            var pool = GetResourcePool(state, unit, resource.Id, scope);
            if (pool == null || pool.CurrentValue <= 0) continue;

            int drain = pool.CurrentValue < remaining ? pool.CurrentValue : remaining;
            int old = pool.CurrentValue;
            pool.CurrentValue -= drain;
            remaining -= drain;
            state.EventBus?.Raise(new ResourceChangedEvent(state.TurnNumber, unit, resource, old, pool.CurrentValue, scope));
        }

        if (DeathCondition != null)
            CheckAndApplyDeath(state, unit);
    }

    public void SetResource(BattleState state, UnitState unit, ResourceDefinition resource, int value,
        ResourceOwnershipScope scope)
    {
        var pool = GetOrCreatePool(state, unit, resource, scope);
        int old = pool.CurrentValue;
        pool.CurrentValue = value;
        state.EventBus?.Raise(new ResourceChangedEvent(state.TurnNumber, unit, resource, old, pool.CurrentValue, scope));

        if (DeathCondition != null)
            CheckAndApplyDeath(state, unit);
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

            CheckAndApplyDeath(state, unit);

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

    public void InitializeGlobalResource(BattleState state, ResourceDefinition resource, int initialValue, int maxValue = -1)
    {
        if (resource == null) return;
        if (!state.GlobalResources.ContainsKey(resource.Id))
        {
            int cap = maxValue >= 0 ? maxValue : resource.DefaultMaxValue;
            state.GlobalResources[resource.Id] = new ResourceInstance(resource, initialValue, cap);
        }
    }

    private void InitializeResource(BattleState state, UnitState unit, ResourceDefinition resource,
        int initialValue, int unitMaxValue = -1)
    {
        foreach (var scope in resource.AllowedScopes)
        {
            int cap = scope == ResourceOwnershipScope.Unit && unitMaxValue >= 0
                ? unitMaxValue
                : resource.DefaultMaxValue;

            switch (scope)
            {
                case ResourceOwnershipScope.Unit:
                    if (!unit.Resources.ContainsKey(resource.Id))
                        unit.Resources[resource.Id] = new ResourceInstance(resource, initialValue, cap);
                    break;
                case ResourceOwnershipScope.Team:
                    if (!state.TeamResources.TryGetValue(unit.Team, out var teamResources))
                    {
                        teamResources = new Dictionary<string, ResourceInstance>();
                        state.TeamResources[unit.Team] = teamResources;
                    }
                    if (!teamResources.ContainsKey(resource.Id))
                        teamResources[resource.Id] = new ResourceInstance(resource, initialValue, cap);
                    break;
                case ResourceOwnershipScope.Global:
                    if (!state.GlobalResources.ContainsKey(resource.Id))
                        state.GlobalResources[resource.Id] = new ResourceInstance(resource, initialValue, cap);
                    break;
            }
        }
    }

    // ─────────────────────── Resource Helpers ─────────────────────

    private ResourceInstance GetResourcePool(BattleState state, UnitState unit, string resourceId,
        ResourceOwnershipScope scope)
    {
        return scope switch
        {
            ResourceOwnershipScope.Unit =>
                unit.Resources.TryGetValue(resourceId, out var u) ? u : null,
            ResourceOwnershipScope.Team =>
                state.TeamResources.TryGetValue(unit.Team, out var td) &&
                td.TryGetValue(resourceId, out var t) ? t : null,
            ResourceOwnershipScope.Global =>
                state.GlobalResources.TryGetValue(resourceId, out var g) ? g : null,
            _ => null
        };
    }

    private ResourceInstance GetOrCreatePool(BattleState state, UnitState unit,
        ResourceDefinition resource, ResourceOwnershipScope scope)
    {
        var pool = GetResourcePool(state, unit, resource.Id, scope);
        if (pool != null) return pool;

        pool = new ResourceInstance(resource, resource.DefaultValue, resource.DefaultMaxValue);
        switch (scope)
        {
            case ResourceOwnershipScope.Unit:
                unit.Resources[resource.Id] = pool;
                break;
            case ResourceOwnershipScope.Team:
                if (!state.TeamResources.TryGetValue(unit.Team, out var teamDict))
                {
                    teamDict = new Dictionary<string, ResourceInstance>();
                    state.TeamResources[unit.Team] = teamDict;
                }
                teamDict[resource.Id] = pool;
                break;
            case ResourceOwnershipScope.Global:
                state.GlobalResources[resource.Id] = pool;
                break;
        }
        return pool;
    }

    private ResourceDefinition FindResourceDefinition(BattleState state, UnitState unit, string resourceId)
    {
        if (unit.Resources.TryGetValue(resourceId, out var u))
            return u.Definition;
        if (state.TeamResources.TryGetValue(unit.Team, out var td) &&
            td.TryGetValue(resourceId, out var t))
            return t.Definition;
        if (state.GlobalResources.TryGetValue(resourceId, out var g))
            return g.Definition;
        return null;
    }

    private void SpendFromFirstFound(BattleState state, UnitState unit, string resourceId, int amount)
    {
        ResourceInstance pool = null;
        ResourceOwnershipScope scope = ResourceOwnershipScope.Unit;

        if (unit.Resources.TryGetValue(resourceId, out pool))
            scope = ResourceOwnershipScope.Unit;
        else if (state.TeamResources.TryGetValue(unit.Team, out var td) &&
                 td.TryGetValue(resourceId, out pool))
            scope = ResourceOwnershipScope.Team;
        else if (state.GlobalResources.TryGetValue(resourceId, out pool))
            scope = ResourceOwnershipScope.Global;

        if (pool == null) return;

        int old = pool.CurrentValue;
        pool.CurrentValue = Mathf.Max(0, pool.CurrentValue - amount);
        state.EventBus?.Raise(new ResourceChangedEvent(state.TurnNumber, unit, pool.Definition, old, pool.CurrentValue, scope));

        if (DeathCondition != null)
            CheckAndApplyDeath(state, unit);
    }

    // ─────────────────────── Tag Resources ────────────────────────

    public int GetTagResourceAmount(BattleState state, UnitState unit, ResourceTag tag)
    {
        if (tag == null) return 0;

        if (ActiveSnapshot != null)
            return ActiveSnapshot.GetTagResource(state, unit, tag);

        int total = 0;
        foreach (var res in unit.Resources.Values)
            if (res.Definition != null && res.Definition.HasTag(tag))
                total += res.CurrentValue;

        if (state.TeamResources.TryGetValue(unit.Team, out var teamDict))
            foreach (var res in teamDict.Values)
                if (res.Definition != null && res.Definition.HasTag(tag))
                    total += res.CurrentValue;

        foreach (var res in state.GlobalResources.Values)
            if (res.Definition != null && res.Definition.HasTag(tag))
                total += res.CurrentValue;

        return total;
    }

    public void SpendTagResource(BattleState state, UnitState unit, ResourceTag tag, int amount)
    {
        if (tag == null) return;

        int remaining = amount;

        // Drain Unit scope first, then Team, then Global
        remaining = DrainTaggedPools(state, unit, tag, remaining, unit.Resources.Values, ResourceOwnershipScope.Unit);

        if (remaining > 0 && state.TeamResources.TryGetValue(unit.Team, out var teamDict))
            remaining = DrainTaggedPools(state, unit, tag, remaining, teamDict.Values, ResourceOwnershipScope.Team);

        if (remaining > 0)
            DrainTaggedPools(state, unit, tag, remaining, state.GlobalResources.Values, ResourceOwnershipScope.Global);

        if (DeathCondition != null)
            CheckAndApplyDeath(state, unit);
    }

    private int DrainTaggedPools(BattleState state, UnitState unit, ResourceTag tag,
        int remaining, IEnumerable<ResourceInstance> pools, ResourceOwnershipScope scope)
    {
        foreach (var pool in pools)
        {
            if (remaining <= 0) break;
            if (pool.Definition == null || !pool.Definition.HasTag(tag)) continue;
            if (pool.CurrentValue <= 0) continue;

            int drain = pool.CurrentValue < remaining ? pool.CurrentValue : remaining;
            int old = pool.CurrentValue;
            pool.CurrentValue -= drain;
            remaining -= drain;
            state.EventBus?.Raise(new ResourceChangedEvent(state.TurnNumber, unit, pool.Definition, old, pool.CurrentValue, scope));
        }
        return remaining;
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
using System;
using UnityEngine;

[Serializable]
public sealed class DamageEffectConfig : EffectConfig
{
    [SerializeField] private int power = 0;

    public override string DisplayName => "Damage";

    public override void Apply(BattleState state, ActionExecution execution, CombatRules rules)
    {
        foreach (var target in ResolveTargets(state, execution))
        {
            int damage = rules.CalculateDamage(execution.Actor, target, power + execution.PowerModifier);
            target.Hp -= damage;

            bool isLethal = target.Hp <= 0;
            state.EventBus.Raise(new DamageDealtEvent(state.TurnNumber, execution.Actor, target, damage, isLethal));

            if (isLethal)
            {
                target.Hp = 0;
                target.IsAlive = false;
                state.EventBus.Raise(new UnitDefeatedEvent(state.TurnNumber, target, execution.Actor));
            }
        }
    }
}
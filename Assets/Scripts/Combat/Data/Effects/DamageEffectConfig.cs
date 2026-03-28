using System;
using UnityEngine;

[Serializable]
public sealed class DamageEffectConfig : EffectConfig
{
    [SerializeField] private int power = 0;

    public override string DisplayName => "Damage";

    public override void Apply(BattleState state, ActionExecution execution, CombatRules rules)
    {
        foreach (var target in execution.Targets)
        {
            int damage = rules.CalculateDamage(execution.Actor, target, power + execution.PowerModifier);
            target.Hp -= damage;

            state.Log.Add($"{execution.Actor.Definition.DisplayName} hits {target.Definition.DisplayName} for {damage}");

            if (target.Hp <= 0)
            {
                target.Hp = 0;
                target.IsAlive = false;
                state.Log.Add($"{target.Definition.DisplayName} is defeated");
            }
        }
    }
}
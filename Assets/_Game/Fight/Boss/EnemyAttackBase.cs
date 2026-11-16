using UnityEngine;

[RequireComponent(typeof(DamageDealer))]
public abstract class EnemyAttackBase : MonoBehaviour
{
    protected DamageDealer damageDealer;
    protected BossStateMachine ownerBoss;

    protected virtual void Awake()
    {
        damageDealer = GetComponent<DamageDealer>();
    }

    // 只有純數值交割，沒有 direction，沒有 speed
    public virtual void Initialize(BossStateMachine boss, float damageMultiplier = 1f, bool ignoreInvincibility = false)
    {
        ownerBoss = boss;
        if (boss != null)
        {
            var core = boss.GetComponent<EntityCore>();
            if (core != null && core.RuntimeData != null)
            {
                // ★ 修改處：移除 Total 前綴，並透過 .Value 讀取 EntityAttribute 的最終計算值
                float finalAttack = core.RuntimeData.AttackPower.Value * damageMultiplier;
                damageDealer.InjectSnapshot(
                    finalAttack, 
                    core.RuntimeData.CritRate.Value, 
                    core.RuntimeData.CritMultiplier.Value, 
                    ignoreInvincibility
                );
            }
        }
    }
}
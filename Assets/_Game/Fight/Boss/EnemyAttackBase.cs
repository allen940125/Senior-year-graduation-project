using UnityEngine;

[RequireComponent(typeof(DamageDealer))]
public abstract class EnemyAttackBase : MonoBehaviour
{
    [Header("★ 碰撞縮放設定")]
    [Tooltip("勾選後，碰撞框會乘上物件自身的 Transform.lossyScale；取消則嚴格使用 SO 原始尺寸")]
    [SerializeField] protected bool useTransformScale = false;

    protected abstract AttackDataSO BaseAttackData { get; }
    
    protected DamageDealer damageDealer;
    protected BossStateMachine ownerBoss;

    protected virtual void Awake()
    {
        damageDealer = GetComponent<DamageDealer>();
    }

    public virtual void Initialize(BossStateMachine boss, float damageMultiplier = 1f, bool ignoreInvincibility = false)
    {
        ownerBoss = boss;
        if (boss != null)
        {
            var core = boss.GetComponent<EntityCore>();
            if (core != null && core.RuntimeData != null)
            {
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

    /// <summary>
    /// 統一取得最終生效的碰撞形狀（自動依據開關決定是否套用縮放）
    /// </summary>
    protected CollisionShapeConfig GetEffectiveShape(CollisionShapeConfig rawShape)
    {
        return useTransformScale ? rawShape.GetScaled(transform.lossyScale) : rawShape;
    }

    /// <summary>
    /// 供外部發射器或預覽系統讀取真實傷害碰撞形狀
    /// </summary>
    public virtual bool TryGetDamageShape(out CollisionShapeConfig shape)
    {
        if (BaseAttackData == null)
        {
            shape = default;
            return false;
        }

        shape = GetEffectiveShape(BaseAttackData.damageShape);
        return true;
    }
}
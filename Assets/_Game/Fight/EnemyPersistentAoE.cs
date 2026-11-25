using System.Collections.Generic;
using UnityEngine;

public class EnemyPersistentAoE : EnemyAttackBase
{
    [Header("★ 攻擊資料配置")]
    [SerializeField] private AttackDataSO attackData;
    protected override AttackDataSO BaseAttackData => attackData;
    
    [Header("★ 持續時間設定")]
    [Tooltip("大於 0：時間到自動銷毀；小於等於 0：常駐直到 Boss 階段結束清空")]
    [SerializeField] private float lifeTime = 5f;

    [Tooltip("生成後延遲幾秒才開始造成傷害（給玩家一點反應時間，設 0 則立刻生效）")]
    [SerializeField] private float activeDelay = 0f;

    private readonly Collider[] _hitBuffer = new Collider[16];
    private readonly Dictionary<GameObject, float> _lastDamageTimes = new Dictionary<GameObject, float>();
    private float _elapsedTime = 0f;
    private bool _isInitialized = false;

    public override void Initialize(BossStateMachine boss, float damageMultiplier = 1f, bool ignoreInvincibility = false)
    {
        float finalMult = (attackData != null) ? attackData.damageMultiplier * damageMultiplier : damageMultiplier;
        bool finalIgnore = (attackData != null && attackData.ignoreInvincibility) || ignoreInvincibility;
        base.Initialize(boss, finalMult, finalIgnore);

        if (ownerBoss != null) ownerBoss.RegisterActiveBullet(this.gameObject);

        if (lifeTime > 0f)
        {
            Destroy(gameObject, lifeTime);
        }

        _isInitialized = true;
    }

    private void OnDestroy()
    {
        if (ownerBoss != null) ownerBoss.UnregisterActiveBullet(this.gameObject);
    }

    private void FixedUpdate()
    {
        if (!_isInitialized || attackData == null) return;

        _elapsedTime += Time.fixedDeltaTime;
        if (_elapsedTime < activeDelay) return;

        int hitCount = ShapeOverlapUtility.PerformOverlap(
            transform.position,
            transform.rotation,
            _hitBuffer,
            GetEffectiveShape(attackData.damageShape),
            attackData.damageLayer
        );

        for (int i = 0; i < hitCount; i++)
        {
            GameObject target = _hitBuffer[i].gameObject;

            if (attackData.useDamageTick)
            {
                if (_lastDamageTimes.TryGetValue(target, out float lastTime) &&
                    Time.time < lastTime + attackData.damageTickInterval)
                {
                    continue;
                }
                _lastDamageTimes[target] = Time.time;
            }

            damageDealer.DealDamageTo(target);
        }
    }

// 2. 畫 Gizmo 時（確保編輯器預覽與實戰 100% 一致）：
    private void OnDrawGizmos()
    {
        if (attackData != null)
        {
            GetEffectiveShape(attackData.damageShape)
                .DrawGizmo(transform.position, transform.rotation, new Color(1f, 0f, 0f, 0.4f));
        }
    }
}
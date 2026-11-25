using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Gamemanager;

public class EnemyDelayedAoE : EnemyAttackBase
{
    [Header("★ 攻擊資料配置")]
    [SerializeField] private DelayedAoEDataSO attackData;
    protected override AttackDataSO BaseAttackData => attackData;

    [Header("★ 觸發模式（預設關閉 = 由 Animator Event 控制）")]
    [Tooltip("勾選後不依賴 Animator，自動依照下方讀秒與連段數執行，結束後自動銷毀")]
    [SerializeField] private bool useCodeTimer = false;
    [Tooltip("第一下爆發前的讀條等待時間（秒）")]
    [SerializeField] private float initialDelay = 1.0f;
    [Min(1)] [SerializeField] private int burstCount = 1;
    [Tooltip("連段之間的休息間隔（秒）")]
    [SerializeField] private float burstInterval = 0.5f;

    [Header("★ 嚴格傷害追蹤 (Debug)")]
    [SerializeField] private bool enableDamageDebug = false;

    private readonly Collider[] _hitColliders = new Collider[16];
    // 防止單次爆發中，同一個角色身上有多個 Collider 而被重複扣兩次血
    private readonly HashSet<GameObject> _singleBurstHitTargets = new HashSet<GameObject>();

    public override void Initialize(BossStateMachine boss, float damageMultiplier = 1f, bool ignoreInvincibility = false)
    {
        float finalMultiplier = (attackData != null) ? attackData.damageMultiplier * damageMultiplier : damageMultiplier;
        bool finalIgnore = (attackData != null && attackData.ignoreInvincibility) || ignoreInvincibility;
        base.Initialize(boss, finalMultiplier, finalIgnore);

        if (ownerBoss != null) ownerBoss.RegisterActiveBullet(this.gameObject);

        if (useCodeTimer)
        {
            StartCoroutine(RunCodeBurstRoutine());
        }
    }

    private void OnDestroy()
    {
        if (ownerBoss != null) ownerBoss.UnregisterActiveBullet(this.gameObject);
    }

    private IEnumerator RunCodeBurstRoutine()
    {
        if (initialDelay > 0f)
        {
            yield return new WaitForSeconds(initialDelay);
        }

        int count = Mathf.Max(1, burstCount);
        for (int i = 0; i < count; i++)
        {
            Explode();

            // 最後一下打完不用再等休息時間，直接退場
            if (i < count - 1 && burstInterval > 0f)
            {
                yield return new WaitForSeconds(burstInterval);
            }
        }

        DestroySelf();
    }

    /// <summary>
    /// 執行單次爆發判定（Animator Event 模式可直接在時間軸上放置 3 次 Explode 事件）
    /// </summary>
    public void Explode()
    {
        if (attackData == null) return;

        _singleBurstHitTargets.Clear();

        if (attackData.hitEffectPrefab != null)
        {
            Instantiate(attackData.hitEffectPrefab, transform.position, Quaternion.identity);
        }

        int hitCount = ShapeOverlapUtility.PerformOverlap(
            transform.position,
            transform.rotation,
            _hitColliders,
            GetEffectiveShape(attackData.damageShape),
            attackData.damageLayer,
            enableDamageDebug
        );

        for (int i = 0; i < hitCount; i++)
        {
            GameObject target = _hitColliders[i].gameObject;
            if (_singleBurstHitTargets.Add(target))
            {
                damageDealer.DealDamageTo(target);
            }
        }

        if (attackData.enableCameraShake && GameManager.Instance?.MainGameEvent != null)
        {
            GameManager.Instance.MainGameEvent.Send(new CameraShakeEvent
            {
                Intensity = attackData.shakeIntensity,
                Duration = attackData.shakeDuration
            });
        }
    }

    public void DestroySelf()
    {
        Destroy(gameObject);
    }

    private void OnDrawGizmos()
    {
        if (attackData != null)
        {
            GetEffectiveShape(attackData.damageShape)
                .DrawGizmo(transform.position, transform.rotation, new Color(1f, 0f, 0f, 0.4f));
        }
    }
}
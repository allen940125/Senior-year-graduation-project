using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>發射器基礎流程：計算生成資料、顯示預警、依序生成子彈。</summary>
public abstract class BulletSpawnerBase : AttackPatternBase
{
    [Header("生成參數")]
    public GameObject bulletPrefab;
    public float baseSpeed = 5f;
    [Tooltip("每顆子彈發射的間隔時間；設為 0 則瞬間齊射")]
    public float spawnInterval = 0.05f;

    [Tooltip("額外傷害倍率；會與子彈 SO 的倍率相乘")]
    public float patternDamageMultiplier = 1.0f;

    protected override void OnExecute(BossStateMachine boss, float speedMultiplier, bool isAngry)
    {
        float finalSpeed = baseSpeed * speedMultiplier;
        if (isAngry) finalSpeed *= 1.5f;

        transform.SetParent(null);
        StartCoroutine(ExecuteSpawnerSequence(boss, finalSpeed));
    }

    /// <summary>只計算位置與方向，不在此方法中生成物件。</summary>
    protected abstract List<SpawnData> CalculateAllSpawnData();

    private IEnumerator ExecuteSpawnerSequence(BossStateMachine boss, float speed)
    {
        List<SpawnData> dataList = CalculateAllSpawnData();

        if (bulletPrefab != null)
        {
            if (bulletPrefab.TryGetComponent<EnemyBullet>(out var bullet) && bullet.bulletData != null)
            {
                for (int i = 0; i < dataList.Count; i++)
                {
                    var data = dataList[i];
                    data.predictedTrajectory = bullet.bulletData.CalculatePredictedTrajectory(data.position, data.direction);
                    dataList[i] = data;
                }
            }
            else if (bulletPrefab.TryGetComponent<EnemyHomingMissile>(out var missile) && missile.missileData != null)
            {
                for (int i = 0; i < dataList.Count; i++)
                {
                    var data = dataList[i];
                    data.predictedTrajectory = missile.missileData.CalculatePredictedTrajectory(data.position, data.direction);
                    dataList[i] = data;
                }
            }
        }

        yield return StartCoroutine(ShowWarningsAndWait(dataList));

        if (spawnInterval > 0f)
        {
            for (int i = 0; i < dataList.Count; i++)
            {
                var data = dataList[i];
                CreateBullet(boss, data.position, data.direction, speed);

                if (i < dataList.Count - 1)
                {
                    yield return new WaitForSeconds(spawnInterval);
                }
            }
        }
        else
        {
            foreach (var data in dataList)
            {
                CreateBullet(boss, data.position, data.direction, speed);
            }
        }

        FinishPattern();
    }

    private void CreateBullet(BossStateMachine boss, Vector2 spawnPos, Vector2 direction, float speed)
    {
        if (bulletPrefab == null)
        {
            Debug.LogError($"[{name}] 未設定 Bullet Prefab！", this);
            return;
        }

        GameObject attackObj = Instantiate(bulletPrefab, spawnPos, Quaternion.identity);

        if (attackObj.TryGetComponent<EnemyAttackBase>(out var attackBase))
        {
            attackBase.Initialize(boss, patternDamageMultiplier);
        }
        else
        {
            Debug.LogWarning($"[{name}] 生成物件 {attackObj.name} 缺少 EnemyAttackBase！", this);
        }

        if (attackObj.TryGetComponent<IProjectile>(out var projectile))
        {
            projectile.SetTrajectory(new Vector3(direction.x, direction.y, 0f), speed);
        }
    }
    
    [ContextMenu("🔍 診斷碰撞框預覽 (Debug Gizmo Shape)")]
    public void DebugGizmoShape()
    {
        Debug.Log($"========== [{name}] 碰撞框預覽診斷 ==========");

        // 1. 改用統一介面檢查生成點，適用所有 BulletSpawnerBase 子類
        var dataList = CalculateAllSpawnData();
        if (dataList == null || dataList.Count == 0)
        {
            Debug.LogError("[失敗] CalculateAllSpawnData() 回傳 0 個點位。請檢查點位設定或數量參數。");
            return;
        }
        Debug.Log($"[通過] 有效生成點數量：{dataList.Count}");

        // 2. 檢查 Prefab
        if (bulletPrefab == null)
        {
            Debug.LogError("[失敗] bulletPrefab 為空（null），請在 Inspector 拖入攻擊 Prefab。");
            return;
        }
        Debug.Log($"[通過] bulletPrefab：{bulletPrefab.name} | Prefab Scale：{bulletPrefab.transform.localScale}");

        // 3. 檢查根物件上的攻擊腳本
        bool hasShape = false;
        CollisionShapeConfig shape = default;

        if (bulletPrefab.TryGetComponent<EnemyAttackBase>(out var enemyAttack))
        {
            hasShape = enemyAttack.TryGetDamageShape(out shape);
            Debug.Log($"[偵測] 根物件找到 EnemyAttackBase（{enemyAttack.GetType().Name}），TryGetDamageShape() 回傳：{hasShape}");
        }
        else if (bulletPrefab.TryGetComponent<DamageCasterBase>(out var damageCaster))
        {
            hasShape = damageCaster.TryGetDamageShape(out shape);
            Debug.Log($"[偵測] 根物件找到 DamageCasterBase（{damageCaster.GetType().Name}），TryGetDamageShape() 回傳：{hasShape}");
        }
        else
        {
            var childEnemy = bulletPrefab.GetComponentInChildren<EnemyAttackBase>(true);
            var childCaster = bulletPrefab.GetComponentInChildren<DamageCasterBase>(true);

            if (childEnemy != null || childCaster != null)
            {
                string foundName = childEnemy != null ? childEnemy.gameObject.name : childCaster.gameObject.name;
                Debug.LogError($"[失敗] 攻擊腳本掛在子物件 '{foundName}' 上，而非 Prefab 根物件。");
            }
            else
            {
                Debug.LogError("[失敗] Prefab 及其子物件上完全找不到 EnemyAttackBase 或 DamageCasterBase。");
            }
            return;
        }

        // 4. 檢查回傳結果與實際尺寸
        if (!hasShape)
        {
            Debug.LogError("[失敗] TryGetDamageShape() 回傳 false。請檢查該子類是否已實作，以及 Prefab 的 SO 欄位是否為空。");
            return;
        }

        Debug.Log($"[通過] 形狀：{shape.shapeType} | Radius={shape.radius} | BoxSize={shape.boxSize} | CapsuleLen={shape.capsuleLength} | ZThickness={shape.zThickness} | RotOffset={shape.shapeRotationOffset}");

        // 5. 防呆：檢查尺寸是否為 0
        bool isZeroSize =
            (shape.shapeType == BulletShapeType.Circle && shape.radius <= 0f) ||
            (shape.shapeType == BulletShapeType.Box && (shape.boxSize.x <= 0f || shape.boxSize.y <= 0f)) ||
            (shape.shapeType == BulletShapeType.Capsule && shape.radius <= 0f);

        if (isZeroSize)
        {
            Debug.LogWarning("[異常] 形狀資料已讀取，但尺寸小於等於 0，畫出來會看不見。");
        }
    }
}
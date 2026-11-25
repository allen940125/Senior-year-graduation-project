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
}
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LiveAimingFanPattern : AttackPatternBase
{
    [Header("子彈與速度")]
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private float baseSpeed = 12f;
    [SerializeField] private float patternDamageMultiplier = 1f;

    [Header("連發設定")]
    [Min(1)] public int burstCount = 1;
    [Tooltip("每一發之間的間隔秒數")]
    public float burstInterval = 0.25f;

    [Header("扇形設定")]
    [Min(1)] public int bulletCount = 3;
    [Tooltip("相鄰子彈的夾角")]
    public float angleBetweenBullets = 15f;

    [Header("實時瞄準")]
    [Tooltip("持續追蹤玩家的秒數")]
    public float trackDuration = 1.2f;
    [Tooltip("鎖定方向後等待開火的秒數")]
    public float lockDuration = 0.3f;

    [Header("瞄準射線")]
    [SerializeField] private TrajectoryVisualizer visualizerPrefab;
    [Tooltip("只顯示正中央瞄準射線")]
    public bool onlyShowCenterAimLine = false;
    [SerializeField] private float aimLineLength = 25f;
    [SerializeField] private Color trackingColor = new Color(1f, 0.2f, 0.2f, 0.5f);
    [SerializeField] private Color lockedColor = new Color(1f, 0.9f, 0.1f, 0.9f);

    private readonly List<TrajectoryVisualizer> _spawnedVisualizers = new List<TrajectoryVisualizer>();

    protected override void OnExecute(BossStateMachine boss, float speedMultiplier, bool isAngry)
    {
        transform.SetParent(null);

        float finalSpeed = baseSpeed * speedMultiplier;
        if (isAngry) finalSpeed *= 1.5f;

        StartCoroutine(RunAimAndFireRoutine(boss, finalSpeed));
    }

    protected override void OnPatternCleanup()
    {
        ClearAimLines();
    }

    private IEnumerator RunAimAndFireRoutine(BossStateMachine boss, float speed)
    {
        if (bulletPrefab == null)
        {
            Debug.LogError($"[{name}] 未指派 bulletPrefab！");
            FinishPattern();
            yield break;
        }

        burstCount = Mathf.Max(1, burstCount);
        bulletCount = Mathf.Max(1, bulletCount);

        Transform playerTransform = FindPlayerTransform();
        CreateAimLines();

        Vector2 currentAimDir = GetDirectionToTarget(playerTransform, Vector2.down);

        float timer = 0f;
        SetLinesColor(trackingColor);

        while (timer < Mathf.Max(0f, trackDuration))
        {
            currentAimDir = GetDirectionToTarget(playerTransform, currentAimDir);
            UpdateAimLines(currentAimDir);

            timer += Time.deltaTime;
            yield return null;
        }

        if (lockDuration > 0f)
        {
            SetLinesColor(lockedColor);
            UpdateAimLines(currentAimDir);
            yield return new WaitForSeconds(lockDuration);
        }

        ClearAimLines();

        float baseAngle = Mathf.Atan2(currentAimDir.y, currentAimDir.x) * Mathf.Rad2Deg;

        for (int b = 0; b < burstCount; b++)
        {
            for (int i = 0; i < bulletCount; i++)
            {
                float angleOffset = (i - (bulletCount - 1) * 0.5f) * angleBetweenBullets;
                Vector2 fireDir = AngleToVector(baseAngle + angleOffset);
                SpawnSingleBullet(boss, transform.position, fireDir, speed);
            }

            if (b < burstCount - 1 && burstInterval > 0f)
            {
                yield return new WaitForSeconds(burstInterval);
            }
        }

        FinishPattern();
    }

    private void CreateAimLines()
    {
        ClearAimLines();
        if (visualizerPrefab == null) return;

        int lineCount = onlyShowCenterAimLine ? 1 : bulletCount;
        for (int i = 0; i < lineCount; i++)
        {
            TrajectoryVisualizer vis = Instantiate(visualizerPrefab, transform);
            _spawnedVisualizers.Add(vis);
        }
    }

    private void UpdateAimLines(Vector2 centerDir)
    {
        if (_spawnedVisualizers.Count == 0) return;

        float baseAngle = Mathf.Atan2(centerDir.y, centerDir.x) * Mathf.Rad2Deg;
        Vector3 startPos = transform.position;
        bulletPrefab.TryGetComponent<EnemyBullet>(out var bulletComp);

        for (int i = 0; i < _spawnedVisualizers.Count; i++)
        {
            if (_spawnedVisualizers[i] == null) continue;

            float angleOffset = onlyShowCenterAimLine
                ? 0f
                : (i - (bulletCount - 1) * 0.5f) * angleBetweenBullets;

            Vector3 dir = AngleToVector(baseAngle + angleOffset);

            Vector3[] points = bulletComp != null && bulletComp.bulletData != null
                ? bulletComp.bulletData.CalculatePredictedTrajectory(startPos, dir)
                : null;

            if (points == null || points.Length < 2)
            {
                points = new[] { startPos, startPos + dir * aimLineLength };
            }

            _spawnedVisualizers[i].DrawTrajectory(points);
        }
    }

    private void SetLinesColor(Color color)
    {
        foreach (var visualizer in _spawnedVisualizers)
        {
            if (visualizer != null)
            {
                visualizer.SetColor(color);
            }
        }
    }

    private void ClearAimLines()
    {
        foreach (var visualizer in _spawnedVisualizers)
        {
            if (visualizer != null)
            {
                Destroy(visualizer.gameObject);
            }
        }
        _spawnedVisualizers.Clear();
    }

    private void SpawnSingleBullet(BossStateMachine boss, Vector2 spawnPos, Vector2 direction, float speed)
    {
        GameObject obj = Instantiate(bulletPrefab, spawnPos, Quaternion.identity);

        if (obj.TryGetComponent<EnemyAttackBase>(out var attackBase))
        {
            attackBase.Initialize(boss, patternDamageMultiplier);
        }

        if (obj.TryGetComponent<IProjectile>(out var projectile))
        {
            projectile.SetTrajectory(new Vector3(direction.x, direction.y, 0f), speed);
        }
    }

    private Transform FindPlayerTransform()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        return player != null ? player.transform : null;
    }

    private Vector2 GetDirectionToTarget(Transform target, Vector2 fallbackDir)
    {
        if (target == null) return fallbackDir;
        Vector2 diff = (Vector2)target.position - (Vector2)transform.position;
        return diff.sqrMagnitude > 0.0001f ? diff.normalized : fallbackDir;
    }

    private Vector2 AngleToVector(float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
    }

    protected override void OnGeneratePreview(Transform previewContainer)
    {
        bulletCount = Mathf.Max(1, bulletCount);
        float baseAngle = -90f;

        for (int i = 0; i < bulletCount; i++)
        {
            float angleOffset = (i - (bulletCount - 1) * 0.5f) * angleBetweenBullets;
            CreatePreviewDummy(previewContainer, transform.position, AngleToVector(baseAngle + angleOffset), bulletPrefab);
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        int count = Mathf.Max(1, bulletCount);
        Vector2 centerDir = GetDirectionToTarget(FindPlayerTransform(), Vector2.down);
        float baseAngle = Mathf.Atan2(centerDir.y, centerDir.x) * Mathf.Rad2Deg;

        for (int i = 0; i < count; i++)
        {
            float angleOffset = (i - (count - 1) * 0.5f) * angleBetweenBullets;
            Vector3 dir = AngleToVector(baseAngle + angleOffset);

            Gizmos.color = (i == count / 2) ? lockedColor : trackingColor;
            Gizmos.DrawLine(transform.position, transform.position + dir * aimLineLength);
        }
    }
}

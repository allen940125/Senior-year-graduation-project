using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LiveAimingFanPattern : AttackPatternBase
{
    [Header("★ 子彈與速度設定")]
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private float baseSpeed = 12f;
    [SerializeField] private float patternDamageMultiplier = 1f;

    [Header("★ 連發設定（設 1 為單次齊射，>1 為依序連發）")]
    [Min(1)] public int burstCount = 1;
    [Tooltip("每一發之間的間隔秒數")]
    public float burstInterval = 0.25f;
    
    [Header("★ 逆命 Q 扇形設定（設 1 就是單發狙擊）")]
    [Min(1)] public int bulletCount = 3;
    [Tooltip("相鄰兩發子彈的固定夾角（例如 3 發配 15 度 = -15, 0, +15）")]
    public float angleBetweenBullets = 15f;

    [Header("★ 實時瞄準與鎖定時間")]
    [Tooltip("持續追蹤玩家的秒數")]
    public float trackDuration = 1.2f;
    [Tooltip("鎖定方向不再追蹤、準備開火的緩衝秒數（設 0 則追蹤完立刻發射）")]
    public float lockDuration = 0.3f;

    [Header("★ 瞄準射線視覺設定")]
    [Tooltip("預警射線 Prefab（需掛載 TrajectoryVisualizer）")]
    [SerializeField] private TrajectoryVisualizer visualizerPrefab;
    [Tooltip("勾選後只顯示正中央瞄準玩家的單條射線，不顯示兩側偏移射線")]
    public bool onlyShowCenterAimLine = false;
    [SerializeField] private float aimLineLength = 25f;
    [SerializeField] private Color trackingColor = new Color(1f, 0.2f, 0.2f, 0.5f);
    [SerializeField] private Color lockedColor = new Color(1f, 0.9f, 0.1f, 0.9f);

    private readonly List<TrajectoryVisualizer> _spawnedVisualizers = new List<TrajectoryVisualizer>();

    protected override void OnExecute(BossStateMachine boss, float speedMultiplier, bool isAngry)
    {
        transform.SetParent(null);
        if (boss != null) boss.RegisterActivePattern(this.gameObject);

        float finalSpeed = baseSpeed * speedMultiplier;
        if (isAngry) finalSpeed *= 1.5f;

        StartCoroutine(RunAimAndFireRoutine(boss, finalSpeed));
    }

    private IEnumerator RunAimAndFireRoutine(BossStateMachine boss, float speed)
    {
        if (bulletPrefab == null)
        {
            Debug.LogError($"[{name}] 未指派 bulletPrefab！");
            FinishPattern();
            yield break;
        }

        Transform playerTransform = FindPlayerTransform();
        CreateAimLines();

        Vector2 currentAimDir = GetDirectionToTarget(playerTransform, Vector2.down);

        // 階段 1：實時追蹤玩家
        float timer = 0f;
        SetLinesColor(trackingColor);

        while (timer < trackDuration)
        {
            currentAimDir = GetDirectionToTarget(playerTransform, currentAimDir);
            UpdateAimLines(currentAimDir);

            timer += Time.deltaTime;
            yield return null;
        }

        // 階段 2：鎖定方向
        if (lockDuration > 0f)
        {
            SetLinesColor(lockedColor);
            UpdateAimLines(currentAimDir);
            yield return new WaitForSeconds(lockDuration);
        }

        // 階段 3：關閉射線並開火（支援單次齊射或依序連發）
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

            // 最後一波打完不用再等，直接結束
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
        Vector3 startPos = new Vector3(transform.position.x, transform.position.y, 0f);
        bulletPrefab.TryGetComponent<EnemyBullet>(out var bulletComp);

        for (int i = 0; i < _spawnedVisualizers.Count; i++)
        {
            if (_spawnedVisualizers[i] == null) continue;

            // 勾選只顯示中心線時，強制偏移 0 度（直接瞄準中心方向）
            float angleOffset = onlyShowCenterAimLine
                ? 0f
                : (i - (bulletCount - 1) * 0.5f) * angleBetweenBullets;

            Vector3 dir = AngleToVector(baseAngle + angleOffset);

            Vector3[] points = (bulletComp != null && bulletComp.bulletData != null)
                ? bulletComp.bulletData.CalculatePredictedTrajectory(startPos, dir)
                : null;

            if (points == null || points.Length < 2)
            {
                points = new Vector3[] { startPos, startPos + dir * aimLineLength };
            }

            _spawnedVisualizers[i].DrawTrajectory(points);
        }
    }

    private void SetLinesColor(Color color)
    {
        foreach (var vis in _spawnedVisualizers)
        {
            if (vis != null) vis.SetColor(color);
        }
    }

    private void ClearAimLines()
    {
        foreach (var vis in _spawnedVisualizers)
        {
            if (vis != null) Destroy(vis.gameObject);
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
        float baseAngle = -90f;
        for (int i = 0; i < bulletCount; i++)
        {
            float angleOffset = (i - (bulletCount - 1) * 0.5f) * angleBetweenBullets;
            // ★ 補上傳入 bulletPrefab
            CreatePreviewDummy(previewContainer, transform.position, AngleToVector(baseAngle + angleOffset), bulletPrefab);
        }
    }
}
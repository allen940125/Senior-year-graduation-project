using System.Collections;
using UnityEngine;

public class FlappyPipeWavePattern : AttackPatternBase
{
    public enum CurveMode
    {
        SineWave,
        ClampedRandomWalk,
        CustomCurve
    }

    [Header("水管 Prefab 與基礎數值")] [SerializeField]
    private GameObject pipePrefab;

    [SerializeField] private float baseSpeed = 6f;
    [SerializeField] private float patternDamageMultiplier = 1f;

    [Tooltip("單根水管高度，需與 Prefab 一致")] [SerializeField]
    private float singlePipeHeight = 15f;

    [Header("波次與密度")] [Min(1)] public int pipeCount = 10;
    [Min(0.5f)] public float pipeSpacing = 3.5f;
    public float gapHeight = 3.0f;

    [Header("生成基準與方向")] public Vector2 spawnOffset = new Vector2(10f, 0f);
    public Vector2 moveDirection = Vector2.left;

    [Header("曲線與落差")] [Header("曲線與落差")] public CurveMode curveMode = CurveMode.SineWave; // ★ 補回這行
    [Tooltip("缺口中心點上下擺動的最大範圍")] public float maxAmplitudeY = 3.5f;

    [Header("SineWave")] public float waveCycles = 1.5f;
    public bool randomStartPhase = true;

    [Header("ClampedRandomWalk")] [Tooltip("相鄰兩組缺口的最大落差")]
    public float maxStepDeltaY = 1.2f;

    [Header("CustomCurve")] [Tooltip("橫軸 0 到 1，縱軸乘上 maxAmplitudeY")]
    public AnimationCurve customCurve = AnimationCurve.EaseInOut(0f, -1f, 1f, 1f);

    protected override void OnExecute(BossStateMachine boss, float speedMultiplier, bool isAngry)
    {
        transform.SetParent(null);

        float finalSpeed = baseSpeed * speedMultiplier;
        if (isAngry) finalSpeed *= 1.5f;

        StartCoroutine(RunPipeWaveRoutine(boss, finalSpeed));
    }

    private IEnumerator RunPipeWaveRoutine(BossStateMachine boss, float speed)
    {
        if (pipePrefab == null)
        {
            Debug.LogError($"[{name}] 未指派 pipePrefab！");
            FinishPattern();
            yield break;
        }

        pipeCount = Mathf.Max(1, pipeCount);
        pipeSpacing = Mathf.Max(0.5f, pipeSpacing);
        gapHeight = Mathf.Max(0f, gapHeight);
        singlePipeHeight = Mathf.Max(0f, singlePipeHeight);

        Vector2 baseCenter = (Vector2)transform.position + spawnOffset;
        Vector2 dir = moveDirection.sqrMagnitude > 0.001f ? moveDirection.normalized : Vector2.left;

        float[] gapOffsetsY = GenerateCurveOffsets();
        float interval = speed > 0.01f ? pipeSpacing / speed : 0.5f;
        float offsetFromGapCenter = gapHeight * 0.5f + singlePipeHeight * 0.5f;

        for (int i = 0; i < pipeCount; i++)
        {
            float gapCenterY = baseCenter.y + gapOffsetsY[i];

            SpawnSinglePipe(boss, new Vector2(baseCenter.x, gapCenterY + offsetFromGapCenter), dir, speed);
            SpawnSinglePipe(boss, new Vector2(baseCenter.x, gapCenterY - offsetFromGapCenter), dir, speed);

            if (i < pipeCount - 1)
            {
                yield return new WaitForSeconds(interval);
            }

        }

        FinishPattern();
    }

    private float[] GenerateCurveOffsets()
    {
        float[] offsets = new float[pipeCount];
        float phase = randomStartPhase ? Random.Range(0f, Mathf.PI * 2f) : 0f;
        float currentY = Random.Range(-maxAmplitudeY * 0.5f, maxAmplitudeY * 0.5f);

        for (int i = 0; i < pipeCount; i++)
        {
            float t = pipeCount > 1 ? (float)i / (pipeCount - 1) : 0f;

            switch (curveMode)
            {
                case CurveMode.SineWave:
                    offsets[i] = Mathf.Sin(t * waveCycles * Mathf.PI * 2f + phase) * maxAmplitudeY;
                    break;

                case CurveMode.ClampedRandomWalk:
                    if (i > 0)
                    {
                        float delta = Random.Range(-Mathf.Max(0f, maxStepDeltaY), Mathf.Max(0f, maxStepDeltaY));
                        currentY = Mathf.Clamp(currentY + delta, -maxAmplitudeY, maxAmplitudeY);
                    }

                    offsets[i] = currentY;
                    break;

                case CurveMode.CustomCurve:
                    offsets[i] = customCurve.Evaluate(t) * maxAmplitudeY;
                    break;
            }
        }

        return offsets;
    }

    private void SpawnSinglePipe(BossStateMachine boss, Vector2 spawnPos, Vector2 direction, float speed)
    {
        GameObject pipeObj = Instantiate(pipePrefab, spawnPos, Quaternion.identity);

        if (pipeObj.TryGetComponent<EnemyAttackBase>(out var attackBase))
        {
            attackBase.Initialize(boss, patternDamageMultiplier);
        }

        if (pipeObj.TryGetComponent<IProjectile>(out var projectile))
        {
            projectile.SetTrajectory(new Vector3(direction.x, direction.y, 0f), speed);
        }
    }

    protected override void OnGeneratePreview(Transform previewContainer)
    {
        if (pipePrefab == null) return;

        int count = Mathf.Max(1, pipeCount);
        Vector3 baseCenter = transform.position + (Vector3)spawnOffset;
        Vector3 dir = moveDirection.sqrMagnitude > 0.001f ? (Vector3)moveDirection.normalized : Vector3.left;
        float offsetFromGapCenter = gapHeight * 0.5f + singlePipeHeight * 0.5f;

        for (int i = 0; i < count; i++)
        {
            float t = count > 1 ? (float)i / (count - 1) : 0f;
            float offsetY = EvaluatePreviewOffsetY(t);

            Vector3 gapCenter = baseCenter + dir * (i * pipeSpacing) + Vector3.up * offsetY;
            CreatePreviewDummy(previewContainer, gapCenter + Vector3.up * offsetFromGapCenter, dir, pipePrefab);
            CreatePreviewDummy(previewContainer, gapCenter - Vector3.up * offsetFromGapCenter, dir, pipePrefab);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 baseCenter = transform.position + (Vector3)spawnOffset;
        Vector3 dir = moveDirection.sqrMagnitude > 0.001f ? (Vector3)moveDirection.normalized : Vector3.left;

        Gizmos.color = new Color(1f, 1f, 0f, 0.35f);
        Gizmos.DrawWireCube(baseCenter, new Vector3(0.5f, maxAmplitudeY * 2f + gapHeight, 0f));

        bool hasPipeShape = false;
        CollisionShapeConfig pipeShape = default;
        if (pipePrefab != null)
        {
            if (pipePrefab.TryGetComponent<EnemyAttackBase>(out var ea))
                hasPipeShape = ea.TryGetDamageShape(out pipeShape);
            else if (pipePrefab.TryGetComponent<DamageCasterBase>(out var dc))
                hasPipeShape = dc.TryGetDamageShape(out pipeShape);
        }

        float offsetFromGapCenter = gapHeight * 0.5f + singlePipeHeight * 0.5f;
        Vector3 prevPoint = Vector3.zero;
        int previewCount = Mathf.Max(1, pipeCount);

        for (int i = 0; i < previewCount; i++)
        {
            float t = previewCount > 1 ? (float)i / (previewCount - 1) : 0f;
            float offsetY = EvaluatePreviewOffsetY(t);

            Vector3 gapCenter = baseCenter + dir * (i * pipeSpacing) + Vector3.up * offsetY;

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(gapCenter, new Vector3(0.6f, gapHeight, 0f));
            if (i > 0) Gizmos.DrawLine(prevPoint, gapCenter);
            prevPoint = gapCenter;

            // 畫出上下兩根水管的真實碰撞紅框
            if (hasPipeShape)
            {
                Quaternion rot = pipePrefab.transform.rotation;
                Color pipeColor = new Color(1f, 0.2f, 0.2f, 0.5f);
                pipeShape.DrawGizmo(gapCenter + Vector3.up * offsetFromGapCenter, rot, pipeColor);
                pipeShape.DrawGizmo(gapCenter - Vector3.up * offsetFromGapCenter, rot, pipeColor);
            }
        }
    }

    private float EvaluatePreviewOffsetY(float t)
    {
        if (curveMode == CurveMode.SineWave)
            return Mathf.Sin(t * waveCycles * Mathf.PI * 2f) * maxAmplitudeY;
        if (curveMode == CurveMode.CustomCurve)
            return customCurve.Evaluate(t) * maxAmplitudeY;
        return 0f;
    }
}
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FlappyPipeWavePattern : AttackPatternBase
{
    public enum CurveMode
    {
        SineWave,           // 規則正弦波曲線
        ClampedRandomWalk,  // 隨機但嚴格限制「相鄰兩根最大落差」
        CustomCurve         // 在 Inspector 直接手拉 AnimationCurve
    }

    [Header("★ 水管 Prefab 與基礎數值")]
    [SerializeField] private GameObject pipePrefab;
    [SerializeField] private float baseSpeed = 6f;
    [SerializeField] private float patternDamageMultiplier = 1f;
    [Tooltip("單根水管的高度（需與 Prefab 一致）")]
    [SerializeField] private float singlePipeHeight = 15f;

    [Header("★ 波次與密度控制")]
    [Tooltip("總共生成幾組（上下一對為一組）")]
    [Min(1)] public int pipeCount = 10;
    [Tooltip("相鄰兩組水管的水平間距（世界單位，數值越小越密集）")]
    [Min(0.5f)] public float pipeSpacing = 3.5f;
    [Tooltip("統一管理的通行缺口高度")]
    public float gapHeight = 3.0f;

    [Header("★ 生成基準與方向")]
    public Vector2 spawnOffset = new Vector2(10f, 0f);
    public Vector2 moveDirection = Vector2.left;

    [Header("★ 曲線與落差控制")]
    public CurveMode curveMode = CurveMode.ClampedRandomWalk;
    [Tooltip("缺口中心點能上下擺動的最大範圍 (±值)")]
    public float maxAmplitudeY = 3.5f;

    [Header("SineWave 專用")]
    [Tooltip("整波攻擊內完成幾個波浪週期")]
    public float waveCycles = 1.5f;
    public bool randomStartPhase = true;

    [Header("ClampedRandomWalk 專用")]
    [Tooltip("相鄰兩根水管的缺口高度最大允許落差（避免出現不可能跳過的斷層）")]
    public float maxStepDeltaY = 1.2f;

    [Header("CustomCurve 專用")]
    [Tooltip("橫軸 0~1 代表第 1 根到最後 1 根；縱軸 -1~1 會乘上 maxAmplitudeY")]
    public AnimationCurve customCurve = AnimationCurve.EaseInOut(0f, -1f, 1f, 1f);

    protected override void OnExecute(BossStateMachine boss, float speedMultiplier, bool isAngry)
    {
        transform.SetParent(null);
        if (boss != null) boss.RegisterActivePattern(this.gameObject);

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

        Vector2 baseCenter = (Vector2)transform.position + spawnOffset;
        Vector2 dir = moveDirection.sqrMagnitude > 0.001f ? moveDirection.normalized : Vector2.left;

        // 預先算好整條曲線的缺口 Y 座標
        float[] gapOffsetsY = GenerateCurveOffsets();

        // 依據「距離 / 速度」決定生成間隔，確保任何倍速下密度一致
        float interval = speed > 0.01f ? (pipeSpacing / speed) : 0.5f;
        float offsetFromGapCenter = (gapHeight * 0.5f) + (singlePipeHeight * 0.5f);

        for (int i = 0; i < pipeCount; i++)
        {
            float gapCenterY = baseCenter.y + gapOffsetsY[i];

            // 同一幀同時生成上、下兩根水管
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
            float t = (pipeCount > 1) ? (float)i / (pipeCount - 1) : 0f;

            switch (curveMode)
            {
                case CurveMode.SineWave:
                    offsets[i] = Mathf.Sin(t * waveCycles * Mathf.PI * 2f + phase) * maxAmplitudeY;
                    break;

                case CurveMode.ClampedRandomWalk:
                    if (i > 0)
                    {
                        // 嚴格限制下一根與上一根的落差在 [-maxStepDeltaY, +maxStepDeltaY]
                        float delta = Random.Range(-maxStepDeltaY, maxStepDeltaY);
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
        // 若需要編輯器點擊預覽，可在此生成假物件；平時直接看下方 Gizmos 即可
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 baseCenter = transform.position + (Vector3)spawnOffset;
        Vector3 dir = moveDirection.sqrMagnitude > 0.001f ? (Vector3)moveDirection.normalized : Vector3.left;

        // 畫出上下擺動邊界
        Gizmos.color = new Color(1f, 1f, 0f, 0.35f);
        Gizmos.DrawWireCube(baseCenter, new Vector3(0.5f, maxAmplitudeY * 2f + gapHeight, 0f));

        // 即時預覽曲線軌跡與每組缺口位置（沿著飛行方向展開）
        Gizmos.color = Color.cyan;
        Vector3 prevPoint = Vector3.zero;

        for (int i = 0; i < pipeCount; i++)
        {
            float t = (pipeCount > 1) ? (float)i / (pipeCount - 1) : 0f;
            float offsetY = 0f;

            if (curveMode == CurveMode.SineWave)
                offsetY = Mathf.Sin(t * waveCycles * Mathf.PI * 2f) * maxAmplitudeY;
            else if (curveMode == CurveMode.CustomCurve)
                offsetY = customCurve.Evaluate(t) * maxAmplitudeY;

            // 越早生成的越往飛行方向延伸，方便在編輯器一眼看出整波密度與曲度
            Vector3 gapCenter = baseCenter + dir * (i * pipeSpacing) + Vector3.up * offsetY;
            Gizmos.DrawWireCube(gapCenter, new Vector3(0.6f, gapHeight, 0f));

            if (i > 0) Gizmos.DrawLine(prevPoint, gapCenter);
            prevPoint = gapCenter;
        }
    }
}
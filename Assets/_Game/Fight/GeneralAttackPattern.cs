using UnityEngine;
using System.Collections.Generic;

public class GeneralAttackPattern : BulletSpawnerBase
{
    public enum PatternType
    {
        Circle,
        Shotgun,
        Sniper,
        RandomSpray,
        RandomRain,
        LinearLine,
        Simple,
        BoxPerimeter
    }

    [Header("攻擊模式")]
    public PatternType patternType = PatternType.Circle;

    [Header("彈幕數量")]
    [Min(1)] public int bulletCount = 20;

    [Header("生成範圍")]
    public Vector2 spawnAreaSize = new Vector2(5, 5);

    [Header("環形設定")]
    [Tooltip("圓形半徑；0 表示在同一點生成")]
    public float circleRadius = 0f;
    [Range(0, 360)] public float circleStartAngle = 0f;

    [Header("扇形設定")]
    public float spreadAngle = 90f;

    [Header("狙擊設定")]
    [Tooltip("瞄準玩家時的隨機誤差角度")]
    public float sniperSpreadAngle = 5f;

    [Header("方向控制")]
    public bool useRandomDirection = true;
    public bool aimAtPlayer = false;
    [Range(0, 360)] public float fixedAngle = 270f;

    [Header("方形邊緣設定")]
    [Tooltip("勾選後改為向矩形中心射擊")]
    public bool fireInwardToCenter = false;

    protected override List<SpawnData> CalculateAllSpawnData()
    {
        bulletCount = Mathf.Max(1, bulletCount);
        List<SpawnData> list = new List<SpawnData>(bulletCount);

        switch (patternType)
        {
            case PatternType.Circle:
            {
                float angleStep = 360f / bulletCount;
                for (int i = 0; i < bulletCount; i++)
                {
                    float currentAngle = circleStartAngle + i * angleStep;
                    Vector2 dir = AngleToVector(currentAngle);
                    Vector2 pos = transform.position;
                    if (circleRadius > 0f) pos += dir * circleRadius;
                    list.Add(new SpawnData { position = pos, direction = dir });
                }
                break;
            }

            case PatternType.Shotgun:
            {
                Vector2 baseDir = GetDesiredDirection();
                float baseAngle = Mathf.Atan2(baseDir.y, baseDir.x) * Mathf.Rad2Deg;
                float startAngle = baseAngle - spreadAngle * 0.5f;
                float step = bulletCount > 1 ? spreadAngle / (bulletCount - 1) : 0f;

                for (int i = 0; i < bulletCount; i++)
                {
                    float currentAngle = startAngle + step * i;
                    list.Add(new SpawnData { position = transform.position, direction = AngleToVector(currentAngle) });
                }
                break;
            }

            case PatternType.Simple:
            {
                Vector2 dir = GetDesiredDirection();
                for (int i = 0; i < bulletCount; i++)
                {
                    list.Add(new SpawnData { position = transform.position, direction = dir });
                }
                break;
            }

            case PatternType.Sniper:
            {
                Vector2 targetDir = GetDirToPlayer();
                float baseAngle = Mathf.Atan2(targetDir.y, targetDir.x) * Mathf.Rad2Deg;

                for (int i = 0; i < bulletCount; i++)
                {
                    float offset = Random.Range(-sniperSpreadAngle, sniperSpreadAngle);
                    list.Add(new SpawnData { position = transform.position, direction = AngleToVector(baseAngle + offset) });
                }
                break;
            }

            case PatternType.RandomSpray:
            {
                for (int i = 0; i < bulletCount; i++)
                {
                    Vector2 dir = useRandomDirection ? Random.insideUnitCircle.normalized : GetDesiredDirection();
                    list.Add(new SpawnData { position = GetRandomSpawnPos(), direction = dir });
                }
                break;
            }

            case PatternType.RandomRain:
            {
                for (int i = 0; i < bulletCount; i++)
                {
                    list.Add(new SpawnData { position = GetRandomSpawnPos(), direction = Vector2.down });
                }
                break;
            }

            case PatternType.LinearLine:
            {
                Vector2 startPos = (Vector2)transform.position - new Vector2(spawnAreaSize.x / 2f, 0f);
                Vector2 endPos = (Vector2)transform.position + new Vector2(spawnAreaSize.x / 2f, 0f);

                for (int i = 0; i < bulletCount; i++)
                {
                    float t = bulletCount > 1 ? (float)i / (bulletCount - 1) : 0.5f;
                    Vector2 spawnPos = Vector2.Lerp(startPos, endPos, t);
                    spawnPos.y += Random.Range(-spawnAreaSize.y / 2f, spawnAreaSize.y / 2f);

                    Vector2 dir;
                    if (aimAtPlayer)
                    {
                        dir = GetDirToPlayer();
                    }
                    else if (useRandomDirection)
                    {
                        dir = Random.insideUnitCircle.normalized;
                    }
                    else
                    {
                        Vector2 offsetFromCenter = spawnPos - (Vector2)transform.position;
                        dir = offsetFromCenter != Vector2.zero
                            ? offsetFromCenter.normalized
                            : AngleToVector(fixedAngle);
                    }

                    list.Add(new SpawnData { position = spawnPos, direction = dir });
                }
                break;
            }

            case PatternType.BoxPerimeter:
            {
                float w = spawnAreaSize.x;
                float h = spawnAreaSize.y;
                float perimeter = 2f * (w + h);
                float step = perimeter / bulletCount;

                for (int i = 0; i < bulletCount; i++)
                {
                    float d = i * step;
                    Vector2 localPos;

                    if (d < w)
                    {
                        localPos = new Vector2(-w / 2f + d, -h / 2f);
                    }
                    else if (d < w + h)
                    {
                        localPos = new Vector2(w / 2f, -h / 2f + d - w);
                    }
                    else if (d < 2 * w + h)
                    {
                        localPos = new Vector2(w / 2f - (d - w - h), h / 2f);
                    }
                    else
                    {
                        localPos = new Vector2(-w / 2f, h / 2f - (d - 2 * w - h));
                    }

                    Vector2 worldPos = (Vector2)transform.position + localPos;
                    Vector2 dir;

                    if (aimAtPlayer)
                    {
                        dir = GetDirToPlayer();
                    }
                    else if (useRandomDirection)
                    {
                        dir = Random.insideUnitCircle.normalized;
                    }
                    else if (localPos != Vector2.zero)
                    {
                        dir = localPos.normalized;
                        if (fireInwardToCenter) dir = -dir;
                    }
                    else
                    {
                        dir = AngleToVector(fixedAngle);
                    }

                    list.Add(new SpawnData { position = worldPos, direction = dir });
                }
                break;
            }
        }

        return list;
    }

    [ContextMenu("測試發射")]
    public void DebugTest()
    {
        if (!Application.isPlaying)
        {
            Debug.LogError("請先按 Play！");
            return;
        }

        Execute(null, 1f, false);
    }

    protected override void OnGeneratePreview(Transform previewContainer)
    {
        var dataList = CalculateAllSpawnData();
        foreach (var data in dataList)
        {
            CreatePreviewDummy(previewContainer, data.position, data.direction, bulletPrefab);
        }
    }

    private Vector2 GetDesiredDirection()
    {
        return aimAtPlayer ? GetDirToPlayer() : AngleToVector(fixedAngle);
    }

    private Vector2 AngleToVector(float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
    }

    private Vector2 GetRandomSpawnPos()
    {
        float x = Random.Range(-spawnAreaSize.x / 2f, spawnAreaSize.x / 2f);
        float y = Random.Range(-spawnAreaSize.y / 2f, spawnAreaSize.y / 2f);
        return (Vector2)transform.position + (Vector2)transform.right * x + (Vector2)transform.up * y;
    }

    private Vector2 GetDirToPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        return player != null
            ? (player.transform.position - transform.position).normalized
            : Vector2.down;
    }

    private void OnDrawGizmosSelected()
    {
        Matrix4x4 oldMatrix = Gizmos.matrix;

        // 1. 畫生成區域框（僅在需要區域的模式顯示，並修正你原本重複畫兩次的冗餘）
        if (patternType == PatternType.RandomSpray ||
            patternType == PatternType.RandomRain ||
            patternType == PatternType.LinearLine ||
            patternType == PatternType.BoxPerimeter)
        {
            Gizmos.color = Color.green;
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(spawnAreaSize.x, spawnAreaSize.y, 0f));
            Gizmos.matrix = oldMatrix;
        }

        if (patternType == PatternType.Circle && circleRadius > 0f)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, circleRadius);
        }

        if (patternType == PatternType.Shotgun)
        {
            Gizmos.color = Color.yellow;
            Vector2 baseDir = aimAtPlayer ? GetDirToPlayer() : AngleToVector(fixedAngle);
            float baseAngle = Mathf.Atan2(baseDir.y, baseDir.x) * Mathf.Rad2Deg;
            float half = spreadAngle * 0.5f;
            Gizmos.DrawLine(transform.position, transform.position + (Vector3)(AngleToVector(baseAngle - half) * 2f));
            Gizmos.DrawLine(transform.position, transform.position + (Vector3)(AngleToVector(baseAngle + half) * 2f));
        }

        if (patternType == PatternType.Simple || patternType == PatternType.Sniper)
        {
            Gizmos.color = Color.magenta;
            Vector3 dir = aimAtPlayer ? (Vector3)GetDirToPlayer() : (Vector3)AngleToVector(fixedAngle);
            Gizmos.DrawLine(transform.position, transform.position + dir * 2f);
        }

        // 2. 針對確定性陣型，直接畫出每顆子彈的真實碰撞紅框
        if (bulletPrefab != null &&
            patternType != PatternType.RandomSpray &&
            patternType != PatternType.RandomRain)
        {
            bool hasShape = false;
            CollisionShapeConfig shape = default;
            if (bulletPrefab.TryGetComponent<EnemyAttackBase>(out var ea)) hasShape = ea.TryGetDamageShape(out shape);
            else if (bulletPrefab.TryGetComponent<DamageCasterBase>(out var dc)) hasShape = dc.TryGetDamageShape(out shape);

            if (hasShape)
            {
                var dataList = CalculateAllSpawnData();
                Color hitColor = new Color(1f, 0.2f, 0.2f, 0.5f);
                foreach (var data in dataList)
                {
                    Quaternion rot = Quaternion.Euler(0f, 0f, Mathf.Atan2(data.direction.y, data.direction.x) * Mathf.Rad2Deg - 90f);
                    shape.DrawGizmo(data.position, rot, hitColor);
                }
            }
        }
    }
}

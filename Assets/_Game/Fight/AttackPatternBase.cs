using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class AttackPatternBase : MonoBehaviour
{
    [Header("基礎設定")]
    [Tooltip("彈幕發射完畢後，是否自動銷毀此發射器")]
    public bool destroyOnFinish = true;

    [Header("空間定位")]
    [Tooltip("執行時強制移動到世界座標原點並重置旋轉")]
    public bool snapToWorldOrigin = false;

    [Header("預警系統")]
    [Tooltip("是否啟用發射前警告")]
    public bool useWarning = false;
    [Tooltip("警告持續時間 (秒)")]
    public float warningTime = 1.0f;
    [Tooltip("警告 Prefab")]
    public GameObject warningPrefab;

    protected BossStateMachine _ownerBoss;

    public event Action PatternFinished;
    public bool IsFinished { get; private set; }

    private readonly List<GameObject> _activeWarnings = new List<GameObject>();

    public void Execute(BossStateMachine boss, float speedMultiplier, bool isAngry)
    {
        IsFinished = false;
        _ownerBoss = boss;

        if (snapToWorldOrigin)
        {
            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;
        }

        if (boss != null)
        {
            boss.RegisterActivePattern(gameObject);
        }

        OnExecute(boss, speedMultiplier, isAngry);
    }

    protected abstract void OnExecute(BossStateMachine boss, float speedMultiplier, bool isAngry);

    /// <summary>標記模式已完成；由發射序列與子 Pattern 共同使用。</summary>
    protected void FinishPattern()
    {
        if (IsFinished)
        {
            return;
        }

        IsFinished = true;

        if (_ownerBoss != null)
        {
            _ownerBoss.UnregisterActivePattern(gameObject);
            _ownerBoss = null;
        }

        PatternFinished?.Invoke();

        if (destroyOnFinish)
        {
            Destroy(gameObject);
        }
    }

    protected virtual void OnPatternCleanup()
    {
    }

    protected virtual void OnDestroy()
    {
        StopAllCoroutines();
        OnPatternCleanup();
        CleanupWarnings();

        if (!IsFinished)
        {
            IsFinished = true;
            PatternFinished?.Invoke();
        }

        if (_ownerBoss != null)
        {
            _ownerBoss.UnregisterActivePattern(gameObject);
            _ownerBoss = null;
        }
    }

    // Editor preview

    private const string PREVIEW_CONTAINER_NAME = "--- Preview Container ---";

    [ContextMenu("生成預覽 (預覽彈幕軌跡)")]
    public void GenerateEditorPreview()
    {
        ClearEditorPreview();

        GameObject container = new GameObject(PREVIEW_CONTAINER_NAME);
        container.transform.SetParent(transform);
        container.transform.localPosition = Vector3.zero;
        container.transform.localRotation = Quaternion.identity;
        container.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;

        OnGeneratePreview(container.transform);
    }

    [ContextMenu("清除預覽")]
    public void ClearEditorPreview()
    {
        Transform oldContainer = transform.Find(PREVIEW_CONTAINER_NAME);
        if (oldContainer != null)
        {
            DestroyImmediate(oldContainer.gameObject);
        }
    }

    protected abstract void OnGeneratePreview(Transform previewContainer);

    /// <summary>依 Prefab 的傷害形狀建立預覽物件。</summary>
    protected void CreatePreviewDummy(Transform container, Vector3 position, Vector3 direction, GameObject sourcePrefab = null)
    {
        PrimitiveType primitiveType = PrimitiveType.Cube;
        Vector3 dummyScale = new Vector3(0.1f, 0.4f, 0.1f);
        Quaternion shapeOffsetRot = Quaternion.identity;
        bool shouldAlignWithDirection = true;

        if (sourcePrefab != null)
        {
            bool isProjectile = sourcePrefab.TryGetComponent<IProjectile>(out _);
            if (!isProjectile)
            {
                shouldAlignWithDirection = false;
            }
            else if (sourcePrefab.TryGetComponent<EnemyBullet>(out var enemyBullet) &&
                     enemyBullet.bulletData != null &&
                     !enemyBullet.bulletData.alignRotationWithDirection)
            {
                shouldAlignWithDirection = false;
            }

            if (TryExtractDamageShape(sourcePrefab, out CollisionShapeConfig shape))
            {
                shapeOffsetRot = Quaternion.Euler(shape.shapeRotationOffset);
                float zDepth = shape.zThickness > 0f ? shape.zThickness : 0.2f;

                switch (shape.shapeType)
                {
                    case BulletShapeType.Box:
                        primitiveType = PrimitiveType.Cube;
                        dummyScale = new Vector3(shape.boxSize.x, shape.boxSize.y, zDepth);
                        break;

                    case BulletShapeType.Circle:
                        primitiveType = PrimitiveType.Sphere;
                        float diameter = shape.radius * 2f;
                        dummyScale = new Vector3(diameter, diameter, zDepth);
                        break;

                    case BulletShapeType.Capsule:
                        primitiveType = PrimitiveType.Capsule;
                        float capDiameter = shape.radius * 2f;
                        float capHeight = Mathf.Max(capDiameter, shape.capsuleLength);
                        dummyScale = new Vector3(capDiameter, capHeight * 0.5f, capDiameter);
                        break;
                }
            }
            else
            {
                dummyScale = sourcePrefab.transform.localScale;
            }
        }

        GameObject dummy = GameObject.CreatePrimitive(primitiveType);
        dummy.name = sourcePrefab != null ? $"Preview ({sourcePrefab.name})" : "Preview Bullet";
        dummy.transform.SetParent(container);
        dummy.transform.position = position;
        dummy.transform.localScale = dummyScale;

        Quaternion baseRot = shouldAlignWithDirection && direction.sqrMagnitude > 0.0001f
            ? Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f)
            : (sourcePrefab != null ? sourcePrefab.transform.rotation : Quaternion.identity);

        dummy.transform.rotation = baseRot * shapeOffsetRot;
        DestroyImmediate(dummy.GetComponent<Collider>());
    }

    private bool TryExtractDamageShape(GameObject prefab, out CollisionShapeConfig shape)
    {
        if (prefab.TryGetComponent<EnemyAttackBase>(out var enemyAttack) &&
            enemyAttack.TryGetDamageShape(out shape))
        {
            return true;
        }

        if (prefab.TryGetComponent<DamageCasterBase>(out var damageCaster) &&
            damageCaster.TryGetDamageShape(out shape))
        {
            return true;
        }

        shape = default;
        return false;
    }

    public struct SpawnData
    {
        public Vector2 position;
        public Vector2 direction;
        public Vector3[] predictedTrajectory;
    }

    protected IEnumerator ShowWarningsAndWait(List<SpawnData> spawnDataList)
    {
        if (!useWarning || warningTime <= 0f || warningPrefab == null || spawnDataList == null || spawnDataList.Count == 0)
        {
            yield break;
        }

        foreach (var data in spawnDataList)
        {
            GameObject warningObj = Instantiate(warningPrefab, data.position, Quaternion.identity);
            float angle = Mathf.Atan2(data.direction.y, data.direction.x) * Mathf.Rad2Deg;
            warningObj.transform.rotation = Quaternion.Euler(0, 0, angle - 90f);

            TrajectoryVisualizer visualizer = warningObj.GetComponentInChildren<TrajectoryVisualizer>();
            if (visualizer != null)
            {
                visualizer.DrawTrajectory(data.predictedTrajectory);
            }

            _activeWarnings.Add(warningObj);
        }

        try
        {
            yield return new WaitForSeconds(warningTime);
        }
        finally
        {
            CleanupWarnings();
        }
    }

    private void CleanupWarnings()
    {
        for (int i = _activeWarnings.Count - 1; i >= 0; i--)
        {
            if (_activeWarnings[i] != null)
            {
                Destroy(_activeWarnings[i]);
            }
        }
        _activeWarnings.Clear();
    }
}
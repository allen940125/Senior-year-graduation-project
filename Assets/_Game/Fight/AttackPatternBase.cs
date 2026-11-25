using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class AttackPatternBase : MonoBehaviour
{
    [Header("★ 基礎設定 (全發射器共用)")]
    [Tooltip("彈幕發射完畢後，是否自動銷毀此發射器？(勾選可保持場景乾淨)")]
    public bool destroyOnFinish = true;

    // ★ 新增空間定位系統
    [Header("★ 空間定位 (Spatial Positioning)")]
    [Tooltip("勾選後，此發射器生成時會無視 Boss 的槍口位置，強制將自身移動到世界座標 (0,0,0)，並重置旋轉。非常適合全畫面陣型或序列。")]
    public bool snapToWorldOrigin = false;
    
    [Header("★ 預警系統 (Telegraph)")]
    [Tooltip("是否啟用發射前警告？")]
    public bool useWarning = false;
    [Tooltip("警告持續時間 (秒)")]
    public float warningTime = 1.0f;
    [Tooltip("要生成的警告 Prefab (如紅色射線、驚嘆號標誌)")]
    public GameObject warningPrefab;
    
    protected BossStateMachine _ownerBoss;

// ==========================================
    // 遊戲執行時的生命週期
    // ==========================================
    public void Execute(BossStateMachine boss, float speedMultiplier, bool isAngry)
    {
        _ownerBoss = boss;

        // ★ 核心攔截：在子類別執行任何數學運算前，強制校正空間位置
        if (snapToWorldOrigin)
        {
            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;
        }

        OnExecute(boss, speedMultiplier, isAngry);
    }

    protected abstract void OnExecute(BossStateMachine boss, float speedMultiplier, bool isAngry);
    

    /// <summary>
    /// ★ 統一的結束方法：子類別發射完畢後，呼叫此方法即可，不要自己寫 Destroy()
    /// </summary>
    protected void FinishPattern()
    {
        if (destroyOnFinish)
        {
            Destroy(gameObject);
        }
    }

    protected virtual void OnDestroy()
    {
        if (_ownerBoss != null)
        {
            _ownerBoss.UnregisterActivePattern(this.gameObject);
        }
    }

    // ==========================================
    // ★ 編輯器預覽系統 (統一由基底類別管理)
    // ==========================================
    
    private const string PREVIEW_CONTAINER_NAME = "--- Preview Container ---";

    [ContextMenu("👁️ 生成預覽 (預覽彈幕軌跡)")]
    public void GenerateEditorPreview()
    {
        ClearEditorPreview(); // 永遠先清除舊的，確保按第二次就是刷新

        // 建立一個專屬容器來放預覽物件
        GameObject container = new GameObject(PREVIEW_CONTAINER_NAME);
        container.transform.SetParent(this.transform);
        container.transform.localPosition = Vector3.zero;
        container.transform.localRotation = Quaternion.identity;
        
        // ★ 防呆標記：設定為不存檔，避免你存 Prefab 的時候把預覽垃圾也存進去
        container.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;

        // 呼叫子類別，請子類別把預覽物件塞進這個容器
        OnGeneratePreview(container.transform);
        
        Debug.Log($"<color=cyan>[預覽成功]</color> 已在 {gameObject.name} 產生預覽幾何體！");
    }

    [ContextMenu("❌ 清除預覽")]
    public void ClearEditorPreview()
    {
        Transform oldContainer = transform.Find(PREVIEW_CONTAINER_NAME);
        if (oldContainer != null)
        {
            // 在 Editor 模式下必須用 DestroyImmediate
            DestroyImmediate(oldContainer.gameObject); 
        }
    }
    
    protected abstract void OnGeneratePreview(Transform previewContainer);

    /// <summary>
    /// ★ 共用工具：依傳入的 Prefab 真實碰撞形狀與尺寸生成預覽幾何體
    /// </summary>
    protected void CreatePreviewDummy(Transform container, Vector3 position, Vector3 direction, GameObject sourcePrefab = null)
    {
        PrimitiveType primitiveType = PrimitiveType.Cube;
        Vector3 dummyScale = new Vector3(0.1f, 0.4f, 0.1f);
        Quaternion shapeOffsetRot = Quaternion.identity;
        bool shouldAlignWithDirection = true;

        if (sourcePrefab != null)
        {
            // 1. 判斷是否該跟著飛行方向旋轉
            // 非投射物（如 AoE）或明確關閉自動旋轉的子彈（如水管），預覽維持原姿態
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

            // 2. 優先讀取真實碰撞形狀與尺寸
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
                        // Unity 內建 Capsule 預設直徑為 1、總高為 2，因此 Y 軸縮放需乘以 0.5
                        float capDiameter = shape.radius * 2f;
                        float capHeight = Mathf.Max(capDiameter, shape.capsuleLength);
                        dummyScale = new Vector3(capDiameter, capHeight * 0.5f, capDiameter);
                        break;
                }
            }
            else
            {
                // 3. 若無碰撞資料，退回使用 Prefab 自身的縮放
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
    
    // 定義一個標準資料包，用來鎖死「算出來的位置與方向」
    public struct SpawnData
    {
        public Vector2 position;
        public Vector2 direction;
        // ★ 新增：這顆子彈未來的預測軌跡點 (由發射器算好塞進來)
        public Vector3[] predictedTrajectory; 
    }
    
    // ==========================================
    // ★ 核心：統一的預警處理協程
    // ==========================================
    /// <summary>
    /// 傳入算好的點位，此方法會自動生成警告、等待、並在結束時精準銷毀警告
    /// </summary>
    // ==========================================
    // ★ 核心：統一的預警處理協程
    // ==========================================
    protected IEnumerator ShowWarningsAndWait(List<SpawnData> spawnDataList)
    {
        if (!useWarning || warningTime <= 0f || warningPrefab == null || spawnDataList.Count == 0) 
        {
            Debug.LogWarning($"<color=orange>[預警攔截]</color> {gameObject.name} 沒顯示預警。原因檢查：useWarning={useWarning}, time={warningTime}, prefab={(warningPrefab!=null?"有":"無")}, 數量={spawnDataList.Count}");
            yield break;
        }

        List<GameObject> activeWarnings = new List<GameObject>();

        foreach (var data in spawnDataList)
        {
            GameObject warningObj = Instantiate(warningPrefab, data.position, Quaternion.identity);
            
            // ★ 加這行，只要有印出來，代表 100% 有生成在場景裡，絕對是被擋住或太小！
            Debug.Log($"<color=green>[預警生成]</color> 已在 {data.position} 生成預警物件：{warningObj.name}");
            
            float angle = Mathf.Atan2(data.direction.y, data.direction.x) * Mathf.Rad2Deg;
            warningObj.transform.rotation = Quaternion.Euler(0, 0, angle - 90f);
            
            TrajectoryVisualizer visualizer = warningObj.GetComponentInChildren<TrajectoryVisualizer>();
            if (visualizer != null)
            {
                // ★ 完美對接：把發射器已經算好的軌跡點陣列，直接交給畫筆！
                // Visualizer 拿到陣列後，就會照著裡面的點畫出折線 (反彈) 或 曲線 (導彈)
                visualizer.DrawTrajectory(data.predictedTrajectory);
            }

            activeWarnings.Add(warningObj);
        }

        yield return new WaitForSeconds(warningTime);

        foreach (var w in activeWarnings)
        {
            if (w != null) Destroy(w);
        }
    }
}
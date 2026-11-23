using System.Collections;
using Gamemanager;
using UnityEngine;
using UnityEngine.UI; // 若使用 TextMeshPro，請改為 using TMPro; 並將下方 Text 改為 TMP_Text

public class TutorialManager : MonoBehaviour
{
    [Header("教學用敵人藍圖")]
    [SerializeField] private EntityBlueprintSO dummyBlueprint;      // 木人樁
    [SerializeField] private EntityBlueprintSO antivirusBlueprint;  // 防毒軟體 BOSS

    [Header("教學 UI 設定")]
    [Tooltip("全螢幕壓黑的 Image，請掛上 Custom/UI/TutorialMaskCutout 材質")]
    [SerializeField] private Image darkMaskImage;
    [SerializeField] private Text hintText;
    [Tooltip("挖洞邊緣往外多留一點空間 (Viewport 比例，預設 0.02)")]
    [SerializeField] private float holePadding = 0.02f;

    [Header("3D 目標按鈕")]
    [SerializeField] private BaseButtonController3D attackButton3D;

    // Shader 屬性 ID
    private static readonly int HoleRectID = Shader.PropertyToID("_HoleRect");
    private Material _maskMatInstance;
    private Collider _currentHighlightCollider;

    // 流程狀態旗標
    private bool _isBossInIdle = false;
    private bool _hasAttackExecuted = false;
    private bool _isCurrentBossDead = false;
    private bool _isWaitingForUITransform = false;
    private bool _hasFirstUITransformed = false;

    private BossStateMachine _trackedBoss;

    private void Awake()
    {
// ★ 核心防呆：如果已經完成過新手教學，立刻自我銷毀，把控制權留給 BattleManager
        if (GameManager.Instance.HasCompletedTutorialBattle)
        {
            Destroy(gameObject);
            return;
        }

        // 只有「還沒打過教學」時，才會接管 BattleManager
        BattleManager bm = BattleManager.Instance != null
            ? BattleManager.Instance
            : FindFirstObjectByType<BattleManager>();

        if (bm != null)
        {
            bm.manualFlowControl = true;
        }
        else
        {
            Debug.LogError("[TutorialManager] 場景中找不到 BattleManager！");
        }

        // 實例化材質避免修改到專案原始 Material 資產
        if (darkMaskImage != null)
        {
            _maskMatInstance = new Material(darkMaskImage.material);
            darkMaskImage.material = _maskMatInstance;
            // ★ 關鍵：關掉 Raycast Target，才不會擋住 3D PhysicsRaycaster 的點擊
            darkMaskImage.raycastTarget = false;
            darkMaskImage.gameObject.SetActive(false);
        }
    }

    private void Start()
    {
        if (GameManager.Instance?.MainGameEvent != null)
        {
            var evt = GameManager.Instance.MainGameEvent;
            evt.SetSubscribe(evt.OnBossEnterIdlePhaseEvent, OnBossEnteredIdle);
            // 若「UI 變 Object」實際發送的是 OnInventoryDumpTriggeredEvent，請直接替換這行
            evt.SetSubscribe(evt.OnItemDroppedInWorldEvent, OnFirstUITransformed);
        }

        StartCoroutine(RunTutorialFlow());
    }

    private void LateUpdate()
    {
        // 每幀更新 3D 目標物件在螢幕上的挖洞位置
        if (darkMaskImage != null && darkMaskImage.gameObject.activeSelf && _currentHighlightCollider != null)
        {
            UpdateShaderHoleRect(_currentHighlightCollider.bounds);
        }
    }

    private void OnDestroy()
    {
        UnbindCurrentBoss();
        UnlockAllButtons();

        if (attackButton3D != null)
        {
            attackButton3D.OnCommandExecuted.RemoveListener(OnAttackButtonExecuted);
        }

        if (_maskMatInstance != null)
        {
            Destroy(_maskMatInstance);
        }

        if (GameManager.Instance?.MainGameEvent != null)
        {
            var evt = GameManager.Instance.MainGameEvent;
            evt.Unsubscribe<BossEnterIdlePhaseEvent>(OnBossEnteredIdle);
            evt.Unsubscribe<ItemDroppedInWorldEvent>(OnFirstUITransformed);
        }
    }

    private IEnumerator RunTutorialFlow()
    {
        // 0. 等待大管家初始化完成
        if (GameManager.Instance != null)
        {
            while (!GameManager.Instance.IsInitialized) yield return null;
        }
        yield return null;

        // ==========================================
        // 階段一：木人樁與 3D 攻擊按鈕教學
        // ==========================================
        _isBossInIdle = false;
        _isCurrentBossDead = false;
        yield return StartCoroutine(SpawnAndStartBattle(dummyBlueprint));

        // 等待木人樁打完第一輪彈幕進入 Idle
        yield return new WaitUntil(() => _isBossInIdle);

        // 鎖定其他按鈕，只開放攻擊按鈕並挖洞高亮
        _hasAttackExecuted = false;
        if (attackButton3D != null)
        {
            attackButton3D.OnCommandExecuted.AddListener(OnAttackButtonExecuted);
            SetTutorialButtonLock(attackButton3D, allowDragAndRotate: false);
            ShowHighlight3D(attackButton3D.gameObject, "請點擊攻擊");
        }

        // 等待玩家真的成功觸發攻擊按鈕
        yield return new WaitUntil(() => _hasAttackExecuted);

        if (attackButton3D != null)
        {
            attackButton3D.OnCommandExecuted.RemoveListener(OnAttackButtonExecuted);
        }
        HideHighlight();
        UnlockAllButtons();

        // 放行，等待木人樁死亡
        yield return new WaitUntil(() => _isCurrentBossDead);

        // ==========================================
        // 階段二：戰鬥中無縫切換防毒軟體 BOSS
        // ==========================================
        _isCurrentBossDead = false;
        _hasFirstUITransformed = false;
        _isWaitingForUITransform = true;
        yield return StartCoroutine(SpawnAndStartBattle(antivirusBlueprint));

        // ★ 修正：同時等「UI 變 Object」或「BOSS 先被打死」，防止死鎖
        yield return new WaitUntil(() => _hasFirstUITransformed || _isCurrentBossDead);
        _isWaitingForUITransform = false;

        // ==========================================
        // 階段三：只有在 BOSS 還沒死的情況下，才跳拖曳旋轉提示
        // ==========================================
        if (!_isCurrentBossDead)
        {
            Time.timeScale = 0f;
            ShowHighlight3D(attackButton3D != null ? attackButton3D.gameObject : null, "拖曳與旋轉物件來抵擋彈幕（按空白鍵或點擊繼續）");

            yield return null;
            yield return new WaitUntil(() => Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0));

            HideHighlight();
            Time.timeScale = 1f;

            // 繼續等玩家把防毒軟體打死
            yield return new WaitUntil(() => _isCurrentBossDead);
        }

        // ==========================================
        // 教學結束
        // ==========================================
        // ★ 標記教學已完成，之後再進 GameScene 就不會再觸發了
        GameManager.Instance.HasCompletedTutorialBattle = true;
        
        Debug.Log("[TutorialManager] 新手教學戰鬥完成，準備切換至探索場景");
        BattleManager.Instance.ChangeState(BattleState.Win);
    }

    private IEnumerator SpawnAndStartBattle(EntityBlueprintSO blueprint)
    {
        if (blueprint == null)
        {
            Debug.LogError("[TutorialManager] 未指派 EntityBlueprintSO！");
            yield break;
        }

        UnbindCurrentBoss();

        EntityRuntime runtimeData = new EntityRuntime();
        runtimeData.Initialize(blueprint);

        BattleManager.Instance.SpawnBoss(runtimeData);
        yield return null; // 等一幀讓 BossStateMachine.Start() 跑完

        _trackedBoss = BattleManager.Instance.CurrentBoss;
        if (_trackedBoss != null)
        {
            _trackedBoss.OnPhaseEntered += OnBossPhaseEntered;
        }

        BattleManager.Instance.ChangeState(BattleState.PlayerFight);
    }

    private void UnbindCurrentBoss()
    {
        if (_trackedBoss != null)
        {
            _trackedBoss.OnPhaseEntered -= OnBossPhaseEntered;
            _trackedBoss = null;
        }
    }

    // ==========================================
    // 3D 按鈕鎖定與解鎖
    // ==========================================
    private void SetTutorialButtonLock(BaseButtonController3D allowedButton, bool allowDragAndRotate)
    {
        var allButtons = FindObjectsByType<BaseButtonController3D>(FindObjectsSortMode.None);
        foreach (var btn in allButtons)
        {
            bool isTarget = (btn == allowedButton);
            btn.IsExternallyLocked = !isTarget;
            btn.SetInteractable(isTarget && allowDragAndRotate, isTarget && allowDragAndRotate);
        }
    }

    private void UnlockAllButtons()
    {
        var allButtons = FindObjectsByType<BaseButtonController3D>(FindObjectsSortMode.None);
        foreach (var btn in allButtons)
        {
            btn.IsExternallyLocked = false;
            btn.SetInteractable(true, true);
        }
    }

    // ==========================================
    // 3D 投影挖洞控制
    // ==========================================
    public void ShowHighlight3D(GameObject target3DObj, string message)
    {
        if (hintText != null) hintText.text = message;

        if (target3DObj != null)
        {
            _currentHighlightCollider = target3DObj.GetComponentInChildren<Collider>();
        }
        else
        {
            _currentHighlightCollider = null;
            if (_maskMatInstance != null) _maskMatInstance.SetVector(HoleRectID, Vector4.zero);
        }

        if (darkMaskImage != null)
        {
            darkMaskImage.gameObject.SetActive(true);
            if (_currentHighlightCollider != null)
            {
                UpdateShaderHoleRect(_currentHighlightCollider.bounds);
            }
        }
    }

    public void HideHighlight()
    {
        _currentHighlightCollider = null;
        if (darkMaskImage != null) darkMaskImage.gameObject.SetActive(false);
    }

    private void UpdateShaderHoleRect(Bounds bounds)
    {
        Camera cam = Camera.main;
        if (cam == null || _maskMatInstance == null) return;

        // 取出 3D Bounds 的 8 個頂點並投影到 Viewport (0~1)
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;
        Vector3[] corners = new Vector3[8]
        {
            new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z),
            new Vector3(min.x, max.y, min.z), new Vector3(max.x, max.y, min.z),
            new Vector3(min.x, min.y, max.z), new Vector3(max.x, min.y, max.z),
            new Vector3(min.x, max.y, max.z), new Vector3(max.x, max.y, max.z)
        };

        float minX = 1f, minY = 1f, maxX = 0f, maxY = 0f;
        for (int i = 0; i < 8; i++)
        {
            Vector3 vp = cam.WorldToViewportPoint(corners[i]);
            if (vp.x < minX) minX = vp.x;
            if (vp.y < minY) minY = vp.y;
            if (vp.x > maxX) maxX = vp.x;
            if (vp.y > maxY) maxY = vp.y;
        }

        Vector4 rect = new Vector4(
            Mathf.Clamp01(minX - holePadding),
            Mathf.Clamp01(minY - holePadding),
            Mathf.Clamp01(maxX + holePadding),
            Mathf.Clamp01(maxY + holePadding)
        );

        _maskMatInstance.SetVector(HoleRectID, rect);
        Debug.Log($"[TutorialMask] 攝影機: {cam.name} | 挖洞範圍: {rect}");
    }

    // ==========================================
    // 事件回呼
    // ==========================================
    private void OnBossEnteredIdle(BossEnterIdlePhaseEvent evt) => _isBossInIdle = true;
    private void OnAttackButtonExecuted() => _hasAttackExecuted = true;

    private void OnFirstUITransformed(ItemDroppedInWorldEvent evt)
    {
        if (_isWaitingForUITransform) _hasFirstUITransformed = true;
    }

    private void OnBossPhaseEntered(BossStateMachine.BossPhase phase)
    {
        if (phase == BossStateMachine.BossPhase.Dead) _isCurrentBossDead = true;
    }
}
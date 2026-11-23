using Game.SceneManagement;
using Gamemanager;
using UnityEngine;

public enum BattleState { Dialogue, PlayerMove, PlayerFight, BossDecide, Win, Lose }

public class BattleManager : MonoBehaviour
{
    public static BattleManager Instance { get; private set; }
    public BattleState currentState;

    [Header("場景設定")]
    public Transform bossSpawnPoint;
    [SerializeField] private DialogueUIManager dialogueUI;

    [Header("流程開關")]
    [Tooltip("★ 新增 1：勾選後，BattleManager 不會自動生怪跟開打，完全交給外部（如 TutorialManager）呼叫")]
    public bool manualFlowControl = false;

    [Tooltip("勾選後開場會走對話；未勾選則直接進入戰鬥")]
    [SerializeField] private bool enableOpeningDialogue = false;
    [Tooltip("Yarn Spinner 開場對話節點名稱")]
    [SerializeField] private string openingDialogueNode = "Boss_Opening";

    [Header("Debug / 單一場景測試設定")]
    public EntityBlueprintSO debugBossBlueprint;

    private BossStateMachine currentBoss;
    public BossStateMachine CurrentBoss => currentBoss;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        // ★ 修改 1：如果是手動模式（新手教學），Start() 什麼都不做，等外部指揮
        if (manualFlowControl) return;

        InitializeDefaultBoss();
        StartCoroutine(EvaluateOpeningFlowRoutine());
    }
    
    private void OnDestroy()
    {
        if (currentBoss != null)
        {
            currentBoss.OnPhaseEntered -= HandleBossPhaseEntered;
        }
    }

    private System.Collections.IEnumerator EvaluateOpeningFlowRoutine()
    {
        if (GameManager.Instance != null)
        {
            while (!GameManager.Instance.IsInitialized)
            {
                yield return null;
            }
        }

        yield return null;

        if (enableOpeningDialogue && dialogueUI != null)
        {
            ChangeState(BattleState.Dialogue);
            dialogueUI.StartDialogue(openingDialogueNode, onComplete: () =>
            {
                ChangeState(BattleState.PlayerFight);
            });
        }
        else
        {
            ChangeState(BattleState.PlayerFight);
        }
    }

    // 原本的 InitializeBoss 改成只負責「抓預設資料」，真正生怪交給下面的 SpawnBoss
    private void InitializeDefaultBoss()
    {
        EntityRuntime incomingData = GameFlowManager.PendingBattleEnemyData;

        if (incomingData == null)
        {
            if (debugBossBlueprint != null)
            {
                Debug.LogWarning($"<color=orange>[BattleManager Debug] 無跨場景資料，套用測試藍圖: {debugBossBlueprint.name}</color>");
                incomingData = new EntityRuntime();
                incomingData.Initialize(debugBossBlueprint);
            }
            else
            {
                Debug.LogError("[BattleManager] 找不到 Boss 資料，且未配置 debugBossBlueprint！");
                return;
            }
        }

        SpawnBoss(incomingData);
    }

    /// <summary>
    /// ★ 新增 2：公開的生怪方法。正式關卡跟新手教學都呼叫這個。
    /// </summary>
    public void SpawnBoss(EntityRuntime incomingData)
    {
        if (incomingData == null || incomingData.Blueprint == null || incomingData.Blueprint.battlePrefab == null)
        {
            Debug.LogError("[BattleManager] Boss 資料或 Prefab 缺失！");
            return;
        }

        // 清理舊 BOSS 前，先解除它的事件訂閱
        if (currentBoss != null)
        {
            currentBoss.OnPhaseEntered -= HandleBossPhaseEntered;
            Destroy(currentBoss.gameObject);
        }

        GameObject bossObj = Instantiate(incomingData.Blueprint.battlePrefab, bossSpawnPoint.position, bossSpawnPoint.rotation);
        currentBoss = bossObj.GetComponent<BossStateMachine>();

        if (currentBoss != null)
        {
            currentBoss.OnPhaseEntered += HandleBossPhaseEntered;
        }

        if (bossObj.TryGetComponent(out EntityCore bossCore))
        {
            bossCore.InjectRuntimeData(incomingData);
        }
    }

    // ★ 核心防呆：只有在「非手動模式」下，BOSS 死亡才會自動判勝離場
    private void HandleBossPhaseEntered(BossStateMachine.BossPhase phase)
    {
        if (phase != BossStateMachine.BossPhase.Dead) return;

        if (manualFlowControl)
        {
            // 手動模式（如新手教學）：不自動結算，交給外部決定要換下一隻怪還是結束戰鬥
            return;
        }

        ChangeState(BattleState.Win);
    }
    
    public void ChangeState(BattleState newState)
    {
        Debug.Log("[BattleManager] ChangeState: " + newState);
        currentState = newState;
        switch (currentState)
        {
            case BattleState.Dialogue:
                Debug.Log("開始對話");
                break;

            case BattleState.PlayerFight:
                Debug.Log("開始戰鬥" + currentBoss.bossName);
                if (GameManager.Instance?.MainGameEvent != null)
                {
                    GameManager.Instance.MainGameEvent.Send(new BattleInitiatedEvent());
                }
                if (currentBoss != null) currentBoss.StartBattle();
                Debug.Log("正式開始戰鬥" + currentBoss.bossName);
                break;

            case BattleState.Win:
                Debug.Log("玩家獲勝！");
                GameManager.Instance.SceneTransitionManager.LoadScene(SceneType.AdventureScene);
                break;

            case BattleState.Lose:
                Debug.Log("戰鬥失敗！");
                break;
        }
    }
}
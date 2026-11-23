using UnityEngine;
using Gamemanager;
using Game.SceneManagement;
using UnityEngine.Serialization;

[RequireComponent(typeof(Collider), typeof(EntityCore))]
public class OverworldEnemyEncounter : MonoBehaviour
{
    [SerializeField] private SceneType battleScene = SceneType.GameScene; 
    
    private bool _hasTriggered = false;
    private EntityCore _core;

    private void Awake() 
    {
        GetComponent<Collider>().isTrigger = true; 
        _core = GetComponent<EntityCore>(); 
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_hasTriggered) return;

        // ★ 1. 判定是不是玩家（確保玩家物件的 Tag 有設為 Player）
        if (!other.CompareTag("Player")) return;

        if (_core == null)
        {
            Debug.LogError("[明雷怪] 致命錯誤：找不到 EntityCore！");
            return;
        }

        _hasTriggered = true;

        GameManager.Instance.MainGameEvent.Send(new StartBattleEvent()
        {
            EnemyData = _core.RuntimeData,
            BattleScene = battleScene
        });
    }
}
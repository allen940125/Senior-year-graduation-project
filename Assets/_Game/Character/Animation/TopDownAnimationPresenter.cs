using GameFramework.Actors;
using GameFramework.Actors.TopDown2D;
using UnityEngine;

public class TopDownAnimationPresenter : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EntityAnimationController animController;
    [SerializeField] private TopDownStateMachine stateMachine;
    [SerializeField] private EntityHealthComponent healthComponent;

    [Header("Settings")]
    [SerializeField, Min(0f)] private float moveThreshold = 0.1f;
    [Tooltip("2D 逐幀動畫建議設 0；有骨架/混合需求再設 0.05 ~ 0.1")]
    [SerializeField, Min(0f)] private float transitionDuration = 0.05f;

    [Header("Animator State Names（必須與 Animator 視窗裡的節點名稱完全一致）")]
    [SerializeField] private string idleState = "Idle";
    [SerializeField] private string walkSideState = "Run";
    [SerializeField] private string walkUpState = "WalkUp";
    [SerializeField] private string walkDownState = "WalkDown";
    [SerializeField] private string hitState = "Hit";
    [SerializeField] private string dieState = "Die";

    private string _currentState;
    private bool _isPlayingHit;
    private bool _skipHitCheckNextUpdate;
    private bool _isDead;

    private void Awake()
    {
        if (animController == null) animController = GetComponent<EntityAnimationController>();
        if (stateMachine == null) stateMachine = GetComponent<TopDownStateMachine>();
        if (healthComponent == null) healthComponent = GetComponent<EntityHealthComponent>();
    }

    private void OnEnable()
    {
        if (healthComponent != null)
        {
            healthComponent.OnDamageTaken += HandleDamaged;
            healthComponent.OnDeath += HandleDead;
        }
    }

    private void OnDisable()
    {
        if (healthComponent != null)
        {
            healthComponent.OnDamageTaken -= HandleDamaged;
            healthComponent.OnDeath -= HandleDead;
        }
    }

    private void Update()
    {
        if (_isDead || animController == null || stateMachine == null || stateMachine.Context == null)
            return;

        // 受擊動畫播完前，鎖住移動動畫不蓋掉它
        if (_isPlayingHit)
        {
            // 剛觸發 CrossFade 的當幀 Animator 還沒更新狀態，跳過一次檢查避免被 IsNonLoopingAnimationFinished 誤判結束
            if (_skipHitCheckNextUpdate)
            {
                _skipHitCheckNextUpdate = false;
                return;
            }

            if (!animController.IsNonLoopingAnimationFinished(hitState))
                return;

            _isPlayingHit = false;
        }

        Vector3 dir = stateMachine.Context.MoveDirection;
        Vector2 move = new Vector2(dir.x, dir.y);

        string nextState = ResolveLocomotionState(move);
        PlayState(nextState);
    }

    private string ResolveLocomotionState(Vector2 move)
    {
        if (move.sqrMagnitude <= moveThreshold * moveThreshold)
            return idleState;

        if (Mathf.Abs(move.x) >= Mathf.Abs(move.y))
            return walkSideState;

        return move.y > 0f ? walkUpState : walkDownState;
    }

    private void PlayState(string stateName)
    {
        if (string.IsNullOrEmpty(stateName) || _currentState == stateName)
            return;

        _currentState = stateName;
        animController.CrossFade(stateName, transitionDuration);
    }

    private void HandleDamaged(float _)
    {
        if (_isDead || animController == null) return;

        _isPlayingHit = true;
        _skipHitCheckNextUpdate = true;
        _currentState = hitState;
        animController.CrossFade(hitState, transitionDuration);
    }

    private void HandleDead()
    {
        if (_isDead || animController == null) return;

        _isDead = true;
        _isPlayingHit = false;
        PlayState(dieState);
    }
}
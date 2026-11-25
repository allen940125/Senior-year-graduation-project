using UnityEngine;

public class BossAnimationPresenter : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private EntityHealthComponent healthComponent;

    [Header("Settings")]
    [Tooltip("2D 逐幀動畫建議設 0；有骨架/混合需求再設 0.05 ~ 0.1")]
    [SerializeField, Min(0f)] private float transitionDuration = 0.05f;

    [Header("Animator State Names（必須與 Animator 視窗節點名稱一致）")]
    [SerializeField] private string idleState = "Idle";
    [SerializeField] private string hitState = "Hit";
    [SerializeField] private string dieState = "Die";

    private bool _isPlayingHit;
    private bool _skipHitCheckNextUpdate;
    private bool _isDead;

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
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

    private void Start()
    {
        if (animator != null && !string.IsNullOrEmpty(idleState))
        {
            animator.CrossFadeInFixedTime(idleState, 0f);
        }
    }

    private void Update()
    {
        if (_isDead || !_isPlayingHit || animator == null)
            return;

        if (_skipHitCheckNextUpdate)
        {
            _skipHitCheckNextUpdate = false;
            return;
        }

        if (!IsAnimationFinished(hitState))
            return;

        _isPlayingHit = false;
        animator.CrossFadeInFixedTime(idleState, transitionDuration);
    }

    private void HandleDamaged(float _)
    {
        if (_isDead || animator == null || string.IsNullOrEmpty(hitState))
            return;

        _isPlayingHit = true;
        _skipHitCheckNextUpdate = true;

        // 最後一個參數傳 0f：確保 Boss 連續吃傷害時，受擊動畫會從頭重播，不會卡死在結尾
        animator.CrossFadeInFixedTime(hitState, transitionDuration, 0, 0f);
    }

    private void HandleDead()
    {
        if (_isDead || animator == null || string.IsNullOrEmpty(dieState))
            return;

        _isDead = true;
        _isPlayingHit = false;
        animator.CrossFadeInFixedTime(dieState, transitionDuration, 0, 0f);
    }

    private bool IsAnimationFinished(string stateName)
    {
        AnimatorStateInfo currentInfo = animator.GetCurrentAnimatorStateInfo(0);
        AnimatorStateInfo nextInfo = animator.GetNextAnimatorStateInfo(0);
        bool isInTransition = animator.IsInTransition(0);

        if (isInTransition && nextInfo.IsName(stateName))
            return false;

        if (currentInfo.IsName(stateName))
            return currentInfo.normalizedTime >= 0.95f;

        // 防卡死：若已不在該狀態也沒在過渡，直接判定結束
        return !isInTransition;
    }
}
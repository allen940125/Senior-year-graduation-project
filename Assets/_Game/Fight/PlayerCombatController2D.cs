using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

// 命名修正：明確標示這是「戰鬥時」且基於「2D 平面邏輯」的控制器
[RequireComponent(typeof(Rigidbody))]
public class PlayerCombatController2D : MonoBehaviour, IEntityRuntimeDependent
{
    private EntityRuntime _entityData;
    private StaminaTrait _staminaTrait;

    [Header("操控與硬核生存參數")]
    public float smoothTime = 0.08f;
    public float staminaRegenDelay = 0.8f;

    [Header("逃跑用速度 (若已改用 UPM Buff 系統，此欄可設為 1 或移除)")]
    public float MoveSpeedMultiplier = 1f;

    // [Dash 註解] private bool _isDashCooldown = false;
    private float _lastStaminaConsumeTime;
    
    private Vector3 _currentInput;
    private Vector3 _currentVelocity;
    // [Dash 註解] private Vector3 _dashDirection;
    
    // 在變數宣告區加入 Animator
    private Rigidbody _rb;
    private Animator _animator; // ★ 新增這行

    [Header("視覺設定")]
    [Tooltip("把你的 3D 模型或圖片物件 (Visual_Pivot) 拖進來")]
    public Transform visualPivot;
    
    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        
        // ★ 抓取子物件 (Visual_Pivot) 身上的 Animator
        _animator = GetComponentInChildren<Animator>(); 
        
        _rb.useGravity = false;
        _rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        _rb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;
    }

    // ==========================================
    // ★ 徹底解耦：刪除 Start，強制透過介面被動接收資料
    // ==========================================
    public void OnRuntimeDataChanged(EntityRuntime newData)
    {
        _entityData = newData;
        if (_entityData != null)
        {
            if (!_entityData.TryGetTrait(out _staminaTrait))
            {
                Debug.LogWarning($"[系統警告] {gameObject.name} 缺少 StaminaTrait，衝刺系統將被禁用！");
            }
        }
    }

    private bool CanMove()
    {
        if (_entityData == null) return false;
        // ★ 修改 1：改用 UPM 的 HasAnyState 檢查狀態標籤
        return // !_entityData.HasAnyState(EntityStateFlags.Dashing) && // [Dash 註解]
               !_entityData.HasAnyState(EntityStateFlags.Stunned) && 
               !_entityData.HasAnyState(EntityStateFlags.Dead);
    }

    // [Dash 註解] 整個 CanDash 方法先註解
    /*
    private bool CanDash() => CanMove() && 
                              !_isDashCooldown && 
                              _staminaTrait != null && 
                              _staminaTrait.currentStamina >= _staminaTrait.dashCost && 
                              _currentInput != Vector3.zero;
    */

    private void Update()
    {
        // ★ 修改 2：HasState 改為 HasAnyState
        if (_entityData == null || _entityData.HasAnyState(EntityStateFlags.Dead)) return;

        HandleStaminaRegen();
        HandleInput();
    }

    private void HandleStaminaRegen()
    {
        if (_staminaTrait == null) return; 

        if (// !_entityData.HasAnyState(EntityStateFlags.Dashing) && // [Dash 註解]
            _staminaTrait.currentStamina < _staminaTrait.maxStamina && 
            Time.time >= _lastStaminaConsumeTime + staminaRegenDelay)
        {
            // ★ 解耦修正：只管呼叫數據改變，刪除所有 GameManager 的廣播。
            // 讓 UI 去監聽 StaminaTrait.OnStaminaRatioChanged 即可。
            _staminaTrait.RegenStamina(20f, Time.deltaTime); 
        }
    }

    private void HandleInput()
    {
        if (Keyboard.current != null)
        {
            float x = 0; float y = 0;
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) y = 1;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) y = -1;
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) x = -1;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) x = 1;
            
            _currentInput = new Vector3(x, y, 0f).normalized;
        }

        // [Dash 註解] Shift 觸發衝刺
        /*
        if (Keyboard.current != null && Keyboard.current.shiftKey.wasPressedThisFrame && CanDash())
        {
            StartCoroutine(DashRoutine());
        }
        */
    }

    private void FixedUpdate()
    {
        // 防呆校正
        if (transform.position.z != 0f)
        {
            transform.position = new Vector3(transform.position.x, transform.position.y, 0f);
        }

        // [Dash 註解] 衝刺狀態的位移邏輯
        /*
        if (_entityData != null && _entityData.HasAnyState(EntityStateFlags.Dashing))
        {
            float dashSpd = _staminaTrait != null ? _staminaTrait.dashSpeed : 20f;
            _rb.linearVelocity = _dashDirection * dashSpd;
        }
        else 
        */
        if (CanMove())
        {
            // ★ 修改 3：直接讀取 UPM 的 MoveSpeed.Value（已包含 Blueprint.baseMoveSpeed 與所有速度 Buff）
            float runtimeMoveSpd = _entityData != null ? _entityData.MoveSpeed.Value : 5f;
            float finalMoveSpd = runtimeMoveSpd * MoveSpeedMultiplier; 
            
            Vector3 targetVelocity = _currentInput * finalMoveSpd;
            
            _rb.linearVelocity = Vector3.SmoothDamp(_rb.linearVelocity, targetVelocity, ref _currentVelocity, smoothTime);
        }
        else
        {
            _rb.linearVelocity = Vector3.zero;
        }
    }

    // [Dash 註解] 整個衝刺協程與 360 度旋轉表現（已預先對齊 UPM 的 AddStateWithCount）
    /*
    private IEnumerator DashRoutine()
    {
        _entityData.AddStateWithCount(EntityStateFlags.Dashing | EntityStateFlags.Invincible);
        
        _isDashCooldown = true;
        _lastStaminaConsumeTime = Time.time;

        if (_staminaTrait != null) 
            _staminaTrait.ConsumeStamina(_staminaTrait.dashCost);

        _dashDirection = _currentInput;
        float duration = _staminaTrait != null ? _staminaTrait.dashDuration : 0.2f;

        if (visualPivot != null)
        {
            float spinDirection = (_currentInput.x < 0) ? 1f : -1f;
            Quaternion startRotation = visualPivot.localRotation;
            float elapsed = 0f;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float percent = elapsed / duration;
                float currentAngle = Mathf.Lerp(0f, 360f * spinDirection, percent);
                visualPivot.localRotation = startRotation * Quaternion.Euler(0, 0, currentAngle);
                yield return null; 
            }
            
            visualPivot.localRotation = startRotation;
        }
        else
        {
            yield return new WaitForSeconds(duration);
        }

        _entityData.RemoveStateWithCount(EntityStateFlags.Dashing | EntityStateFlags.Invincible);
        _rb.linearVelocity = Vector3.zero;

        float cooldown = _staminaTrait != null ? _staminaTrait.dashCooldown : 0.5f;
        yield return new WaitForSeconds(cooldown);
        _isDashCooldown = false;
    }
    */
}
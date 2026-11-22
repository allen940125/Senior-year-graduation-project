using System.Collections.Generic;
using UnityEngine;

namespace GameFramework.Actors.TopDown2D
{
    [CreateAssetMenu(fileName = "DashAbility2D", menuName = "Abilities/Dash 2D")]
    public class DashAbility2DSO : AbilitySO
    {
        [Header("2D 衝刺物理設定")]
        public float dashSpeed = 20f;

        [Header("360度旋轉表現")]
        public bool enableSpin = true;

        // ★ 每個 Runner 獨立的執行期資料，避免 SO 共用導致資料汙染
        private class DashRuntimeState
        {
            public Vector3 Direction;
            public float Elapsed;
            public float SpinDirection;
            public Transform VisualPivot;
            public Quaternion StartRotation;
            public IEntityMotor Motor;
        }

        private readonly Dictionary<AbilityRunner, DashRuntimeState> _activeStates = new();

        private void Reset()
        {
            duration = 0.2f;
            cooldownTime = 0.5f;
            requiredStates = EntityStateFlags.None; // 2D Top-Down 沒有 Grounded
            blockStates = EntityStateFlags.Dead | EntityStateFlags.Stunned | EntityStateFlags.Rooted | EntityStateFlags.Dashing;
        }

        public override bool CanActivate(AbilityRunner runner)
        {
            if (!base.CanActivate(runner)) return false;

            // 還原舊邏輯：沒有方向輸入時不允許衝刺
            if (runner.TryGetComponent(out TopDownStateMachine sm))
            {
                if (sm.Context.MoveDirection.sqrMagnitude < 0.01f) return false;
            }

            return true;
        }

        public override void Activate(AbilityRunner runner)
        {
            var runtime = runner.Core.RuntimeData;

            // 1. 掛上狀態標籤 (無敵 + 衝刺)
            runtime.AddState(EntityStateFlags.Dashing);
            runtime.AddState(EntityStateFlags.Invincible);

            // 2. 取得輸入方向與視覺節點
            Vector3 dashDir = Vector3.right;
            Transform pivot = null;

            if (runner.TryGetComponent(out TopDownStateMachine sm))
            {
                dashDir = sm.Context.MoveDirection.normalized;
                pivot = sm.visualPivot;
            }

            runner.TryGetComponent(out IEntityMotor motor);

            // 3. 記錄此實例的執行期狀態
            var state = new DashRuntimeState
            {
                Direction = dashDir,
                Elapsed = 0f,
                SpinDirection = dashDir.x < 0f ? 1f : -1f,
                VisualPivot = pivot,
                StartRotation = pivot != null ? pivot.localRotation : Quaternion.identity,
                Motor = motor
            };
            _activeStates[runner] = state;

            // 4. 覆寫初始衝刺速度
            motor?.OverwriteVelocity(dashDir * dashSpeed);
        }

        public override void OnAbilityTick(AbilityRunner runner)
        {
            if (!_activeStates.TryGetValue(runner, out var state)) return;

            state.Elapsed += Time.deltaTime;

            // 維持恆定衝刺速度 (對應舊版 FixedUpdate 裡強制鎖定 linearVelocity)
            state.Motor?.OverwriteVelocity(state.Direction * dashSpeed);

            // 執行 360 度 Z 軸旋轉
            if (enableSpin && state.VisualPivot != null && duration > 0f)
            {
                float percent = Mathf.Clamp01(state.Elapsed / duration);
                float currentAngle = Mathf.Lerp(0f, 360f * state.SpinDirection, percent);
                state.VisualPivot.localRotation = state.StartRotation * Quaternion.Euler(0f, 0f, currentAngle);
            }
        }

        public override void EndAbility(AbilityRunner runner)
        {
            // 1. 拔除狀態標籤
            runner.Core.RuntimeData.RemoveState(EntityStateFlags.Dashing);
            runner.Core.RuntimeData.RemoveState(EntityStateFlags.Invincible);

            if (_activeStates.TryGetValue(runner, out var state))
            {
                // 2. 還原視覺旋轉角度，防止浮點誤差歪斜
                if (state.VisualPivot != null)
                {
                    state.VisualPivot.localRotation = state.StartRotation;
                }

                // 3. 衝刺結束立刻煞停 (對應舊版 _rb.linearVelocity = Vector3.zero)
                state.Motor?.StopImmediately();

                // 4. 清理執行期快取
                _activeStates.Remove(runner);
            }
        }
    }
}
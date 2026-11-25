using UnityEngine;

namespace GameFramework.Actors.TopDown2D
{
    [CreateAssetMenu(fileName = "TopDownMovementSO", menuName = "Data/Characters/Player/TopDownMovementSO")]
    public class TopDownMovementSO : ScriptableObject
    {
        [Header("物理環境 (務必設定 GravityMultiplier=0, EnableHover=false, ControlVerticalAxis=true)")]
        public MotorMovementProfile TopDownProfile;

        [Header("操作倍率")]
        [Range(1f, 3f)] public float SprintMultiplier = 1.5f;

        [Header("動畫名稱")]
        public string IdleAnim = "Idle";
        public string MoveAnim = "Run";
    }
}
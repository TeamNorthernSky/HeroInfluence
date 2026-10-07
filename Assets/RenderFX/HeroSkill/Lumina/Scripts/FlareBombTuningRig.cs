using UnityEngine;
namespace JC.VFX
{
    /// <summary>에디터 전용 튜닝 참조. 전투 실행이나 스킬 데이터 교체를 하지 않습니다.</summary>
    public sealed class FlareBombTuningRig : MonoBehaviour
    {
        public FlareVolumePreset normal;
        public FlareVolumePreset dark;
        public GameObject normalOrb, darkOrb, normalImpact, darkImpact;
        [Tooltip("프리뷰용 거리입니다. 실제 전투 사거리·속도는 변경하지 않습니다.")]
        [Range(1,12)] public float previewDistance=5;
    }
}

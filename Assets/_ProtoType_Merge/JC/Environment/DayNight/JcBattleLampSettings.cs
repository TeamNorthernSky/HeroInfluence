using UnityEngine;

namespace JC.Environment
{
    // JC | 2026-11-04 | 시간대별 전장 조명을 조절하고 가로등의 중심광과 주변광을 배치에 맞게 적용합니다.
    public sealed class JcBattleLampSettings : MonoBehaviour
    {
        public JcBattleLampProfile Source { get; private set; }
        public JcBattleLampProfile Draft { get; private set; }
        public int Segment { get; private set; }
        public int Revision { get; private set; }

        void Awake()
        {
            Source = Resources.Load<JcBattleLampProfile>(JcBattleLampProfile.ResourceName);
            if (!Source) return;
            Draft = Instantiate(Source);
            Draft.hideFlags = HideFlags.DontSave;
        }

        internal void Select(int index) => Segment = index;
        public void NotifyChanged() => ++Revision;

        void OnDestroy()
        {
            if (Draft) Destroy(Draft);
        }
    }
}

using UnityEngine;

namespace JC.Environment
{
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

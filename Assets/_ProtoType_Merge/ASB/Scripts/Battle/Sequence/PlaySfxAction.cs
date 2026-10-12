using System.Collections;
using UnityEngine;

namespace ASB.Work.Battle.Sequence
{
    /// <summary>
    /// 효과음을 1회 재생하고 바로 다음 Action으로 넘어갑니다(재생 완료를 기다리지 않음).
    /// </summary>
    public class PlaySfxAction : BattleSequenceAction
    {
        private readonly string _sfxKey;

        public PlaySfxAction(string sfxKey)
        {
            _sfxKey = DHAudioClipCatalog.NormalizeKey(sfxKey);
        }

        public string SfxKey => _sfxKey;

        public override IEnumerator ExecuteRoutine(MonoBehaviour host)
        {
            if (!string.IsNullOrEmpty(_sfxKey))
            {
                PresentationSfxPlayer.Play(_sfxKey);
            }
            yield break;
        }
    }
}

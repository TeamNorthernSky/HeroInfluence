using UnityEngine;

namespace JC.VFX.Seam
{
    /// <summary>
    /// 프리뷰 리셋 위생 관리 — 고아 시퀀스를 정리한다.
    ///
    /// 문제:
    /// SkillPresentationPreviewController.ResetPreview()는 자기 코루틴(_playRoutine)만 StopCoroutine 한다.
    /// 그런데 BattleManager.RunSkillSequenceCore는 내부에서 StartCoroutine(runner.RunAll(this))로
    /// **BattleManager 위에 별도 코루틴**을 띄운다. 리셋해도 이 시퀀스는 계속 돌면서
    /// 공유 PresentationRuntimeContext에 SetupPresentationContext를 호출한다.
    ///
    /// 그 상태에서 다음 스킬을 재생하면, 살아남은 이전 시퀀스가 옛 actionInstanceId로 SetActive를 불러
    /// StopAllHandles()가 돌고 Cue 맵이 교체된다 → 새 실행의 트레일이 죽거나 등록되지 않는다.
    /// (스킬 종료 후 몇 초 기다렸다 리셋하면 고아가 자연 종료하므로 증상이 안 나타난다.)
    ///
    /// 대응: 프리뷰 실행이 끝나는 순간(IsPlaying true→false)에 BattleManager의 코루틴을 정리하고
    /// 연출 컨텍스트를 비운다. 정상 종료 시에는 이미 끝난 상태라 무해하고,
    /// 조기 리셋 시에는 고아를 확실히 끊는다.
    ///
    /// ASB 코드는 수정하지 않는다 — 전부 공개 API(StopAllCoroutines / Clear)만 사용.
    /// </summary>
    public class PreviewResetGuard : MonoBehaviour
    {
        [Tooltip("프리뷰 종료 시 BattleManager에 남은 코루틴(고아 시퀀스)을 정리한다.")]
        [SerializeField] private bool stopOrphanCoroutines = true;

        [Tooltip("프리뷰 종료 시 유닛의 연출 컨텍스트와 Held 이펙트를 비운다.")]
        [SerializeField] private bool clearPresentationContext = true;

        [SerializeField] private bool logResult = true;

        private SkillPresentationPreviewController preview;
        private BattleManager battleManager;
        private bool wasPlaying;

        private void Update()
        {
            if (preview == null)
            {
                preview = FindFirstObjectByType<SkillPresentationPreviewController>();
                if (preview == null)
                {
                    return;
                }
            }

            bool nowPlaying = preview.IsPlaying;
            if (wasPlaying && !nowPlaying)
            {
                CleanUp();
            }

            wasPlaying = nowPlaying;
        }

        /// <summary>남은 시퀀스와 연출 컨텍스트를 정리한다. 수동 호출도 가능.</summary>
        public void CleanUp()
        {
            int cleared = 0;

            if (stopOrphanCoroutines)
            {
                if (battleManager == null)
                {
                    battleManager = FindFirstObjectByType<BattleManager>();
                }

                if (battleManager != null)
                {
                    battleManager.StopAllCoroutines();
                }
            }

            if (clearPresentationContext)
            {
                BattleCharactor[] units = FindObjectsByType<BattleCharactor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                for (int i = 0; i < units.Length; i++)
                {
                    PresentationRuntimeContext ctx = units[i] != null ? units[i].GetComponent<PresentationRuntimeContext>() : null;
                    if (ctx == null)
                    {
                        continue;
                    }

                    ctx.Clear();
                    cleared++;
                }
            }

            if (logResult)
            {
                Debug.Log($"[PreviewResetGuard] 시퀀스 정리 — 코루틴 중단={stopOrphanCoroutines}, 컨텍스트 비움={cleared}유닛.", this);
            }
        }
    }
}

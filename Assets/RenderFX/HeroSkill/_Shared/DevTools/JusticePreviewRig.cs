using System.Collections;
using UnityEngine;

namespace JC.VFX.Seam
{
    /// <summary>
    /// 저스티스 VFX 테스트 씬(z_JC_PreViewsScene_2) 전용 리그.
    ///
    /// ASB 자산을 일절 수정하지 않기 위한 씬 로컬 우회 장치다.
    /// 유닛 프리팹(Unit_Fighter_10001)의 Animator에는 ASB의 Fighter_Ani_Controller가 직접 물려 있고,
    /// 그 클립들에는 연출 Cue 이벤트(AniEvent_PresentationCue)가 하나도 없다.
    /// 원본에 이벤트를 심으면 ASB 영역을 건드리게 되므로,
    /// 클립 복제본 + JC 오버라이드 컨트롤러를 만들어 두고 런타임에 컨트롤러만 갈아끼운다.
    ///
    /// 즉 디스크상의 ASB 에셋은 그대로이고, 이 씬에서 스폰된 인스턴스만 JC 컨트롤러를 쓴다.
    /// </summary>
    public class JusticePreviewRig : MonoBehaviour
    {
        [Header("Swap Target")]
        [Tooltip("교체 대상 컨트롤러 이름(스폰된 유닛의 Animator에 물려 있는 ASB 원본).")]
        [SerializeField] private string sourceControllerName = "Fighter_Ani_Controller";

        [Tooltip("대신 물릴 JC 오버라이드 컨트롤러(Cue 이벤트가 심긴 클립 포함).")]
        [SerializeField] private RuntimeAnimatorController jcController;

        [Header("Timing")]
        [Tooltip("PreviewBattleSceneManager의 스폰이 끝난 뒤 교체해야 하므로 몇 프레임 기다린다.")]
        [SerializeField, Min(1)] private int waitFrames = 2;

        [SerializeField] private bool logResult = true;

        private void Awake()
        {
            Debug.Log("[JusticePreviewRig] Awake — 리그 살아있음.", this);
        }

        private IEnumerator Start()
        {
            for (int i = 0; i < waitFrames; i++)
            {
                yield return null;
            }

            Debug.Log("[JusticePreviewRig] 교체 시도 시작.", this);

            if (jcController == null)
            {
                Debug.LogWarning("[JusticePreviewRig] jcController가 비어 있어 교체를 건너뜁니다.", this);
                yield break;
            }

            int swapped = 0;
            Animator[] animators = FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < animators.Length; i++)
            {
                Animator animator = animators[i];
                RuntimeAnimatorController current = animator != null ? animator.runtimeAnimatorController : null;
                if (current == null || current.name != sourceControllerName)
                {
                    continue;
                }

                animator.runtimeAnimatorController = jcController;
                swapped++;
            }

            if (logResult)
            {
                Debug.Log($"[JusticePreviewRig] '{sourceControllerName}' → '{jcController.name}' 교체 {swapped}건.", this);
            }
        }
    }
}

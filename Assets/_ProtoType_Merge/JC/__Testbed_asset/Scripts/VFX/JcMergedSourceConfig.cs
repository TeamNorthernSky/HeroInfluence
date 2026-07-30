using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// 병합 에셋의 재료·산출물 지정.
    ///
    /// 실제 전투씬은 연출 카탈로그와 이펙트 레지스트리를 **ASB 것 하나씩만** 참조한다.
    /// JC 이펙트를 실전투에 태우려면 「ASB 전부 + JC 것」을 담은 병합본이 필요하다.
    /// 병합본은 스냅샷이라 ASB가 스킬·이펙트를 추가하면 낡는다 —
    /// 그래서 한 번 만들고 마는 게 아니라 **언제든 다시 만들 수 있게** 재료를 데이터로 들고 있는다.
    /// (경로를 코드에 상수로 박으면 대상이 늘어날 때 손댈 수 없다 — 프리셋 체계와 같은 이유다.)
    ///
    /// 재생성: 메뉴 `JC VFX/병합 에셋 재생성`.
    /// </summary>
    [CreateAssetMenu(menuName = "JC VFX/Merged Source Config (병합 재료 지정)", fileName = "JC_MergeSources")]
    public class JcMergedSourceConfig : ScriptableObject
    {
        [Header("── 재료: ASB (기반) ──")]
        [Tooltip("실전투씬이 현재 참조하는 ASB 연출 카탈로그. 이쪽 바인딩이 먼저 깔린다.")]
        public SkillPresentationCatalog asbCatalog;
        [Tooltip("실전투씬이 현재 참조하는 ASB 이펙트 레지스트리.")]
        public EffectRegistry asbRegistry;

        [Header("── 재료: JC (덮어쓰기) ──")]
        [Tooltip("JC 연출 카탈로그. 같은 스킬 인덱스가 있으면 ASB 것을 밀어내고 이쪽이 이긴다.")]
        public SkillPresentationCatalog jcCatalog;
        [Tooltip("JC 이펙트 레지스트리. 같은 id가 있으면 이쪽이 이긴다(현재 id 대역이 겹치지 않아 실제로는 단순 합집합).")]
        public EffectRegistry jcRegistry;

        [Header("── 산출물 (기존 에셋에 덮어쓴다 — GUID 보존) ──")]
        [Tooltip("병합 카탈로그. 씬이 이 에셋을 참조하게 되므로 재생성 시에도 같은 파일에 써야 한다.")]
        public SkillPresentationCatalog mergedCatalog;
        [Tooltip("병합 레지스트리.")]
        public EffectRegistry mergedRegistry;

        [Header("── 낡음 감지 (자동 기록 — 손으로 고치지 말 것) ──")]
        [Tooltip("마지막 병합 시점의 재료 구성 지문. 재료가 바뀌면 이 값과 어긋나 경고가 뜬다.\n" +
                 "★병합본은 스냅샷이라, ASB가 스킬·이펙트를 추가해도 재생성 전까지는 게임에 나오지 않는다.\n" +
                 "그 상황이 조용히 지나가면 원인을 찾기 어려워서 지문을 남긴다.")]
        public string sourceFingerprint;
        [Tooltip("마지막 병합 시각(기록용).")]
        public string lastMergedAt;
        [Tooltip("끄면 플레이 진입 시 낡음 검사를 하지 않는다.")]
        public bool warnWhenStale = true;
    }
}

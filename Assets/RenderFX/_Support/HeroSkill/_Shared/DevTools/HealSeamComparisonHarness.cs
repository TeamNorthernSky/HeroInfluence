using System.Collections;
using UnityEngine;

namespace JC.VFX.Seam
{
    /// <summary>
    /// 비교 진입점: 키 1 = Version A(현-규약 대조군), 키 2 = Version B(확장 seam).
    /// 동일 시전자→타깃·동일 애니/타이밍 소스로 발사해 유일 변수를 seam 설계로 고정.
    /// 완료 시 각 버전이 실어 나른 단계 매트릭스를 콘솔에 출력.
    /// </summary>
    public class HealSeamComparisonHarness : MonoBehaviour
    {
        [SerializeField] private AsbContractHealDriver contractDriver; // Version A
        [SerializeField] private AsbSeamHealDriver seamDriver;         // Version B
        [SerializeField] private KeyCode versionAKey = KeyCode.Alpha1;
        [SerializeField] private KeyCode versionBKey = KeyCode.Alpha2;

        private bool _running;

        private void Update()
        {
            if (_running) return;
            if (Input.GetKeyDown(versionAKey) && contractDriver != null)
                StartCoroutine(RunAndReport("Version A (현-규약 분해)", contractDriver.Run(), () => contractDriver.RenderedStages));
            else if (Input.GetKeyDown(versionBKey) && seamDriver != null)
                StartCoroutine(RunAndReport("Version B (확장 seam)", seamDriver.Run(), () => seamDriver.RenderedStages));
        }

        private IEnumerator RunAndReport(string label, IEnumerator routine, System.Func<HealStageFlags> stages)
        {
            _running = true;
            yield return StartCoroutine(routine);
            HealStageFlags s = stages();
            Debug.Log($"[HealSeam] {label} 렌더 단계 = [ " +
                      $"차징:{Mark(s, HealStageFlags.Charge)} " +
                      $"발사:{Mark(s, HealStageFlags.Launch)} " +
                      $"궤도:{Mark(s, HealStageFlags.Orbit)} " +
                      $"힐:{Mark(s, HealStageFlags.Heal)} ]");
            _running = false;
        }

        private static string Mark(HealStageFlags s, HealStageFlags f) => (s & f) != 0 ? "O" : "X";
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class BattleResultRewardVisualSettings : MonoBehaviour
{
    [Header("경험치 획득")]
    [Tooltip("레벨별 충전 구간 하나의 시간과 곡선입니다. 실제 초이며 획득량·전투 배속과 무관합니다. 0이면 즉시 완료합니다. 결과창은 일시정지 배율과 무관하게 재생합니다.")]
    public BattleUnitBarVisualSettings.LossMotion expGain = new BattleUnitBarVisualSettings.LossMotion(0.5f);
    [Tooltip("추가되는 EXP 구간의 시간별 색입니다. 완료하면 기존 EXP 이미지 색으로 돌아갑니다.")]
    public Gradient expGainColors = BattleUnitBarVisualSettings.CreateRecoveryGradient(new Color(1f, 1f, 0.65f));
    [Header("랭크 상승")]
    [Tooltip("이전 아이콘→백색→새 아이콘의 총 시간(초)입니다. 앞뒤 절반씩 재생하며 캐릭터당 결과 표시 1회입니다. 0이면 즉시 새 아이콘으로 바뀝니다.")]
    [Min(0f)] public float rankFlashDuration = 0.5f;
    [Tooltip("아이콘 알파를 보존하고 RGB를 백색/지정색으로 바꾸는 UI 재질입니다. 카드별 복제본을 사용하므로 다른 UI에 영향을 주지 않습니다.")]
    public Material tintMaterial;

    private static readonly List<BattleResultRewardVisualSettings> Active = new List<BattleResultRewardVisualSettings>();
    private void OnEnable() { if (!Active.Contains(this)) Active.Add(this); }
    private void OnDisable() => Active.Remove(this);
    public static BattleResultRewardVisualSettings ForScene(Scene scene)
    {
        for (int i = Active.Count - 1; i >= 0; i--)
        {
            var s = Active[i];
            if (s == null) { Active.RemoveAt(i); continue; }
            if (s.isActiveAndEnabled && s.gameObject.scene == scene) return s;
        }
        return null;
    }
}

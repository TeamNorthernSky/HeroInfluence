using System;
using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// 클립 이벤트 대행 표 — 「어느 애니 상태의 몇 초에 어떤 Cue 이름을 부를 것인가」.
    ///
    /// ★임시 장치다. 원래 이 일은 애니메이션 클립에 심긴 `AniEvent_PresentationCue` 이벤트가 한다.
    ///   파이터 계열 클립에는 그 이벤트가 아직 없어서(서포터 4종에만 존재) JC가 대신 부르는 것뿐이고,
    ///   클립에 진짜 이벤트가 생기면 <see cref="Seam.JcCueTimingDriver"/>가 스스로 비켜선다.
    ///   그때 이 표는 지워도 되고, 남겨 둬도 아무 일도 하지 않는다.
    ///
    /// 시각은 클립 이벤트와 같은 **초 단위**다(정규화 비율 아님). 애니 이벤트를 찍을 때 쓰는 값과
    /// 그대로 호환되므로, 나중에 클립에 옮겨 심을 때 숫자를 변환할 필요가 없다.
    /// </summary>
    [CreateAssetMenu(menuName = "JC VFX/Cue Timing Table (클립 이벤트 대행 표)", fileName = "JC_CueTiming")]
    public class JcCueTimingTable : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            [Tooltip("끄면 이 줄만 건너뛴다.")]
            public bool enabled = true;

            [Tooltip("애니메이터 상태의 전체 경로. 예: Base Layer.ClassSkill_3\n" +
                     "★연출 데이터(SkillPresentationData)의 AnimationStateName과 글자 그대로 같아야 한다.")]
            public string stateName;

            [Tooltip("부를 Cue 이름. 예: impact / fist_trail_on / fist_trail_off\n" +
                     "★연출 데이터의 CueName과 같아야 한다. 표에만 있고 데이터에 없는 이름은 불러도 무해하다\n" +
                     "(등록되지 않은 Cue는 프리젠터가 조용히 무시한다).")]
            public string cueName;

            [Tooltip("상태 시작으로부터 몇 초 뒤에 부를지. 클립 이벤트의 time과 같은 단위.")]
            [Min(0f)] public float time;

            [Tooltip("메모용. 동작에는 영향 없음.")]
            public string note;
        }

        [Header("── 발화 표 ──")]
        [Tooltip("한 상태에 여러 Cue를 넣어도 된다. 순서는 상관없다(각 줄이 자기 시각에 독립적으로 발화).")]
        public Entry[] entries = new Entry[0];

        [Header("── 진단 ──")]
        [Tooltip("발화할 때마다 콘솔에 남긴다. 타이밍을 맞출 때만 켠다.")]
        public bool logFire;

        [Tooltip("표에 있는 상태가 한 번도 등장하지 않으면 전투 종료 시 경고한다(상태 이름 오타 탐지).")]
        public bool warnUnusedStates = true;
    }
}

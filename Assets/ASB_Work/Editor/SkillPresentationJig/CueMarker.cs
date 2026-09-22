using UnityEngine;
using UnityEngine.Timeline;

namespace ASB.Work.EditorTools.Jig
{
    /// <summary>
    /// Timeline 편집 지그 전용 Cue 위치 표시 마커.
    ///
    /// <b>발화하지 않는다.</b> <see cref="UnityEngine.Playables.INotification"/>을 구현하지 않는 이유가 그것이다 —
    /// 이펙트는 EffectRegistry 해석과 SetupPresentationContext가 만든 런타임 컨텍스트가 있어야 뜨고,
    /// 에디터 스크러빙 중에는 그 근거가 없다. 이 마커는 "위치를 보고 드래그하기 위한 표시물"이다.
    ///
    /// <b>에디터 어셈블리에 둔다.</b> 이 폴더에는 asmdef가 없어 Assembly-CSharp-Editor로 컴파일되므로
    /// 플레이어 빌드에 코드가 들어가지 않는다. 지그가 만드는 TimelineAsset도 Editor/ 아래에 두므로
    /// 런타임에서 이 타입을 역직렬화할 일이 없다.
    /// </summary>
    public class CueMarker : Marker
    {
        [Tooltip("역기입 대상 CueBinding을 특정하는 유일한 키. CueName과 리스트 인덱스는 중복·순서변경에 취약하다.")]
        public string CueId;

        [Tooltip("표시용. 실제 매칭에는 쓰지 않는다.")]
        public string CueName;

        [Tooltip("표시용. 예: \"Attack.Beats[0]\"")]
        public string PhaseLabel;

        [Tooltip("클립 이벤트에서 읽어온 마커. 드래그해도 역기입되지 않는다(클립 안에 시각이 있으므로).")]
        public bool FromClipEvent;

        // ── 생성 시점 스냅샷 ──
        // 역기입에서 phaseStart/길이를 다시 계산하지 않는다. 생성 이후 컨트롤러나 자산이 바뀌면
        // 재계산 결과가 생성 당시와 달라져 조용히 틀린 값이 들어간다.

        [Tooltip("이 Cue가 속한 페이즈 구간이 트랙에서 시작하는 시각(초).")]
        public double PhaseStart;

        [Tooltip("그 state의 실효 길이(초). NormalizedTime ↔ 초 변환의 분모.")]
        public double EffectiveLength;

        [Tooltip("생성 당시의 CueTimingSource(int). 역기입 시 값이 달라졌으면 건너뛴다.")]
        public int TimingSource;
    }
}

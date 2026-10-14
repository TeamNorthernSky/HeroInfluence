using System;
using System.Linq;
using UnityEngine;

namespace JC.BattleTesting
{
    [CreateAssetMenu(menuName = "JC/전투/공통 뷰 설정")]
    public sealed class JcBattlePresentationProfile : ScriptableObject
    {
        [Serializable]
        public sealed class EventView
        {
            [Tooltip("MainEventObject/SubEventObject의 EventKey입니다. 구역과 함께 일치하는 이벤트에 적용합니다.")]
            public string eventKey;
            [Min(1), Tooltip("이벤트가 속한 구역 번호입니다.")]
            public int zoneId = 1;
            [Tooltip("끄면 이벤트 오브젝트 위치를 사용합니다. 켜면 아래 월드 좌표를 기준으로 주변 공간을 탐색합니다.")]
            public bool overrideAnchor;
            [Tooltip("탐사씬 월드 좌표 기준의 전투 배경 기준점입니다.")]
            public Vector3 anchor;
            [Tooltip("Auto는 도로 형태에 맞춰 일정한 방향을 선택합니다. X/Z는 해당 축으로만 진형 공간을 탐색합니다.")]
            public JcBattleRoadAxis axis;
        }

        [Tooltip("고정 이벤트별 배경 기준점·방향 예외 설정입니다. 항목이 없어도 이벤트 위치를 기준으로 자동 구성됩니다.")]
        public EventView[] events = Array.Empty<EventView>();
        [Tooltip("일반 유닛과 공유하는 신규 HP바 프리팹입니다. 인질에게는 HP 행만 표시합니다.")]
        public GameObject unitBarPrefab;
        [Range(.5f, 1f), Tooltip("일반 유닛 대비 인질 HP바의 화면상 길이입니다.")]
        public float hostageWidthScale = .85f;
        [Range(.5f, 1f), Tooltip("일반 유닛 대비 인질 HP바의 화면상 두께입니다.")]
        public float hostageThicknessScale = .85f;
        [Tooltip("튜토리얼 강조색과 같은 인질 HP바 테두리 색상입니다.")]
        public Color hostageBorder = new Color(25f / 255f, 1f, 84f / 255f, 1f);

        public EventView Find(int zone, string key) => events?.FirstOrDefault(e =>
            e != null && e.zoneId == zone && string.Equals(e.eventKey, key, StringComparison.Ordinal));

        public static JcBattlePresentationProfile Load() =>
            Resources.Load<JcBattlePresentationProfile>("JC_BattlePresentation_Default");
    }
}

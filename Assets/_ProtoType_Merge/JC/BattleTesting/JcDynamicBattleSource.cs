using System;
using UnityEngine;
namespace JC.BattleTesting
{
    // DH 카탈로그 대체본이 아니라, 독립 테스트씬에 전달할 공통 외형 스냅샷입니다.
    public sealed class JcDynamicBattleSource : ScriptableObject
    {
        [Serializable] public sealed class Part
        {
            public string name, group;
            public Mesh mesh;
            public Mesh originalMesh;
            public Material[] materials;
            public Matrix4x4 matrix;
            public Bounds bounds;
            public bool terrain;
            [NonSerialized] public MeshRenderer renderer;
        }
        [Serializable] public struct Road { public Vector3 center; public float size; }
        [Tooltip("탐사맵 전체에서 읽은 정적 외형입니다. 위치별 전투 배경은 포함하지 않습니다.")]
        public Part[] parts = Array.Empty<Part>();
        [Tooltip("도로 방향과 원본 폭을 측정하기 위한 위치 정보입니다. 전투 셀로 사용하지 않습니다.")]
        public Road[] roads = Array.Empty<Road>();
        [Tooltip("도로 표면의 원본 재질입니다.")]
        public Material roadMaterial;
        [Tooltip("입력 외형을 수집한 씬입니다.")]
        public string sourceScene;
    }
    public enum JcBattleRoadAxis { Auto, X, Z }
    [Serializable]
    public sealed class JcDynamicBattleSettings
    {
        [Tooltip("Auto는 도로 형태에 맞춰 선택합니다. X/Z는 지정 축으로만 배치를 검사합니다.")]
        public JcBattleRoadAxis axis;
        [Tooltip("실제 탐사씬이 없을 때 사용할 맵 전체 공통 외형 입력입니다.")]
        public JcDynamicBattleSource source;
        [Tooltip("조우 당시 플레이어의 탐사 월드 위치입니다. 후보 이외 좌표도 입력할 수 있습니다.")]
        public Vector3 encounterPosition = new Vector3(8, .02f, 7);
        [Tooltip("조우 당시 적의 탐사 월드 위치입니다. 교차로 방향이 동률일 때 사용합니다.")]
        public Vector3 enemyPosition = new Vector3(9, .02f, 7);
        [Min(5),Tooltip("탐사 외형 수집 반경입니다.")]
        public float captureRadius = 18;
        [Range(1,8),Tooltip("탐사 배경을 전투 크기로 확대하는 배율입니다.")]
        public float scale = 3;
        [Range(0,20),Tooltip("조우 위치 주변에서 실제 도로 형태를 유지하며 진형 기준점을 찾는 최대 거리입니다.")]
        public float anchorSearchRadius = 8;
        [Range(1,8),Tooltip("도로 폭이 부족하면 주변 외형 전체를 동일 비율로 확대합니다. 이 값 이상으로 늘리지 않습니다.")]
        public float maxAutoScale = 8;
        [Min(10),Tooltip("원본 도로에서 확보할 전투 진형의 최소 폭입니다.")]
        public float battleWidth = 11.5f;
        [Min(20),Tooltip("원본 도로에서 확보할 전투 진형과 이동 공간의 최소 길이입니다.")]
        public float battleLength = 36;
        [HideInInspector] // 이전 설정의 직렬화 호환용입니다. 원본 외형을 밀어내는 기능은 제거했습니다.
        public float clearance = 3;
    }
}

using UnityEngine;
namespace JC.EnvironmentDetail
{
    [CreateAssetMenu(menuName="JC/환경/탐사 외형 보강 설정")]
    public sealed class JcExploreDetailProfile : ScriptableObject
    {
        [Tooltip("다음 씬 적용 시 베이크 표시를 사용할지 정합니다. 플레이 중 수치 변경은 자동 반영하지 않습니다. 이동·충돌·이벤트 판정은 항상 원본입니다.")]
        public bool enableDetails=true;
        [Tooltip("원본이 달라져도 마지막 확정 베이크를 강제 표시합니다. 새 맵과 겹칠 수 있습니다. 실행 시작 시 적용되며 외형 수치는 재베이크 전까지 반영되지 않습니다.")]
        public bool forceBakedOnMismatch;
        [Tooltip("자동 적용할 테스트베드 탐사씬 이름입니다. 전투씬에는 적용하지 않습니다.")]
        public string[] sceneNames={"DHScene_3"};
        [Tooltip("도로 재질에 미세한 입자와 노멀을 추가합니다. 기존 차선·횡단보도 이미지는 유지합니다.")]
        public bool roadSurface=true;
        [Tooltip("탐사 횡단보도 표시만 흰색으로 바꿉니다. 탐사 중앙선의 색과 비율은 유지합니다.")]
        public bool whiteCrosswalks=true;
        [Tooltip("보도 외곽만 둥글게 만듭니다. 내부 줄눈과 원본 텍스처는 유지합니다.")]
        public bool roundSidewalk=true;
        [Tooltip("보도와 도로가 만나는 경계에 밝은 연속 연석을 표시합니다.")]
        public bool sidewalkCurb=true;
        [Tooltip("잔디와 도로가 만나는 경계에 낮은 연석을 표시합니다. 물가에는 적용하지 않습니다.")]
        public bool roadsideCurb=true;
        [Tooltip("DH 도로 텍스처 이름 접두사입니다. 이 목록에 없는 지면은 도로로 취급하지 않습니다.")]
        public string roadTexturePrefix="Road_";
        [Tooltip("원본 보도 텍스처 이름입니다. RaisedCube 형식만 보강합니다.")]
        public string sidewalkTexture="tilemap_01";
        [Tooltip("야지 경계로 인식할 원본 텍스처 이름입니다.")]
        public string grassTexture="tilemap_04";
        [Range(.12f,.45f),Tooltip("도로와 만나는 보도 블록의 평면상 코너 반경입니다. 연석과 보도를 함께 둥글게 재구성합니다.")]
        public float cornerRadius=.28f;
        [Tooltip("탐사 연석에만 곱할 밝은 회색입니다. 공용 전투 연석 재질은 변경하지 않습니다.")]
        public Color curbTint=new Color(.72f,.72f,.72f,1);
        [Range(.002f,.02f),Tooltip("연석-보도 경계와 연석 블록 사이 줄눈의 폭입니다. 면을 겹치지 않고 나눠 표현합니다.")]
        public float jointWidth=.008f;
        [Range(.25f,1f),Tooltip("직선 연석 블록의 대략적인 길이입니다. 코너는 곡선 블록으로 연결합니다.")]
        public float curbBlockLength=.5f;
        [Tooltip("연석 줄눈의 색상입니다.")]
        public Color jointTint=new Color(.46f,.46f,.46f,1);
        [Range(.04f,.2f),Tooltip("연석 폭입니다. 탐사 1셀 크기를 기준으로 조절합니다.")]
        public float curbWidth=.1f;
        [Range(0,.025f),Tooltip("연석 윗모서리를 한 면으로 깎는 폭입니다. 낮은 연석은 높이에 맞춰 자동 축소합니다.")]
        public float curbBevel=.012f;
        [Range(.02f,.2f),Tooltip("야지 쪽 연석이 노면 위로 올라오는 높이입니다.")]
        public float roadsideHeight=.08f;
        [Range(.005f,.03f),Tooltip("도로와 풀밭 높이 차이가 거의 없을 때 사용하는 낮은 연석 높이입니다.")]
        public float flatGrassCurbHeight=.012f;
        [Range(.005f,.08f),Tooltip("도로-풀밭 높이 차이가 이 값 이하면 평탄 경계로 처리합니다.")]
        public float flatBoundaryTolerance=.03f;
        [Header("도로 · 풀밭 연결")]
        [Tooltip("도로와 풀밭 사이에 얇은 흙띠와 드문 풀을 표시하고 흙길 입구를 연결합니다.")]
        public bool naturalRoadEdges=true;
        [Range(.02f,.25f),Tooltip("연석 뒤 풀밭에 놓을 흙띠의 폭입니다.")]
        public float soilEdgeWidth=.09f;
        [Range(.02f,.3f),Tooltip("도로에서 흙길로 전이할 길이입니다. 연석 폭에서 도로색 유지 구간을 뺀 범위 안으로 제한합니다.")]
        public float pathApronLength=.14f;
        [HideInInspector,Tooltip("이전 프로필 호환용 값입니다. 진입로 색상은 현재 일반 연석 색상을 따릅니다.")]
        public Color entrancePavingTint=new Color(.59f,.59f,.59f,1);
        [Range(.1f,.8f),Tooltip("연석 폭 중 도로 재질을 그대로 유지할 비율입니다. 나머지 구간에서 흙길로 바뀝니다.")]
        public float entranceRoadFraction=.5f;
        [Range(.012f,.06f),Tooltip("진입로 양끝 표시 블록의 두께입니다. 입구 폭의 20% 이내로 제한합니다.")]
        public float entranceMarkerWidth=.03f;
        [HideInInspector,Tooltip("이전 프로필 호환용 값입니다. 진입로 높이는 현재 낮은 연석 높이를 따릅니다.")]
        public float entranceMarkerHeight=.024f;
        [Tooltip("경계 흙띠의 색상입니다.")]
        public Color soilEdgeTint=new Color(.52f,.46f,.32f,1);
        [Tooltip("작은 풀의 색상입니다.")]
        public Color edgeGrassTint=new Color(.43f,.51f,.27f,1);
        [Tooltip("도로 경계 보강용 셰이더입니다. 빌드에서도 유지하도록 참조합니다.")]
        public Shader roadEdgeShader;
        [Tooltip("원본 흙길 이미지의 가장자리에서 읽은 입구 범위입니다. 원본 이미지 변경 시 경계 분석 버튼으로 갱신합니다. -1은 흙길 없음입니다.")]
        public JcPathEdgeRule[] pathEdgeRules=new JcPathEdgeRule[0];
        [Range(0,1),Tooltip("도로의 미세 요철 강도입니다.")]
        public float roadNormalStrength=.25f;
        [Range(0,1),Tooltip("도로의 미세 입자 색상 강도입니다.")]
        public float roadGrainStrength=.35f;
        [Header("수로 · 도심형 호안")]
        [Tooltip("수로와 육지가 맞닿는 경계에 콘크리트 호안을 생성합니다. 원본 이동/충돌 판정은 유지합니다.")]
        public bool canalBanks=true;
        [Tooltip("잔잔한 카툰풍 수면을 표시합니다. 끄면 원본 물 재질을 사용합니다.")]
        public bool cartoonWater=true;
        [Tooltip("실제 Bridge 배치 범위 안에서 양쪽 수면을 잇는 도로 하부를 시각적으로 개방합니다. 도로 상판과 충돌 판정은 유지합니다.")]
        public bool bridgeWaterPassage=true;
        [Range(.05f,.2f),Tooltip("수로 위 도로의 시각적 상판 두께입니다. 원본 콜라이더 두께는 변경하지 않습니다.")]
        public float bridgeDeckThickness=.10f;
        [Tooltip("DH 레지스트리의 수면 텍스처 이름입니다.")]
        public string waterTexture="tilemap_02";
        [Range(.15f,.45f),Tooltip("물 쪽으로 펼쳐지는 호안 폭입니다. 한 셀 폭 수로에도 물길을 남기도록 0.45셀 이하로 제한합니다.")]
        public float bankWidth=.4f;
        [Range(.02f,.12f),Tooltip("호안 상단의 평평한 콘크리트 마감 폭입니다.")]
        public float bankCapWidth=.07f;
        [Tooltip("호안의 기본 콘크리트 색상입니다.")]
        public Color bankTint=new Color(.57f,.59f,.58f,1);
        [Range(.3f,2f),Tooltip("호안 콘크리트 패널 줄눈 간격입니다.")]
        public float bankPanelLength=.8f;
        [Range(0,1),Tooltip("호안 줄눈의 어두워지는 정도입니다.")]
        public float bankJointStrength=.15f;
        [Range(0,1),Tooltip("수면 가까이 젖은 호안의 어두워지는 정도입니다.")]
        public float bankWetStrength=.22f;
        [Tooltip("호안 생성에서 제외할 육지 셀의 월드 X/Z 중심 좌표입니다. 교량·특수 구조물 구간에 사용합니다.")]
        public Vector2Int[] bankExcludedLandCells=new Vector2Int[0];
        [Header("수면 · 흐름")]
        [Tooltip("수로 중앙의 물색입니다.")]
        public Color waterTint=new Color(.135f,.33f,.39f,1);
        [Tooltip("호안 가까운 수면의 밝은 색상입니다. 실제 수심 판정은 아닙니다.")]
        public Color waterEdgeTint=new Color(.265f,.465f,.50f,1);
        [Tooltip("물결 이동 방향입니다. 월드 X/Z 방향으로 적용합니다.")]
        public Vector2 waterFlowDirection=new Vector2(1,0);
        [Tooltip("연결된 수로를 따라 흐릅니다. 방향 값은 각 수역의 하류 선택 기준입니다.")]
        public bool waterFollowCanal=true;
        [Range(0,.2f),Tooltip("수로를 따라 이동하는 속도입니다. 0이어도 잔물결 자체 일렁임은 별도 설정으로 움직입니다.")]
        public float waterFlowSpeed=.035f;
        [Range(.3f,4f),Tooltip("큰 물결 무늬의 크기입니다.")]
        public float waterWaveSize=1.8f;
        [Range(0,.3f),Tooltip("수면 물결의 밝기 변화와 반사 흔들림 강도입니다.")]
        public float waterWaveStrength=.07f;
        [Range(0,3f),Tooltip("큰 물결의 밝고 어두운 음영 가시성입니다. 조명 반사와 독립적이며 0이면 추가 음영만 끕니다. 물결 강도가 0이면 나타나지 않습니다.")]
        public float waterWaveVisibility=1f;
        [Range(.1f,1f),Tooltip("작은 잔물결 사이의 간격입니다. 큰 물결과 독립적으로 조절합니다.")]
        public float waterRippleSize=.32f;
        [Range(0,.15f),Tooltip("잔물결의 표면 기울기와 반사 흔들림 강도입니다. 흰색 패턴은 생성하지 않습니다.")]
        public float waterRippleStrength=.035f;
        [Range(0,3f),Tooltip("잔물결의 유색 음영 가시성입니다. 1이 기본이며 반사가 약한 구도에서도 보이게 합니다. 0이면 추가 음영만 끕니다.")]
        public float waterRippleVisibility=1f;
        [Range(0,1f),Tooltip("잔물결이 일렁이는 속도입니다. 0이면 자체 일렁임을 멈춥니다.")]
        public float waterRippleSpeed=.25f;
        [InspectorName("Water Flow Variation"),Range(0,.15f),Tooltip("수면 전체를 흐르는 큰 불규칙 색 변화의 강도입니다. 흰색 선 무늬는 생성하지 않습니다.")]
        public float waterFlowStreakStrength=.025f;
        [Range(0,.5f),Tooltip("수면의 부드러운 빛 반사 강도입니다.")]
        public float waterShine=.12f;
        [Range(.01f,.2f),Tooltip("물가의 가느다란 밝은 띠 폭입니다.")]
        public float waterEdgeWidth=.05f;
        [Tooltip("빌드에서 유지할 카툰 수면 셰이더 참조입니다.")]
        public Shader waterShader;
        [Tooltip("빌드에서 유지할 콘크리트 호안 셰이더 참조입니다.")]
        public Shader bankShader;
        [Tooltip("재사용 도로 부품의 연석 메시입니다.")]
        public Mesh curbMesh;
        [Tooltip("재사용 도로 부품의 밝은 석재 재질입니다.")]
        public Material curbMaterial;
        [Tooltip("도로에 덧입히는 미세 노멀 텍스처입니다.")]
        public Texture2D roadNormal;
        [Tooltip("도로에 덧입히는 중간 회색 입자 텍스처입니다.")]
        public Texture2D roadGrain;
    }
    [System.Serializable]
    public sealed class JcPathEdgeRule
    {
        [Tooltip("분석한 원본 흙길 텍스처입니다.")] public Texture2D texture;
        [Tooltip("왼쪽 가장자리의 흙길 UV 범위입니다.")] public Vector2 left;
        [Tooltip("오른쪽 가장자리의 흙길 UV 범위입니다.")] public Vector2 right;
        [Tooltip("아래쪽 가장자리의 흙길 UV 범위입니다.")] public Vector2 bottom;
        [Tooltip("위쪽 가장자리의 흙길 UV 범위입니다.")] public Vector2 top;
        public Vector2 Edge(int d)=>d==0?left:d==1?right:d==2?bottom:top;
    }
}

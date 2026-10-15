using UnityEngine;
namespace JC.EnvironmentDetail
{
    [CreateAssetMenu(menuName = "JC/환경/카툰 물 설정")]
    public sealed class JcWaterProfile : ScriptableObject
    {
        [Tooltip("물 전용 저장값입니다. JC_Water에서 불러와 조절하고 설정 저장 버튼으로 확정합니다.")]
        public JcWaterSettings settings = new JcWaterSettings();
    }

    [System.Serializable]
    public sealed class JcWaterSettings
    {
        [Header("물색")]
        [Tooltip("수로 중앙색입니다. 색상 알파는 사용하지 않습니다. 수면 불투명도로 투과 정도를 조절합니다.")] public Color baseColor = new Color(.24813f,.43086f,.64151f,1);
        [Tooltip("물가의 색입니다. 실제 수심을 변경하지 않습니다.")] public Color edgeColor = new Color(.38834f,.57199f,.66038f,1);
        [Range(.05f,3), Tooltip("물가 색이 중앙색으로 변하는 거리입니다. 단위: 월드 미터.")] public float edgeColorWidth = .85f;
        [Range(0,1), Tooltip("물가색 범위와 강도를 월드 위치별로 변화시킵니다. 0이면 기존의 동일 거리 색상이며 1이면 양쪽 색 띠의 규칙성을 크게 줄입니다.")] public float edgeVariation = .65f;
        [Range(.5f,12), Tooltip("물가색의 불규칙한 변화 크기입니다. 단위: 월드 미터. 흐르는 물결과 분리된 완만한 바탕색 분포입니다.")] public float edgeVariationSize = 3.5f;
        [Header("수중 공간")]
        [Range(0,1), Tooltip("얕은 곳의 수면 불투명도입니다. 0이면 수중 물체가 잘 보이고 1이면 완전히 가립니다. 깊은 곳은 수심 흡수에 따라 추가로 가려집니다.")] public float surfaceOpacity = .52f;
        [Range(0,3), Tooltip("깊어질수록 물색에 가려지는 강도입니다. 단위: 1/m. 0이면 깊이에 관계없이 같은 투명도입니다.")] public float depthAbsorption = .65f;
        [Range(.8f,5), Tooltip("확정 수면에서 단순 바닥까지의 깊이입니다. 단위: m. 지형·충돌·베이크 원본은 변경하지 않으며 물 조절기의 시각적 공간만 만듭니다.")] public float basinDepth = 2.5f;
        [Tooltip("물 아래 바닥과 측면의 색입니다. 실제 하천 지형 대신 단순한 어두운 공간으로 표현합니다.")] public Color basinColor = new Color(.075f,.17f,.22f,1);
        [Tooltip("수면 아래를 왕복하는 실제 캡슐 메시를 표시합니다. 플레이에서는 이동하고 편집 모드에서는 경로 위에 정지해 보입니다. 충돌이나 게임 판정이 없는 실루엣 시제품입니다.")] public bool underwaterCapsule = true;
        [Range(.25f,.65f), Tooltip("캡슐 중심이 수면 아래 잠기는 깊이입니다. 단위: m. 캡슐 두께를 포함해 수면을 뚫지 않도록 제한합니다.")] public float capsuleDepth = .35f;
        [Range(0,2), Tooltip("캡슐의 최대 왕복 이동 속도입니다. 단위: m/s. 0이면 시작 위치에 멈춥니다.")] public float capsuleSpeed = .65f;
        [Header("수면 노멀")]
        [Tooltip("큰 물결의 기울기 텍스처입니다. Normal map으로 임포트한 이미지를 사용합니다. 비우면 기본 큰 물결을 사용합니다.")] public Texture2D broadNormal;
        [Tooltip("작은 잔물결의 기울기 텍스처입니다. 비우면 기본 잔물결을 사용합니다.")] public Texture2D detailNormal;
        [Range(0,1), Tooltip("큰 물결 기울기의 강도입니다. 0이면 이 층만 평평해집니다. 실제 메시 높이는 바뀌지 않습니다.")] public float broadNormalStrength = .24f;
        [Range(0,1), Tooltip("작은 잔물결 기울기의 강도입니다. 0이면 잔물결만 끕니다.")] public float detailNormalStrength = .12f;
        [Range(.05f,2), Tooltip("작은 잔물결의 대략적인 간격입니다. 단위: 미터. 텍스처 한 장은 이 값의 8배 범위를 반복합니다.")] public float detailNormalSize = .28f;
        [Range(0,1), Tooltip("잔물결의 독립 이동 속도입니다. 단위: 미터/초. 0이면 독립 이동만 멈추며 수로 흐름은 유지합니다.")] public float rippleSpeed = .08f;
        [Header("물결 음영")]
        [Range(0,1), Tooltip("파란 수면의 밝은 물결과 어두운 골의 대비입니다. 0이면 음영만 끕니다. 스트레이크와 물 높이는 바뀌지 않습니다.")] public float waveShading = .3f;
        [Range(.2f,6), Tooltip("큰 물결 노멀과 음영의 대략적인 간격입니다. 단위: 미터. 텍스처 한 장은 이 값의 8배 범위를 반복합니다.")] public float waveShadeSize = 1.2f;
        [Range(.05f,1), Tooltip("물결 음영의 경계 부드러움입니다. 낮으면 선명한 색면, 높으면 완만한 음영이 됩니다.")] public float waveShadeSoftness = .65f;
        [Header("스트레이크 흐름")]
        [Tooltip("흐름 질감의 색입니다. 알파는 사용하지 않으며 강도로 혼합량을 조절합니다.")] public Color streakColor = new Color(.72f,.87f,.95f,1);
        [Range(0,1), Tooltip("흐름 질감의 최대 혼합량입니다. 0이면 스트레이크만 끄며 음영과 반사는 유지합니다.")] public float streakStrength = .45f;
        [Range(0,1), Tooltip("최대 스트레이크 강도에서 감소하는 비율입니다. 0이면 동일 강도, 0.25면 최대의 75~100%, 1이면 0~100%입니다. 길이·폭과 독립적으로 정해지며 표시 중에는 변하지 않습니다.")] public float streakStrengthVariation = .35f;
        [Range(0,1), Tooltip("고정된 수면 내부 후보 중 표시할 비율입니다. 0이면 숨기고 1이면 모든 후보를 사용합니다. 길이와 폭을 바꿔도 후보 수는 유지됩니다.")] public float streakDensity = .55f;
        [Range(.3f,4), Tooltip("연속된 주선의 최대 길이입니다. 단위: m. 끝은 가늘어지며 주변의 짧은 보조선은 별도로 나타났다 사라집니다.")] public float streakLength = 1.8f;
        [Range(.03f,.4f), Tooltip("주선과 주변 보조선의 최대 가로 크기 기준입니다. 단위: m. 낮추면 주선이 가늘어지고 보조선도 가까워집니다.")] public float streakWidth = .065f;
        [Range(0,1), Tooltip("주선마다 길이가 달라지는 정도입니다. 0이면 모두 설정 길이, 1이면 설정 길이의 25~100%입니다. 폭과 독립적으로 결정되고 이동 중에는 유지됩니다.")] public float streakLengthVariation = .65f;
        [Range(0,1), Tooltip("주선마다 폭이 달라지는 정도입니다. 0이면 모두 설정 폭, 1이면 설정 폭의 25~100%입니다. 길이와 독립적으로 결정되고 이동 중에는 유지됩니다.")] public float streakWidthVariation = .6f;
        [Range(0,2), Tooltip("주선 중심의 구불구불한 정도입니다. 1이면 원래 굴곡, 0.5면 절반, 2면 두 배입니다. 0이면 중심선만 펴지며 기존 두께·밝기 변화는 유지됩니다.")] public float streakBend = 1f;
        [Range(0,1), Tooltip("최대 굽힘에서 감소하는 비율입니다. 0이면 모두 최대 굽힘, 0.5면 최대의 50~100%, 1이면 0~100%입니다. 각 주선의 굽힘은 표시 중 일정합니다.")] public float streakBendVariation = .25f;
        [Range(.1f,5), Tooltip("생성·완전 표시·삭제를 합한 최대 수명의 계수입니다. 1은 최대 4초, 2는 최대 8초입니다. 변동폭에 따라 개별 수명이 감소하며 다음 출현 전 0.25초 공백은 제외합니다.")] public float streakHoldMultiplier = 1;
        [Range(0,1), Tooltip("최대 유지시간에서 감소하는 비율입니다. 0이면 모두 최대 시간, 0.25면 최대의 75~100%, 1이면 0~100%입니다. 각 스트레이크의 값은 고정되며 최대값을 넘지 않습니다.")] public float streakHoldVariation = .35f;
        [Range(0,1), Tooltip("전체 수명 중 생성과 삭제가 함께 사용하는 비율입니다. 각 과정은 절반씩 사용합니다. 0.4면 생성 20%·완전 표시 60%·삭제 20%, 1이면 생성 50%·삭제 50%로 유지 구간이 없습니다. 0이면 즉시 나타나고 사라집니다.")] public float streakTransitionRatio = .4f;
        [Range(.02f,.3f), Tooltip("꼬리에서 머리로 진행하는 생성·삭제 경계의 부드러운 폭입니다. 주선 길이에 대한 비율이며 낮으면 또렷하고 높으면 완만합니다. 생성·삭제에 걸리는 시간은 별도 비율로 결정됩니다.")] public float streakSoftness = .06f;
        [Range(0,1), Tooltip("수면 요철이 강한 곳에 스트레이크를 집중하는 정도입니다. 0이면 요철과 무관한 밝기이며 1이면 큰 요철에서 더 밝게 보입니다. 평평한 구간도 기본 밝기의 70%를 유지합니다. 화면의 그림자나 반사색을 검사하지 않습니다.")] public float streakWaveLink = 1;
        [Header("움직임")]
        [Tooltip("베이크된 수로 방향 지도를 사용합니다. 끄면 아래 공통 방향을 사용합니다. 하류 기준 변경은 지형 설정에서 재베이크합니다.")] public bool followCanal = true;
        [Tooltip("수로 따라 흐름을 끈 경우의 월드 X/Z 방향입니다. (0,0)은 오른쪽으로 처리합니다.")] public Vector2 direction = Vector2.right;
        [Range(0,.5f), Tooltip("흐름 이동 속도입니다. 단위: 월드 미터/초. 0이어도 형태 변화는 별도로 움직입니다.")] public float flowSpeed = .13f;
        [Header("조명")]
        [Range(0,1), Tooltip("주광원 그림자가 물과 스트레이크를 어둡게 하는 정도입니다. 0이면 그림자 영향이 없습니다.")] public float shadowStrength = .25f;
        [Header("반사 표현")]
        [Range(0,1), Tooltip("반사가 물 바탕을 덮는 정도입니다. 스트레이크의 불투명도는 별도입니다. 0이면 반사 촬영도 멈춥니다.")] public float reflection = .65f;
        [Range(0,1), Tooltip("1이면 선명한 주변 풍경, 0이면 흐린 반사입니다.")] public float reflectionClarity = .85f;
        [Range(0,1), Tooltip("비스듬한 시선에서 반사가 강해지는 정도입니다. 0이면 모든 각도에서 일정합니다.")] public float reflectionFresnel = .45f;
        [Range(0,.1f), Tooltip("평면 반사 영상의 물결 왜곡입니다. 화면 UV 단위이며 0이면 영상 왜곡만 멈춥니다. 환경 반사와 광원은 수면 노멀을 계속 사용합니다.")] public float reflectionDistortion = .012f;
        [Header("반사 촬영 · 성능")]
        [Tooltip("주변 풍경과 현재 스카이박스를 반사 카메라로 촬영합니다. 끄면 기본 환경 반사만 사용합니다.")] public bool planarReflection = true;
        [Tooltip("반사 영상의 긴 변 해상도입니다. 같은 카메라·수면 높이는 한 장을 공유합니다.")] public WaterReflectionResolution reflectionResolution = WaterReflectionResolution.Medium;
        [Range(1,8), Tooltip("원본 카메라가 이 횟수만큼 그려질 때마다 반사를 갱신합니다. 1이면 매번 갱신합니다.")] public int reflectionUpdateInterval = 1;
        [Tooltip("반사할 레이어입니다. 연결된 수면 자체는 자동 제외됩니다. UI는 기본 제외입니다.")] public LayerMask reflectionLayers = ~(1 << 5);
        public JcWaterSettings Copy() => JsonUtility.FromJson<JcWaterSettings>(JsonUtility.ToJson(this));
        static Texture2D pattern, defaultBroad, defaultDetail;
        static Texture2D Pattern => pattern != null ? pattern : (pattern = Resources.Load<Texture2D>("JC_Water_Strands"));
        public void Apply(Material m)
        {
            m.renderQueue=(int)UnityEngine.Rendering.RenderQueue.Transparent;
            m.SetVector("_Transmission",new Vector4(Mathf.Clamp01(surfaceOpacity),Mathf.Clamp(depthAbsorption,0,3),0,0));
            m.SetColor("_BaseColor", baseColor); m.SetColor("_EdgeColor", edgeColor); m.SetColor("_StreakColor", streakColor);
            m.SetTexture("_StreakPattern", Pattern);
            if(defaultBroad==null)defaultBroad=Resources.Load<Texture2D>("JC_Water_NormalBroad");
            if(defaultDetail==null)defaultDetail=Resources.Load<Texture2D>("JC_Water_NormalDetail");
            m.SetTexture("_NormalBroad",broadNormal!=null?broadNormal:defaultBroad);
            m.SetTexture("_NormalDetail",detailNormal!=null?detailNormal:defaultDetail);
            m.SetVector("_Normals",new Vector4(Mathf.Clamp01(broadNormalStrength),Mathf.Clamp01(detailNormalStrength),Mathf.Clamp(detailNormalSize,.05f,2),Mathf.Clamp01(rippleSpeed)));
            m.SetVector("_EdgeVariation",new Vector4(Mathf.Clamp01(edgeVariation),Mathf.Clamp(edgeVariationSize,.5f,12),0,0));
            m.SetVector("_Streak",new Vector4(Mathf.Clamp01(streakStrength),Mathf.Clamp01(streakDensity),Mathf.Clamp(streakLength,.3f,4),Mathf.Clamp(streakWidth,.03f,.4f)));
            m.SetVector("_StreakLifetime",new Vector4(Mathf.Clamp(streakHoldMultiplier,.1f,5),Mathf.Clamp01(streakHoldVariation),Mathf.Clamp01(streakTransitionRatio),Mathf.Clamp01(streakStrengthVariation)));
            m.SetVector("_StreakBend",new Vector4(Mathf.Clamp(streakBend,0,2),Mathf.Clamp01(streakBendVariation),0,0));
            m.SetVector("_StreakShape",new Vector4(Mathf.Clamp(streakSoftness,.02f,.3f),Mathf.Clamp01(streakWaveLink),Mathf.Clamp01(streakLengthVariation),Mathf.Clamp01(streakWidthVariation)));
            m.SetVector("_Surface",new Vector4(edgeColorWidth,shadowStrength,0,0));
            m.SetVector("_WaveShade", new Vector4(Mathf.Clamp01(waveShading),Mathf.Max(.2f,waveShadeSize),Mathf.Clamp(waveShadeSoftness,.05f,1),0));
            var d=direction.sqrMagnitude>.0001f?direction.normalized:Vector2.right;
            m.SetVector("_Flow",new Vector4(d.x,d.y,flowSpeed,0));
            m.SetFloat("_FollowCanal",followCanal && m.GetTexture("_FlowMap")!=null?1:0);
            m.SetVector("_Reflection",new Vector4(Mathf.Clamp01(reflection),Mathf.Clamp01(reflectionClarity),Mathf.Clamp01(reflectionFresnel),Mathf.Clamp(reflectionDistortion,0,.1f)));
        }
    }
    public enum WaterReflectionResolution { Low = 256, Medium = 512, High = 1024 }
}

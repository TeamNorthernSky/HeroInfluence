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
        [Header("방향성 큰 물결 · 비교")]
        [Range(0,1), Tooltip("0은 기존 큰 물결, 1은 방향성 큰 물결입니다. 기존 노멀 강도·크기와 잔물결 설정은 유지합니다.")] public float directionalWaveMix = 0f;
        [Range(1,12), Tooltip("물결 마루의 길이 비율입니다. 높이면 긴 곡선이 이어집니다.")] public float waveElongation = 5f;
        [Range(0,2), Tooltip("긴 물결을 휘게 하는 굴곡 강도입니다. 0이면 굴곡 없는 방향성 파형입니다.")] public float waveBendStrength = .65f;
        [Range(.5f,12), Tooltip("굴곡 변화의 공간 크기입니다. 단위: 미터. 높으면 완만하고 큰 곡선이 됩니다.")] public float waveBendSize = 4f;
        [Range(0,.5f), Tooltip("굴곡 형태가 변하는 속도입니다. 단위: 미터/초. 0이면 형태 변화만 멈추고 흐름 이동은 유지합니다.")] public float waveBendSpeed = .08f;
        [Range(-90,90), Tooltip("흐름에 대한 물결 진행 각도입니다. 단위: 도. 0이면 마루가 흐름을 가로지릅니다.")] public float directionalWaveAngle = 0f;
        [Header("물결 음영")]
        [Range(0,1), Tooltip("파란 수면의 밝은 물결과 어두운 골의 대비입니다. 0이면 음영만 끕니다. 광원 하이라이트와 물 높이는 바뀌지 않습니다.")] public float waveShading = .3f;
        [Range(.2f,6), Tooltip("큰 물결 노멀과 음영의 대략적인 간격입니다. 단위: 미터. 텍스처 한 장은 이 값의 8배 범위를 반복합니다.")] public float waveShadeSize = 1.2f;
        [Range(.05f,1), Tooltip("물결 음영의 경계 부드러움입니다. 낮으면 선명한 색면, 높으면 완만한 음영이 됩니다.")] public float waveShadeSoftness = .65f;
        [Header("광원 하이라이트")]
        [Range(0,8), Tooltip("주광원 하이라이트의 밝기 배율입니다. 환경 반사와 독립적이며 1보다 높이면 과장합니다. 광원 방향·색·그림자는 유지하고 발광으로 대체하지 않습니다.")] public float highlightStrength = 1f;
        [Range(0,1), Tooltip("광원 하이라이트의 폭입니다. 낮으면 좁고 선명하며 높으면 넓고 은은합니다. 환경 반사의 선명도는 바꾸지 않습니다.")] public float highlightRoughness = .32f;
        [Range(0,1), Tooltip("광원 하이라이트 경계의 부드러움입니다. 0은 또렷한 카툰 색면, 1은 자연스러운 연속 밝기입니다.")] public float highlightSoftness = .8f;
        [Range(0,2), Tooltip("같은 수면 노멀 위의 좁은 반짝임을 더하는 비율입니다. 0이면 넓은 하이라이트만 남습니다. 원거리에서는 자동으로 넓히고 약화해 점멸을 줄입니다.")] public float sparkleStrength = .25f;
        [Header("연출용 하이라이트")]
        [Range(0,3), Tooltip("카메라를 따라가는 물 전용 페이크 하이라이트 밝기입니다. 0이면 끕니다. 태양 밝기와 독립적이며 다른 오브젝트를 비추지 않습니다.")] public float fakeHighlightStrength = 0f;
        [Tooltip("연출용 하이라이트 색입니다. 실제 주광원 색과 독립적이며 알파는 사용하지 않습니다.")] public Color fakeHighlightColor = new Color(.7f,.85f,1f,1f);
        [Range(0,1), Tooltip("연출용 하이라이트의 폭입니다. 낮으면 가늘고 높은 값은 넓게 퍼집니다.")] public float fakeHighlightWidth = .2f;
        [Range(0,.5f), Tooltip("카메라 쪽으로 기운 물결을 선택하는 정도입니다. 높으면 가파른 물결에서 보입니다. 큰 물결 기울기보다 너무 높이면 반짝임이 줄어듭니다.")] public float fakeHighlightTilt = .12f;
        [Range(0,1), Tooltip("주광원 그림자 안에서 연출용 하이라이트를 줄이는 비율입니다. 0이면 그림자 무관, 1이면 주광원 그림자를 완전히 따릅니다.")] public float fakeHighlightShadow = .5f;
        [Header("움직임")]
        [Tooltip("베이크된 수로 방향 지도를 사용합니다. 끄면 아래 공통 방향을 사용합니다. 하류 기준 변경은 지형 설정에서 재베이크합니다.")] public bool followCanal = true;
        [Tooltip("수로 따라 흐름을 끈 경우의 월드 X/Z 방향입니다. (0,0)은 오른쪽으로 처리합니다.")] public Vector2 direction = Vector2.right;
        [Range(0,.5f), Tooltip("흐름 이동 속도입니다. 단위: 월드 미터/초. 0이어도 형태 변화는 별도로 움직입니다.")] public float flowSpeed = .13f;
        [Header("조명")]
        [Range(0,1), Tooltip("주광원 그림자가 물 바탕을 어둡게 하는 정도입니다. 직접광 하이라이트는 이 값과 관계없이 실제 그림자를 따릅니다.")] public float shadowStrength = .25f;
        [Header("반사 표현")]
        [Range(0,1), Tooltip("반사가 물 바탕을 덮는 정도입니다. 광원 하이라이트 강도는 별도입니다. 0이면 반사 촬영도 멈춥니다.")] public float reflection = .65f;
        [Range(0,1), Tooltip("1이면 선명한 주변 풍경, 0이면 흐린 반사입니다.")] public float reflectionClarity = .85f;
        [Range(0,1), Tooltip("비스듬한 시선에서 반사가 강해지는 정도입니다. 0이면 모든 각도에서 일정합니다.")] public float reflectionFresnel = .45f;
        [Range(0,.1f), Tooltip("평면 반사 영상의 물결 왜곡입니다. 화면 UV 단위이며 0이면 영상 왜곡만 멈춥니다. 환경 반사와 광원은 수면 노멀을 계속 사용합니다.")] public float reflectionDistortion = .012f;
        [Header("반사 촬영 · 성능")]
        [Tooltip("주변 풍경과 현재 스카이박스를 반사 카메라로 촬영합니다. 끄면 기본 환경 반사만 사용합니다.")] public bool planarReflection = true;
        [Tooltip("반사 영상의 긴 변 해상도입니다. 같은 카메라·수면 높이는 한 장을 공유합니다.")] public WaterReflectionResolution reflectionResolution = WaterReflectionResolution.Medium;
        [Range(1,8), Tooltip("원본 카메라가 이 횟수만큼 그려질 때마다 반사를 갱신합니다. 1이면 매번 갱신합니다.")] public int reflectionUpdateInterval = 1;
        [Tooltip("반사할 레이어입니다. 연결된 수면 자체는 자동 제외됩니다. UI는 기본 제외입니다.")] public LayerMask reflectionLayers = ~(1 << 5);
        public JcWaterSettings Copy() => JsonUtility.FromJson<JcWaterSettings>(JsonUtility.ToJson(this));
        static Texture2D defaultBroad, defaultDetail;
        public void Apply(Material m)
        {
            m.renderQueue=(int)UnityEngine.Rendering.RenderQueue.Transparent;
            m.SetVector("_Transmission",new Vector4(Mathf.Clamp01(surfaceOpacity),Mathf.Clamp(depthAbsorption,0,3),0,0));
            m.SetColor("_BaseColor", baseColor); m.SetColor("_EdgeColor", edgeColor);
            if(defaultBroad==null)defaultBroad=Resources.Load<Texture2D>("JC_Water_NormalBroad");
            if(defaultDetail==null)defaultDetail=Resources.Load<Texture2D>("JC_Water_NormalDetail");
            m.SetTexture("_NormalBroad",broadNormal!=null?broadNormal:defaultBroad);
            m.SetTexture("_NormalDetail",detailNormal!=null?detailNormal:defaultDetail);
            m.SetVector("_Normals",new Vector4(Mathf.Clamp01(broadNormalStrength),Mathf.Clamp01(detailNormalStrength),Mathf.Clamp(detailNormalSize,.05f,2),Mathf.Clamp01(rippleSpeed)));
            m.SetVector("_DirectionalWave",new Vector4(Mathf.Clamp01(directionalWaveMix),Mathf.Clamp(waveElongation,1,12),Mathf.Clamp(waveBendStrength,0,2),Mathf.Clamp(waveBendSize,.5f,12)));
            m.SetVector("_WaveBend",new Vector4(Mathf.Clamp(waveBendSpeed,0,.5f),directionalWaveAngle*Mathf.Deg2Rad,0,0));
            m.SetVector("_EdgeVariation",new Vector4(Mathf.Clamp01(edgeVariation),Mathf.Clamp(edgeVariationSize,.5f,12),0,0));
            m.SetColor("_FakeHighlightColor",fakeHighlightColor);
            m.SetVector("_FakeHighlight",new Vector4(Mathf.Clamp(fakeHighlightStrength,0,3),Mathf.Clamp01(fakeHighlightWidth),Mathf.Clamp(fakeHighlightTilt,0,.5f),Mathf.Clamp01(fakeHighlightShadow)));
            m.SetVector("_Specular",new Vector4(Mathf.Clamp(highlightStrength,0,8),Mathf.Clamp01(highlightRoughness),Mathf.Clamp01(highlightSoftness),Mathf.Clamp(sparkleStrength,0,2)));
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

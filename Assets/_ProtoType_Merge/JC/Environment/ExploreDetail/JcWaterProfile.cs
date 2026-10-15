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
        [Tooltip("수로 중앙색입니다. 알파와 관계없이 수면은 불투명합니다.")] public Color baseColor = new Color(.24813f,.43086f,.64151f,1);
        [Tooltip("물가의 색입니다. 실제 수심을 변경하지 않습니다.")] public Color edgeColor = new Color(.38834f,.57199f,.66038f,1);
        [Range(.05f,3), Tooltip("물가 색이 중앙색으로 변하는 거리입니다. 단위: 월드 미터.")] public float edgeColorWidth = .85f;
        [Header("물결 음영")]
        [Range(0,1), Tooltip("파란 수면의 밝은 물결과 어두운 골의 대비입니다. 0이면 음영만 끕니다. 포말과 물 높이는 바뀌지 않습니다.")] public float waveShading = .3f;
        [Range(.2f,6), Tooltip("넓은 물결 음영의 크기입니다. 단위: 월드 미터. 포말 간격과 별도로 조절합니다.")] public float waveShadeSize = 1.2f;
        [Range(.05f,1), Tooltip("물결 음영의 경계 부드러움입니다. 낮으면 선명한 색면, 높으면 완만한 음영이 됩니다.")] public float waveShadeSoftness = .65f;
        [Header("수면 포말")]
        [Tooltip("포말의 색입니다. 이 색상의 알파 대신 포말 불투명도 항목을 사용합니다.")] public Color foamColor = new Color(.91f,.97f,1,1);
        [Range(0,1), Tooltip("수면·물가 포말 전체의 불투명도입니다. 0이면 포말이 보이지 않고 1이면 가장 진합니다. 파란 물 바탕의 투명도는 바뀌지 않습니다.")] public float foamOpacity = 1;
        [Range(.02f,.5f), Tooltip("포말이 나타나고 사라지는 분포 경계의 투명도 보간 폭입니다. 높으면 넓고 부드럽게 사라집니다. 초 단위가 아니며 실제 변화 속도는 흐름·형태 속도를 따릅니다.")] public float foamFade = .18f;
        [Range(.15f,4), Tooltip("포말 무늬의 기본 간격입니다. 단위: 월드 미터.")] public float foamSize = .45f;
        [Range(0,1), Tooltip("포말이 나타나는 영역 비율입니다. 0이면 수면 포말을 끕니다. 물가 포말은 별도입니다.")] public float foamDensity = .52f;
        [Range(.01f,.3f), Tooltip("무늬 간격에 대한 포말 선 두께 비율입니다.")] public float foamThickness = .055f;
        [Range(1,5), Tooltip("포말 무늬를 늘리는 길이 비율입니다. 1이면 방향성이 약하고 높을수록 길어집니다.")] public float foamLength = 1.8f;
        [Range(0,2), Tooltip("포말 곡선을 불규칙하게 휘게 하는 강도입니다. 0이면 기본 곡선, 높으면 더 굽고 갈라집니다. 형태 변화량과 별도로 정지 상태에도 적용됩니다.")] public float foamCurvature = .85f;
        [Header("움직임")]
        [Tooltip("베이크된 수로 방향 지도를 사용합니다. 끄면 아래 공통 방향을 사용합니다. 하류 기준 변경은 지형 설정에서 재베이크합니다.")] public bool followCanal = true;
        [Tooltip("수로 따라 흐름을 끈 경우의 월드 X/Z 방향입니다. (0,0)은 오른쪽으로 처리합니다.")] public Vector2 direction = Vector2.right;
        [Range(0,.5f), Tooltip("흐름 이동 속도입니다. 단위: 월드 미터/초. 0이어도 형태 변화는 별도로 움직입니다.")] public float flowSpeed = .13f;
        [Range(0,1), Tooltip("포말 자체 형태가 변하는 속도입니다. 0이면 형태 변화를 멈춥니다.")] public float morphSpeed = .18f;
        [Range(0,1), Tooltip("포말 형태의 흔들림 크기입니다. 0이면 이동만 합니다.")] public float morphAmount = .25f;
        [Header("물가 포말")]
        [Tooltip("호안에 닿는 수면에 포말을 표시합니다.")] public bool shoreFoam = true;
        [Range(.01f,.4f), Tooltip("물가 포말의 최대 폭입니다. 단위: 월드 미터.")] public float shoreWidth = .09f;
        [Range(0,1), Tooltip("물가 포말의 끊김 정도입니다. 0이면 이어진 띠, 1에 가까우면 드문 조각이 됩니다.")] public float shoreBreakup = .35f;
        [Range(0,1), Tooltip("물가 포말이 변화하는 속도입니다. 0이면 물가 포말의 움직임을 멈춥니다.")] public float shoreSpeed = .16f;
        [Header("조명")]
        [Range(0,1), Tooltip("주광원 그림자가 물과 포말을 어둡게 하는 정도입니다. 0이면 그림자 영향이 없습니다.")] public float shadowStrength = .25f;
        [Range(0,.3f), Tooltip("물 바탕의 보조 환경 반사입니다. 0이면 반사 없이 색면과 포말로 표현합니다.")] public float reflection = 0;
        public JcWaterSettings Copy() => JsonUtility.FromJson<JcWaterSettings>(JsonUtility.ToJson(this));
        public void Apply(Material m)
        {
            m.SetColor("_BaseColor", baseColor); m.SetColor("_EdgeColor", edgeColor); m.SetColor("_FoamColor", foamColor);
            m.SetVector("_Foam", new Vector4(Mathf.Max(.15f,foamSize),Mathf.Clamp01(foamDensity),Mathf.Clamp(foamThickness,.01f,.3f),Mathf.Clamp(foamLength,1,5)));
            m.SetVector("_Morph", new Vector4(morphSpeed,morphAmount,edgeColorWidth,shadowStrength));
            m.SetVector("_WaveShade", new Vector4(Mathf.Clamp01(waveShading),Mathf.Max(.2f,waveShadeSize),Mathf.Clamp(waveShadeSoftness,.05f,1),0));
            m.SetFloat("_FoamCurvature",Mathf.Clamp(foamCurvature,0,2));
            m.SetFloat("_FoamOpacity",Mathf.Clamp01(foamOpacity));
            m.SetFloat("_FoamFade",Mathf.Clamp(foamFade,.02f,.5f));
            m.SetVector("_ShoreFoam", new Vector4(shoreFoam?1:0,shoreWidth,shoreBreakup,shoreSpeed));
            var d=direction.sqrMagnitude>.0001f?direction.normalized:Vector2.right;
            m.SetVector("_Flow",new Vector4(d.x,d.y,flowSpeed,foamSize));
            m.SetFloat("_FollowCanal",followCanal && m.GetTexture("_FlowMap")!=null?1:0);
            m.SetFloat("_Shine",reflection);
        }
    }
}

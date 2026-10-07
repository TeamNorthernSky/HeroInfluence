using UnityEngine;

[System.Serializable]
public sealed class JcBattleLookSettings
{
    [Range(0,2), Tooltip("캐릭터·전투 오브젝트 채도입니다. 1은 원색, 1.15는 색을 약간 더 선명하게 합니다.")]
    public float saturation = 1.15f;
    [Tooltip("캐릭터에 비추는 가상 주광 방향입니다. 배경 조명 강도와 독립적이며 월드 공간의 빛이 오는 방향입니다.")]
    public Vector3 keyDirection = new Vector3(.4f,.8f,-.3f);
    [Tooltip("밝은 면의 색입니다. 원본 캐릭터 색에 곱해집니다.")]
    public Color keyTint = new Color(1,.98f,.94f);
    [Tooltip("어두운 면의 색입니다. 약한 청색으로 검게 꺼지는 음영을 완화합니다.")]
    public Color fillTint = new Color(.87f,.92f,1);
    [Range(0,1), Tooltip("어두운 면의 최소 밝기입니다. 클수록 명암 대비가 줄어듭니다.")]
    public float shadeFloor = .7f;
    [Range(0,1), Tooltip("빛이 옆면으로 감싸는 정도입니다. 클수록 곡면 명암이 부드러워집니다.")]
    public float lightWrap = .4f;
    [Range(0,1), Tooltip("캐릭터가 받는 실시간 그림자의 강도입니다. 바닥에 드리우는 그림자에는 영향을 주지 않습니다.")]
    public float receivedShadow = .2f;
    [Range(0,1), Tooltip("원본 색상 텍스처의 어두운 색을 완만하게 밝힙니다. 그려진 그림자를 완전히 제거하지는 않습니다.")]
    public float albedoLift = .08f;
    [Range(0,.3f), Tooltip("실루엣 가장자리의 약한 밝기 보정입니다. 0이면 끕니다.")]
    public float rimStrength = .025f;
    [Range(0,1), Tooltip("네코밍의 어두운 면 최소 밝기입니다. 밝은 곡면의 과도한 대비를 줄입니다.")]
    public float nekomingShadeFloor = .83f;
    [Range(0,1), Tooltip("네코밍 원본 텍스처의 어두운 색 보정량입니다.")]
    public float nekomingAlbedoLift = .14f;
    [Tooltip("드론·증폭기 등 지정한 비인간형 모델에 별도 몰딩 명암을 사용합니다.")]
    public bool useMechanicalMolding = true;
    [Tooltip("몰딩 보정을 적용할 유닛 TemplateIndex 목록입니다. 인간형 캐릭터 ID는 넣지 않습니다.")]
    public string[] mechanicalUnitIds = {"20001","40002","40003","40005"};
    [Range(0,1), Tooltip("비인간형 모델의 몰딩 경계와 좁은 하이라이트 강조량입니다. 0이면 강조를 끕니다.")]
    public float moldingStrength = .4f;
    [Range(4,96), Tooltip("몰딩 하이라이트의 날카로움입니다. 높을수록 빛나는 띠가 좁아집니다.")]
    public float moldingSharpness = 32;
    [Range(0,1), Tooltip("비인간형 모델의 어두운 면 밝기입니다. 낮출수록 패널 면의 대비가 커집니다.")]
    public float mechanicalShadeFloor = .52f;
    [Range(0,1), Tooltip("비인간형 모델의 빛 감싸기입니다. 작을수록 면과 몰딩의 구분이 선명해집니다.")]
    public float mechanicalLightWrap = .08f;
}

[CreateAssetMenu(menuName="JC/Battle/Character Look Profile")]
public sealed class JcBattleLookProfile : ScriptableObject
{
    [Tooltip("저장된 캐릭터 명암·채도·비인간형 몰딩 설정입니다." )]
    public JcBattleLookSettings settings = new JcBattleLookSettings();
}

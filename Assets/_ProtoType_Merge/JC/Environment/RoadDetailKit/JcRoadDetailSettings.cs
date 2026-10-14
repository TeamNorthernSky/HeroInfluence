using UnityEngine;
namespace JC.EnvironmentDetail
{
    [CreateAssetMenu(menuName="JC/환경/도로 디테일 설정")]
    public sealed class JcRoadDetailSettings : ScriptableObject
    {
        [Tooltip("부품을 생성할 도로 폭입니다. 전투용 배치에서는 배경 프로필의 도로 폭을 사용합니다.")][Min(1)] public float roadWidth=11.5f;
        [Tooltip("직선 도로 부품의 길이입니다.")][Min(1)] public float segmentLength=6;
        [Tooltip("인도 부품 폭입니다.")][Min(.5f)] public float sidewalkWidth=4.8f;
        [Tooltip("노면에서 인도까지 높이입니다.")][Range(.05f,1)] public float sidewalkHeight=.48f;
        [Tooltip("연석 상단 모서리의 둥글기입니다.")][Range(.01f,.1f)] public float rounding=.045f;
        [Tooltip("횡단보도 한 줄의 폭입니다.")][Range(.2f,1)] public float stripeWidth=.55f;
        [Tooltip("횡단보도 줄 사이 간격입니다.")][Range(.1f,1)] public float stripeGap=.45f;
        [Tooltip("횡단보도 통행 방향 길이입니다.")][Range(1,5)] public float crossingDepth=2.8f;
        [Tooltip("도색의 기본 색상입니다.")] public Color paintColor=new Color(.82f,.80f,.7f,1);
        [Tooltip("도색의 마모 비율입니다.")][Range(0,.5f)] public float wear=.12f;
        [Tooltip("표면 깊이 효과 강도입니다. 도색이 뜨지 않게 작은 값을 사용합니다.")][Range(0,.025f)] public float parallax=.006f;
        [Tooltip("야지에 배치할 기존 나무 프리팹입니다. 원본은 수정하지 않습니다.")] public GameObject treePrefab;
        [Tooltip("예시 배경의 나무 높이입니다.")][Range(1,8)] public float treeHeight=4.8f;
        [Tooltip("지형·돌·식생 배치의 고정 난수입니다.")] public int seed=1202;
    }
}

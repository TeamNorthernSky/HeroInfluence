using UnityEngine;
namespace JC.BattleTesting
{
    // 미리 구운 도시 재질의 시간대별 연결점입니다. 전역 LightmapSettings/RenderSettings를 사용하지 않습니다.
    [CreateAssetMenu(menuName="JC/Boss/도시 베이크 연결")]
    public sealed class JcBossSceneryBake : ScriptableObject
    {
        [System.Serializable] public sealed class Binding
        {
            [Tooltip("도시 루트 아래 Renderer의 상대 경로입니다.")] public string rendererPath;
            [Tooltip("배치 변경 후 오래된 결과를 연결하지 않도록 확인할 메시입니다.")] public Mesh mesh;
            [Tooltip("베이크용 UV2를 가진 메시입니다. 비어 있으면 원래 메시를 유지합니다.")] public Mesh bakedMesh;
            [Tooltip("라이팅이 포함된 재질입니다. 실제 베이크 후 할당합니다.")] public Material[] materials;
        }
        [System.Serializable] public sealed class Period
        {
            [Tooltip("시간대 기준 시각입니다.")] public float hour;
            [Tooltip("이 시간대에 완료된 베이크 연결입니다. 비어 있으면 임시 도시 색상을 사용합니다.")] public Binding[] bindings = new Binding[0];
        }
        [Tooltip("08:30, 14:30, 20:30, 02:30 순서입니다. 이번 단계에서는 비워 둡니다.")]
        public Period[] periods = {new Period{hour=8.5f},new Period{hour=14.5f},new Period{hour=20.5f},new Period{hour=2.5f}};
    }
}

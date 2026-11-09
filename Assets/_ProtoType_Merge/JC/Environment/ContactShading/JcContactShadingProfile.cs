using System;
using UnityEngine;

namespace JC.Environment
{
    // JC | 2026-11-06 | 씬별 접점 위치와 조절값을 분리하여 은행·보스가 각자 프로필을 갖고 같은 계산을 재사용합니다.
    [CreateAssetMenu(menuName = "JC/환경/접점 음영 프로필")]
    public sealed class JcContactShadingProfile : ScriptableObject
    {
        public const int MaxSegments = 64;
        [InspectorName("접점 음영 사용"), Tooltip("지정된 재질에만 접점 음영을 표시합니다.")]
        public bool shadingEnabled = true;
        [InspectorName("음영 색상"), Tooltip("접점에서 표면색에 곱할 색상입니다. 흰색이면 어두워지지 않습니다.")]
        public Color tint = new Color(.25f, .23f, .21f, 1);
        [Range(0, 1), InspectorName("음영 농도"), Tooltip("접점 음영을 원래 표면색에 혼합하는 강도입니다. 0이면 원래 색상입니다.")]
        public float strength = .18f;
        [Range(.005f, 1), InspectorName("번짐 폭"), Tooltip("배경 루트의 로컬 거리 단위입니다. 루트 배율 1.5에서 0.12는 월드 거리 0.18입니다.")]
        public float width = .12f;
        [Range(.05f, 1), InspectorName("경계 부드러움"), Tooltip("번짐 폭 중 부드럽게 사라지는 비율입니다. 1이면 전체 폭에서 서서히 사라집니다.")]
        public float softness = 1;
        [InspectorName("음영 대상 원본 재질"), Tooltip("이 원본에서 만들어진 미리보기 재질에만 적용합니다. 원본 재질 에셋은 변경하지 않습니다.")]
        public Material[] targetMaterials = Array.Empty<Material>();
        [InspectorName("접점 셰이더"), Tooltip("URP Lit 속성을 유지하는 JC/Environment/Contact Lit 셰이더입니다. 빌드에서 참조를 유지합니다.")]
        public Shader contactShader;
        [InspectorName("접점 구간"), Tooltip("배경 루트 로컬 좌표로 지정한 선분입니다. 최대 64개이며 코너가 겹쳐도 농도가 중첩되지 않습니다.")]
        public Segment[] segments = Array.Empty<Segment>();

        [Serializable]
        public struct Segment
        {
            [InspectorName("시작"), Tooltip("배경 루트 로컬 좌표의 접점 시작 위치입니다.")]
            public Vector3 start;
            [InspectorName("끝"), Tooltip("배경 루트 로컬 좌표의 접점 끝 위치입니다.")]
            public Vector3 end;
            // JC | 2026-11-06 | 벽 안쪽에도 번지던 대칭 마스크를 막습니다. 기존 프로필의 0은 양방향으로 유지됩니다.
            [InspectorName("바닥 쪽 방향"), Tooltip("이 방향의 반공간에만 음영을 그립니다. 벽에서 실내 바닥을 향하는 로컬 방향이며 0이면 양쪽입니다.")]
            public Vector3 receiverDirection;
        }
    }
}

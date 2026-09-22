using System;
using UnityEngine;
namespace JC.BuildingColors
{
    [Serializable] public struct BgPartColor
    {
        [Tooltip("0~360도 색상각입니다. 원본 색·명암 무시가 꺼져 있으면 원본 파츠의 색 차이를 유지합니다.")] [Range(0,360)] public float hue;
        [Tooltip("파츠 기준 명도입니다. 0은 검정, 1은 흰색입니다. 원본 색·명암 무시가 꺼져 있으면 원본의 명암 차이를 유지합니다.")] [Range(0,1)] public float lightness;
        [Tooltip("지각 채도입니다. 0은 무채색, 100은 강한 색입니다. 원본 알파는 변경하지 않습니다.")] [Range(0,100)] public float saturation;
        [Tooltip("켜면 이 파츠의 원본 색과 텍스처 명암·무늬를 무시하고 목표색으로 채웁니다. 마스크 경계와 알파, 씬 조명·그림자는 유지됩니다. 개별·전체 프로필에 저장됩니다.")] public bool ignoreSourceColorAndShading;
    }
    [Serializable] public sealed class BgTextureBinding
    {
        [Tooltip("같은 종류의 건물을 식별할 원본 재질입니다. 재질 자체는 수정하지 않습니다.")] public Material material;
        [Tooltip("원본 비교용 텍스처입니다. 비어 있으면 재질 기본 흰색을 사용합니다.")] public Texture2D original;
        [Tooltip("색상 조절의 시작 텍스처입니다. 원본 또는 승인된 초기 시안입니다.")] public Texture2D source;
        [Tooltip("R=벽, G=지붕, B=창문, A=식생의 가중치입니다. 선형 데이터로 임포트합니다.")] public Texture2D maskParts;
        [Tooltip("바닥 블록 영역의 가중치입니다. 선형 데이터로 임포트합니다.")] public Texture2D maskFloor;
        [Tooltip("추가 파츠 5~8의 RGBA 가중치입니다.")] public Texture2D maskExtra;
        [Tooltip("추가 파츠 9~12의 RGBA 가중치입니다.")] public Texture2D maskExtra2;
    }
    [Serializable] public sealed class BgMeshBinding
    {
        [Tooltip("비교 및 복원할 원본 메시입니다.")] public Mesh original;
        [Tooltip("서로 다른 파츠가 공유하던 UV를 분리한 파생 메시입니다. 원본 모델은 유지됩니다.")] public Mesh separated;
    }
    [CreateAssetMenu(menuName="JC/Building Colors/건물 파츠 정의")]
    public sealed class BgColorDefinition : ScriptableObject
    {
        [Tooltip("프로필을 연결할 건물 종류 ID입니다. 생성 후 임의로 변경하지 마세요.")] public string buildingId;
        [Tooltip("이 정의가 대응하는 원본 건물 프리팹입니다.")] public GameObject prefab;
        [Tooltip("미리보기 텍스처를 생성하는 전용 셰이더입니다.")] public Shader recolorShader;
        [Tooltip("인스펙터에 표시할 파츠 분류의 범위와 알려진 한계입니다.")] public string classificationNote;
        [Tooltip("재질별 원본·시작 텍스처 및 파츠 마스크입니다.")] public BgTextureBinding[] bindings;
        [Tooltip("시작 텍스처에서 추출한 파츠 기준값입니다. 색상 변화량의 기준이며 기본 목표색과 구분합니다.")] public BgPartColor[] initial;
        [Tooltip("각 파츠의 영구 ID입니다. 프로필은 배열 순서 대신 ID로 연결됩니다.")] public string[] partIds;
        [Tooltip("인스펙터에 표시할 건물별 파츠 이름입니다.")] public string[] partNames;
        [Tooltip("초기값 복원 시 사용할 목표값입니다. 옥상 바닥은 채도 0입니다.")] public BgPartColor[] defaults;
        [Tooltip("공유 UV를 분리한 건물의 원본·파생 메시 연결입니다.")] public BgMeshBinding[] meshes;
        public const int Capacity=13;
        public static readonly string[] LegacyIds={"wall","roof","window","foliage","floor"};
        public string[] Ids=>partIds!=null&&partIds.Length==initial?.Length?partIds:LegacyIds;
        public string[] Names=>partNames!=null&&partNames.Length==initial?.Length?partNames:PartNames;
        public BgPartColor[] Defaults=>defaults!=null&&defaults.Length==initial?.Length?defaults:initial;
        public BgPartColor[] Remap(BgPartColor[] values,string[] ids)
        {
            var result=BgColorModifier.Copy(Defaults);
            if(values==null)return result;
            var sourceIds=ids!=null&&ids.Length==values.Length?ids:LegacyIds;
            for(int i=0;i<values.Length&&i<sourceIds.Length;i++){int to=Array.IndexOf(Ids,sourceIds[i]);if(to>=0&&to<result.Length)result[to]=values[i];}
            // Old RowHouse profiles painted the central front with accent.
            if(buildingId=="BGRowHouse002"&&Array.IndexOf(sourceIds,"central_block")<0){int to=Array.IndexOf(Ids,"central_block"),from=Array.IndexOf(sourceIds,"accent");if(to>=0&&from>=0&&from<values.Length)result[to]=values[from];}
            return result;
        }
        public static bool ValidValues(BgPartColor[] values,string[] ids)
        {
            if(values==null||values.Length==0||values.Length>Capacity)return false;
            foreach(var p in values)if(float.IsNaN(p.hue)||float.IsInfinity(p.hue)||float.IsNaN(p.lightness)||float.IsInfinity(p.lightness)||float.IsNaN(p.saturation)||float.IsInfinity(p.saturation))return false;
            if(ids==null||ids.Length==0)return values.Length==5;
            if(ids.Length!=values.Length)return false;
            var used=new System.Collections.Generic.HashSet<string>();
            foreach(var id in ids)if(string.IsNullOrEmpty(id)||!used.Add(id))return false;
            return true;
        }
        public static readonly string[] PartNames={"벽","지붕·차양","창문","식생","바닥 블록"};
    }
}

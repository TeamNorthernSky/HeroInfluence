// JC | 2026-11-05 | 기존 원경 편집 기능을 폴리싱에서 재사용합니다. Misc 원본 작업본과 참조는 보존합니다.
using System;
using System.Collections.Generic;
using UnityEngine;
namespace JC.BackgroundEditing
{
    // 로컬 작업 씬 전용. 내보낸 결과에는 이 컴포넌트가 포함되지 않습니다.
    public sealed class BackgroundDocument : MonoBehaviour
    {
        [Serializable] public struct Cell { public Vector2Int position; public int kind; }
        [Serializable] public struct Cut { public Vector2 min,max; }
        [Tooltip("읽기 전용으로 보존한 배경 원본입니다.")] public GameObject baseline;
        [Tooltip("베이크할 실내 무대입니다.")] public GameObject stage;
        [Tooltip("그리드에서 직접 칠한 지형입니다. 0 도로, 1 인도, 2 풀밭, 3 수로.")] public List<Cell> cells=new List<Cell>();
        [Tooltip("원본 지형을 대체하는 영역입니다.")] public List<Cut> cuts=new List<Cut>();
        [Tooltip("작업본 전용 에셋 저장 경로입니다.")] public string assetFolder;
        [Tooltip("이 영역 밖의 브러시 입력을 막습니다.")] public bool limitArea=true;
        [Tooltip("편집 가능 영역의 좌하단 그리드입니다.")] public Vector2Int editMin=new Vector2Int(18,32);
        [Tooltip("편집 가능 영역의 우상단 그리드입니다.")] public Vector2Int editMax=new Vector2Int(95,55);
        [Tooltip("건물 선택/이동을 잠급니다.")] public bool lockBuildings;
        [Tooltip("미리보기 시각입니다. 배치 결과에는 영향을 주지 않습니다.")] [Range(0,24)] public float hour=8.5f;
        [Tooltip("이 배경의 고층건물 창문은 원본 모델의 청색을 유지합니다. 공용 색상 프로필은 바꾸지 않습니다.")] public bool blueHighWindows=true;
        [Tooltip("켜면 칠할 때마다 전체 드레싱을 생성합니다. 끄면 지형을 빠르게 확인하고 드레싱 다시 생성 버튼으로 마감합니다.")] public bool dressWhilePainting;
        [Tooltip("켜면 저장된 편집 경계 밖 지형을 생성하지 않습니다.")] public bool cropOutput;
        [Tooltip("차선을 생성하지 않을 주차장 영역입니다.")] public List<Cut> parkingAreas=new List<Cut>();
        [Tooltip("편집 경계 바깥에 별도로 배치하는 산 배경입니다.")] public GameObject borderMountains;
        [Tooltip("전투 캐릭터에 대한 실내와 도시 배경의 전체 배율입니다. 전투 슬롯과 캐릭터 크기는 변경하지 않습니다.")][Range(.25f,2f)] public float stageScale=1f;
        [Tooltip("축소한 무대를 전투 배치에 맞추는 위치 보정입니다.")] public Vector3 stageOffset;
        [Tooltip("실내 배율과 별도로 창밖 도시의 지면 높이를 지정합니다.")] public bool overrideCityAltitude;
        [Tooltip("전투 공간 기준 도시 지면 높이입니다. 낮출수록 고층 전망이 됩니다.")] public float cityAltitude=-24;
        [Tooltip("실내 앞쪽 모서리 밖으로 도시 지면이 노출되지 않도록 보완한 바닥입니다.")] public Mesh foregroundFloor;
        [Tooltip("창틀을 제거한 실내 기둥 메시입니다. 원본 모델은 변경하지 않습니다.")] public Mesh windowlessPillar;
        // JC | 2026-11-05 | 최신 월드 가져오기에서는 편집하지 않은 지형/차선/장식을 재생성하지 않습니다.
        [Tooltip("최신 월드에서 가져온 외형을 편집 구간 밖에서 그대로 보존합니다.")] public bool preserveWorld;
        public Transform Buildings=>transform.Find("건물 배치");
        public Transform Terrain=>transform.Find("지형 결과");
    }
}

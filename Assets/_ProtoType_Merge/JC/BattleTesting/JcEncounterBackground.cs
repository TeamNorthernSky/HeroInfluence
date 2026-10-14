using System;
using UnityEngine;

namespace JC.BattleTesting
{
    [Serializable]
    public sealed class JcBackgroundObjectAdjustment
    {
        [Tooltip("추출 배경 내부의 오브젝트 경로입니다. 재추출해도 같은 원본 객체는 같은 이름을 사용합니다.")]
        public string path;
        [Tooltip("이 물체를 전투 배경에서만 숨깁니다.")]
        public bool hidden;
        [Tooltip("추출 원위치에서 추가할 이동량입니다. 배경 확대·회전 전 좌표입니다.")]
        public Vector3 offset;
    }

    [CreateAssetMenu(menuName = "JC/전투 테스트/인카운터 배경")]
    public sealed class JcEncounterBackground : ScriptableObject
    {
        [Tooltip("추출할 탐사씬의 프로젝트 경로입니다. 저장된 상태를 읽습니다.")]
        public string sourceScene = "Assets/_ProtoType_Merge/Scenes/DHScene_3.unity";
        [Tooltip("탐사씬의 인카운터 원본 이름입니다. Clone 접미사 없이 지정합니다.")]
        public string encounterKey;
        [Tooltip("원본 인카운터 중심에서 추출할 X/Z 반경입니다. 걸쳐 있는 물체·바닥 청크는 통째로 포함합니다.")]
        public Vector2 captureHalfSize = new Vector2(12, 12);
        [Tooltip("정적 외형만 담은 생성 프리팹입니다. 추출 버튼으로 갱신하며 직접 수정하지 않습니다.")]
        public GameObject visualPrefab;
        [Tooltip("도로 폭을 전투 3열 기준으로 재구성합니다. 탐사 셀 데이터는 사용하지 않습니다. 변경 후 배경 재추출을 실행합니다.")]
        public bool fitBattleRoad;
        [Tooltip("확대·회전한 원본 외형에서 측정한 도로의 왼쪽/오른쪽 X 경계입니다.")]
        public Vector2 sourceRoadEdges = new Vector2(-4.5f, 1.5f);
        [Tooltip("전투 3열 전체 폭과 양쪽 여유를 포함한 도로 폭입니다. 기본 11.5는 3열 약 9.9에 여유 1.6을 더한 값입니다.")]
        [Min(1)] public float battleRoadWidth = 11.5f;
        [Tooltip("도로 바깥 건물·산·소품까지 확보하는 추가 여유입니다. 건물 크기를 늘리지 않고 바깥으로 옮깁니다.")]
        [Min(0)] public float roadsideClearance = 5;
        [Tooltip("도로 폭 보정을 적용한 전투 전용 외형입니다. 생성 도구가 갱신합니다.")]
        public GameObject fittedVisualPrefab;
        [Tooltip("보도·횡단보도·야지 디테일 예시를 사용합니다. 끄면 기존 추출 배경으로 비교할 수 있습니다.")]
        public bool useDetailBackground;
        [Tooltip("디테일 부품으로 보강한 배경입니다. 환경 부품 생성 도구로 갱신합니다.")]
        public GameObject detailVisualPrefab;
        [Tooltip("전투씬에서 배경 중심을 옮기는 값입니다. 카메라·진형은 움직이지 않습니다.")]
        public Vector3 battleOffset;
        [Tooltip("탐사 거리와 전투 진형 크기를 맞추는 균일 배율입니다.")]
        [Min(.1f)] public float scale = 3;
        [Tooltip("도로 방향을 전투 진형에 맞추는 Y축 회전입니다.")]
        public float yaw;
        [Tooltip("인카운터 프리팹에 포함된 차단물·피켓 등의 정적 소품을 표시합니다. 인물·손에 든 장비·아이콘은 추출에서 제외됩니다.")]
        public bool showEncounterProps = true;
        [Tooltip("개별 물체의 숨김·이동 보정입니다. 원본 탐사씬이나 공유 모델에는 영향을 주지 않습니다.")]
        public JcBackgroundObjectAdjustment[] adjustments = Array.Empty<JcBackgroundObjectAdjustment>();
        [Tooltip("마지막 추출 결과입니다. 포함·제외 목록과 겹침 후보를 확인할 때 사용합니다.")]
        [TextArea(3, 12)] public string extractionReport;

        public GameObject CreateInstance(Transform owner)
        {
            if (visualPrefab == null) throw new InvalidOperationException(encounterKey + ": 배경을 먼저 추출해 주세요.");
            if (fitBattleRoad && fittedVisualPrefab == null) throw new InvalidOperationException(encounterKey + ": 도로 보정 배경을 재추출해 주세요.");
            var root = Instantiate(fitBattleRoad && useDetailBackground && detailVisualPrefab != null ? detailVisualPrefab : fitBattleRoad ? fittedVisualPrefab : visualPrefab, owner);
            root.name = "JC_EncounterBackground_" + encounterKey;
            root.transform.SetPositionAndRotation(fitBattleRoad ? Vector3.zero : battleOffset, fitBattleRoad ? Quaternion.identity : Quaternion.Euler(0, yaw, 0));
            root.transform.localScale = Vector3.one * (fitBattleRoad ? 1 : scale);
            var props = root.transform.Find("EncounterProps");
            if (props != null) props.gameObject.SetActive(showEncounterProps);
            foreach (var adjustment in adjustments)
            {
                if (adjustment == null || string.IsNullOrEmpty(adjustment.path)) continue;
                var target = root.transform.Find(adjustment.path);
                if (target == null) { Debug.LogWarning(encounterKey + ": 보정 대상 없음: " + adjustment.path); continue; }
                target.localPosition += fitBattleRoad ? Quaternion.Euler(0, yaw, 0) * (adjustment.offset * scale) : adjustment.offset;
                if (adjustment.hidden) target.gameObject.SetActive(false);
            }
            var cameraProfile=Resources.Load<JcBattleCameraProfile>("JC_BattleCamera_Default");
            Vector3[] views=null;
            if(cameraProfile!=null){cameraProfile.viewA.Resolve(new Vector3(0,.3f,-.01f),out var a,out _,out _);cameraProfile.viewB.Resolve(new Vector3(0,.3f,-.01f),out var b,out _,out _);views=new[]{a,b};}
            // 기존 디테일 프리팹에는 이미 자연 소품이 있으므로 중복 산포하지 않습니다.
            JcBattleSurfaceDressing.Apply(root,0,views,!(fitBattleRoad&&useDetailBackground&&detailVisualPrefab!=null));
            return root;
        }
    }
}

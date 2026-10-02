using UnityEngine;
using JC.Indicators;
using UnityEngine.Rendering;

namespace JC.Tutorial
{
    [DisallowMultipleComponent, AddComponentMenu("JC Tutorial/이동 목표 표식")]
    public sealed class JcTutorialWorldMarker : MonoBehaviour
    {
        [Tooltip("파란 바닥과 입체 화살표를 그리는 전용 셰이더입니다. 씬 참조로 빌드에 포함합니다.")] public Shader indicatorShader;
        [Min(.1f), Tooltip("타일 한 칸에 대한 바닥 표식의 크기입니다. 1이면 한 칸입니다.")] public float size = .88f;
        [Min(0), Tooltip("지면 위 바닥 높이(월드 단위)입니다. 기본 이동 표식보다 낮게 배치합니다.")] public float height = .012f;
        [ColorUsage(true,true), Tooltip("두께가 있는 바닥 블록의 색상입니다. 불투명 메시이므로 알파값은 사용하지 않습니다.")] public Color floorColor = new Color(.025f,.30f,1f,.95f);
        [ColorUsage(true,true), Tooltip("화살표 외곽의 노란색입니다.")] public Color arrowColor = new Color(1f,.76f,.025f,1);
        [ColorUsage(true,true), Tooltip("화살표 중앙의 파란색 포인트입니다.")] public Color pointColor = new Color(.02f,.36f,1f,1);
        [Min(0), Tooltip("발광 밝기 배율입니다. 0이면 검게 보이며, 화면의 블룸 설정에 따라 번짐이 달라집니다.")] public float intensity = 1.5f;
        [Min(.1f), Tooltip("화살표 너비의 타일 크기 비율입니다.")] public float arrowSize = .6f;
        [Min(.01f), Tooltip("화살표 두께의 타일 크기 비율입니다.")] public float arrowThickness = .10f;
        [Min(0), Tooltip("화살표 중심의 지면 위 높이(타일 크기 비율)입니다.")] public float arrowHeight = .9f;
        [Tooltip("화살표의 세로축 회전 속도(도/초)입니다. 0이면 정지하고 음수이면 반대 방향입니다.")] public float rotationSpeed = 36;
        [Min(0), Tooltip("화살표 상하 왕복의 한쪽 진폭(타일 크기 비율)입니다. 0이면 정지합니다.")] public float bobAmplitude = .10f;
        [Min(.1f), Tooltip("바닥과 화살표가 각각 상하로 한 번 왕복하는 공통 시간(초)입니다. 게임 시간 배율과 무관합니다.")] public float pulsePeriod = 3f;
        [Min(.01f), Tooltip("바닥 블록의 Y 두께(타일 크기 비율)입니다. 최저 위치에서도 지면 위에 놓입니다.")] public float floorThickness = .065f;
        [Min(.001f), Tooltip("바닥과 화살표의 앞뒤 가장자리 베벨 크기입니다. 메시 기본 크기 기준이며 두께에 맞게 제한됩니다.")] public float bevel = .035f;
        [Min(.001f), Tooltip("화살표 윤곽의 날카로운 꼭짓점을 잘라내는 길이입니다. 메시 기본 너비 1 기준입니다.")] public float cornerCut = .045f;
        [Min(0), Tooltip("바닥이 최저 위치에서 올라가는 전체 왕복 폭(타일 크기 비율)입니다. 0이면 정지합니다.")] public float floorBobTravel = .035f;
        [Range(0, 1), Tooltip("화살표에 대한 바닥 왕복의 위상차(주기 비율)입니다. 0.25이면 1/4주기 어긋나며 공통 주기는 유지합니다.")] public float floorBobPhase = .25f;
        [Min(.1f), Tooltip("바닥 위 스윕 샤인이 한 번 지나가는 시간(초)입니다.")] public float sweepPeriod = 2.4f;
        [Min(0), Tooltip("바닥 스윕 샤인의 밝기입니다. 0이면 스윕을 끕니다.")] public float sweepIntensity = 1.6f;
        [Tooltip("기존 이동 인디케이터입니다. 동일한 메시 생성기와 현재 설정으로 바닥 형상을 공유합니다.")]
        public JcMovementIndicatorController movementIndicator;
        private JcMovementIndicatorSettings builtSettings;
        private float builtCellSize = -1;
        private GameObject visual, arrow;
        private Mesh floorMesh, arrowMesh;
        private Material material;
        private MeshRenderer floorRenderer, arrowRenderer;
        private MaterialPropertyBlock block;
        private float builtThickness = -1;
        private float builtArrowSize = -1;
        private Color builtArrow, builtPoint;
        private float builtFloorThickness = -1, builtBevel = -1, builtCornerCut = -1;

        public void Present(bool visible, Vector3 position, float cellSize, float time, bool showFloor = true)
        {
            if (!visible || !isActiveAndEnabled || indicatorShader == null) { if (visual != null) visual.SetActive(false); return; }
            if (visual == null) Build();
            if (builtThickness != arrowThickness || builtArrowSize != arrowSize || builtArrow != arrowColor || builtPoint != pointColor || builtFloorThickness != floorThickness || builtBevel != bevel || builtCornerCut != cornerCut) BuildMeshes();
            visual.SetActive(true); visual.transform.position = position + Vector3.up * height;
            visual.transform.localScale = Vector3.one * Mathf.Max(.01f,cellSize);

            float phase = time * Mathf.PI * 2 / Mathf.Max(.1f, pulsePeriod);
            var style = movementIndicator != null ? movementIndicator.Settings : JcMovementIndicatorSettings.Default.ForCellSize(cellSize);
            if(builtCellSize != cellSize || !builtSettings.Equals(style)) {
                new JcIndicatorMarkerMesh().Build(floorMesh,style,cellSize*style.markerSize);
                builtSettings=style;builtCellSize=cellSize;
            }
            floorRenderer.enabled=showFloor;
            floorRenderer.transform.localScale=Vector3.one*style.markerSize;
            floorRenderer.transform.localPosition=Vector3.up*(floorBobTravel*(.5f+.5f*Mathf.Sin(phase+floorBobPhase*Mathf.PI*2)));
            arrow.transform.localPosition = Vector3.up * (arrowHeight + bobAmplitude * Mathf.Sin(phase));
            arrow.transform.localRotation = Quaternion.Euler(0, time * rotationSpeed, 0);
            arrow.transform.localScale = Vector3.one * arrowSize;
            block.Clear(); block.SetColor("_Color",floorColor); block.SetFloat("_Mode",0); block.SetFloat("_Clock",time); block.SetFloat("_Intensity",intensity);
            block.SetFloat("_SweepPeriod", sweepPeriod); block.SetFloat("_SweepIntensity", sweepIntensity);
            floorRenderer.SetPropertyBlock(block);
            block.SetFloat("_Mode",1); arrowRenderer.SetPropertyBlock(block);
        }
        private MeshRenderer Part(string name, Mesh mesh, Transform parent)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave, layer = gameObject.layer };
            go.transform.SetParent(parent,false); go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = material; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            return r;
        }
        private void Build()
        {
            visual = new GameObject("목표 표식 (자동 생성)") { hideFlags = HideFlags.HideAndDontSave, layer = gameObject.layer };
            visual.transform.SetParent(transform,false);
            material = new Material(indicatorShader) { hideFlags = HideFlags.HideAndDontSave };
            block = new MaterialPropertyBlock();
            floorMesh = new Mesh { name = "TutorialGoalFloor", hideFlags = HideFlags.HideAndDontSave };
            floorRenderer = Part("청색 지면 타일",floorMesh,visual.transform);
            arrowMesh = new Mesh { name = "TutorialGoalArrow", hideFlags = HideFlags.HideAndDontSave };
            arrowRenderer = Part("황청 회전 화살표",arrowMesh,visual.transform); arrow = arrowRenderer.gameObject;
            BuildMeshes();
        }
        private void BuildMeshes()
        {
            builtCellSize = -1;
            var builder = new JcTutorialSolidMesh();
            var outer = new[] { new Vector2(-.5f,.38f), new Vector2(-.2f,.29f), new Vector2(0,.4f), new Vector2(.2f,.29f), new Vector2(.5f,.38f), new Vector2(0,-.52f) };
            float depth = Mathf.Max(.01f, arrowThickness) / Mathf.Max(.1f, arrowSize);
            builder.Prism(JcTutorialSolidMesh.Chamfer(outer, Mathf.Max(.001f, cornerCut)), depth, bevel, arrowColor);
            // 앞뒤 모두 파란 인레이가 보이되, 뒷면이 앞면을 뚫고 보이지 않도록 독립된 닫힌 메시로 만든다.
            var point = new[] { new Vector2(-.22f,.17f), new Vector2(.22f,.17f), new Vector2(0,-.24f) };
            // 밝은 금색 받침이 보석 둘레를 감싸며 앞뒤 모두 돌출된 베벨이 빛을 받는다.
            var bezel = new Vector2[point.Length]; for(int i=0;i<point.Length;i++)bezel[i]=point[i]*1.14f;
            builder.Prism(JcTutorialSolidMesh.Chamfer(bezel, .025f), depth + .035f, .022f, Color.Lerp(arrowColor,Color.white,.32f));
            builder.Prism(JcTutorialSolidMesh.Chamfer(point, .018f), depth + .09f, .09f, pointColor);
            builder.Apply(arrowMesh);
            builtThickness = arrowThickness; builtArrowSize = arrowSize; builtArrow = arrowColor; builtPoint = pointColor;
            builtFloorThickness = floorThickness; builtBevel = bevel; builtCornerCut = cornerCut;
        }
        private void OnDisable()
        {
            Dispose(visual); Dispose(floorMesh); Dispose(arrowMesh); Dispose(material);
            visual=null; floorMesh=null; arrowMesh=null; material=null; builtThickness=-1;
        }
        private static void Dispose(Object value) { if(value==null)return; if(Application.isPlaying)Destroy(value);else DestroyImmediate(value); }
    }
}

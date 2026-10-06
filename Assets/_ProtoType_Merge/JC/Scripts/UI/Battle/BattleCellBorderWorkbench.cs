using UnityEngine;

/// <summary>설정 초안을 미리보기로 전달합니다. 프로필 쓰기는 Editor의 명시적 저장 버튼만 수행합니다.</summary>
[ExecuteAlways]
public sealed class BattleCellBorderWorkbench : MonoBehaviour
{
    [Tooltip("전투씬과 공유하는 저장 프로필입니다.")] public BattleCellBorderProfile profile;
    [Tooltip("표시용 기본 프리팹입니다. 외형값은 아래 초안이 결정합니다.")] public BattleCellBorderVisual previewPrefab;
    [Tooltip("현재 편집할 상태입니다.")] public BattleCellVisualState selectedState = BattleCellVisualState.CurrentTurn;
    [Tooltip("프로필에 저장하기 전의 조절값입니다.")] public BattleCellBorderConfiguration draft = new BattleCellBorderConfiguration();
    [Tooltip("하단 전환 검수 표시의 현재 상태입니다.")] public BattleCellVisualState transitionState = BattleCellVisualState.Unavailable;
    [Tooltip("하단 표시를 빨강 확정 대상과 선택 불가 사이에서 자동 전환합니다.")] public bool autoSwitch;
    [Min(.5f), Tooltip("자동 전환 사이의 시간(초)입니다.")] public float switchInterval = 2f;
    private readonly BattleCellBorderVisual[] views = new BattleCellBorderVisual[5];
    private readonly BattleCellBorderMotion[] motion = new BattleCellBorderMotion[5];
    private static readonly Vector3[] Positions = {new Vector3(-5.1f,0,2),new Vector3(-1.7f,0,2),new Vector3(1.7f,0,2),new Vector3(5.1f,0,2),new Vector3(0,0,-2)};
    private BattleCellBorderVisual builtPrefab;

    private void OnEnable()
    {
        // 편집 씬을 다시 열거나 플레이를 끝내면 저장 프로필을 기준으로 시작합니다.
        // 플레이 진입 시에는 인스펙터의 현재 초안을 그대로 검수합니다.
        if (profile != null && (!Application.isPlaying || draft == null)) LoadProfile();
    }

    public void LoadProfile() { if(profile!=null)draft=profile.settings.Copy(); }
    public void UnifyShape() => draft.UnifyShape(selectedState);
    public BattleCellBorderVisual Preview(int index) => views[index];
    private void Update()
    {
        double clock=Time.unscaledTimeAsDouble;
#if UNITY_EDITOR
        if(!Application.isPlaying)clock=UnityEditor.EditorApplication.timeSinceStartup;
#endif
        Sample(clock);
    }
    public void Sample(double clock)
    {
        if(previewPrefab==null || draft==null)return;
        if(builtPrefab!=previewPrefab){Clear();builtPrefab=previewPrefab;}
        for(int i=0;i<views.Length;i++)
        {
            if(views[i]==null)
            {
                views[i]=Instantiate(previewPrefab,transform,false);
                views[i].gameObject.hideFlags=HideFlags.DontSave;
                views[i].name=i==4?"상태 전환 검수":"상태 미리보기 "+(BattleCellVisualState)(i+1);
                views[i].SetDriven(true);motion[i]=new BattleCellBorderMotion();
            }
            BattleCellVisualState state=i<4?(BattleCellVisualState)(i+1):autoSwitch && Application.isPlaying?
                ((long)(clock/Mathf.Max(.5f,switchInterval))%2==0?BattleCellVisualState.ConfirmedArea:BattleCellVisualState.Unavailable):transitionState;
            if(state==BattleCellVisualState.Hidden)state=BattleCellVisualState.Unavailable;
            views[i].transform.localPosition=Positions[i]+Vector3.up*motion[i].Evaluate(state,draft,clock);
            views[i].RenderProfile(draft,state,1,(float)clock,transform.TransformPoint(Positions[i]).y);
        }
    }
    private void OnDisable() => Clear();
    private void Clear()
    {
        for(int i=0;i<views.Length;i++)
        {
            if(views[i]!=null){views[i].gameObject.SetActive(false);if(Application.isPlaying)Destroy(views[i].gameObject);else DestroyImmediate(views[i].gameObject);}
            views[i]=null;motion[i]=null;
        }
    }
}

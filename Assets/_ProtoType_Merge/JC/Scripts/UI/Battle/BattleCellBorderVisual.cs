using UnityEngine;

public enum BattleCellVisualState { Hidden, CurrentTurn, Selectable, ConfirmedArea, Unavailable }

/// <summary>대상 판정을 갖지 않는 셀 테두리 프리팹입니다. 테두리와 채움의 외형만 담당합니다.</summary>
[ExecuteAlways, DisallowMultipleComponent]
public sealed class BattleCellBorderVisual : MonoBehaviour
{
    [Header("크기와 테두리")]
    [Tooltip("셀 기준 가로·세로 크기입니다. Transform 배율 대신 이 값을 바꾸면 테두리 두께가 유지됩니다.")]
    public Vector2 size = new Vector2(3.3f, 3.3f);
    [Tooltip("셀의 각 변에서 표시를 안쪽으로 줄이는 여백입니다. 0.1이면 양쪽에서 각각 0.1씩 줄어 인접 표시 사이에 총 0.2의 여백이 생깁니다. 테두리 두께는 유지됩니다.")]
    [Min(0)] public float cellPadding = .1f;
    [Tooltip("네 모서리를 대각선으로 잘라내는 길이입니다. 0이면 모따기가 없는 사각형이 됩니다.")]
    [Min(0)] public float cornerCut = .25f;
    [Tooltip("바깥 은회색 테두리의 두께입니다. 셀 로컬 좌표 단위입니다.")]
    [Min(.001f)] public float outerWidth = .045f;
    [Tooltip("안쪽 대상 구분색 테두리의 두께입니다. 셀 로컬 좌표 단위입니다.")]
    [Min(.001f)] public float innerWidth = .11f;
    [Tooltip("바깥 테두리 색과 불투명도입니다.")]
    public Color outerColor = new Color(.86f, .9f, .94f, 1f);
    [Tooltip("안쪽 대상 구분색과 불투명도입니다.")]
    public Color innerColor = new Color(1f, .8f, .08f, 1f);
    [Tooltip("이 표시의 두 테두리에 스윕을 허용합니다. 선택 불가 프리팹은 기본적으로 끕니다.")]
    public bool allowSweep = true;

    [Header("내부 채움")]
    [Tooltip("내부 채움을 표시합니다. 다른 상태도 필요할 때 켤 수 있습니다.")]
    public bool useFill;
    [Tooltip("내부 채움 색입니다. 알파는 아래 채움 불투명도와 곱해집니다.")]
    public Color fillColor = Color.red;
    [Tooltip("내부 채움 불투명도입니다. CellBox의 테두리 불투명도와 독립적이며 0.25는 25%입니다.")]
    [Range(0, 1)] public float fillOpacity = .25f;
    [Tooltip("선택적인 내부 문양입니다. 비워 두면 단색으로 채웁니다. 텍스처의 알파도 적용됩니다.")]
    public Texture2D fillTexture;

    [Header("프리팹 구성")]
    [Tooltip("바깥 테두리 메시 렌더러입니다.")]
    public MeshRenderer outerBorder;
    [Tooltip("안쪽 테두리 메시 렌더러입니다.")]
    public MeshRenderer innerBorder;
    [Tooltip("내부 채움 메시 렌더러입니다.")]
    public MeshRenderer interior;

    [Tooltip("외형과 움직임의 정본 프로필입니다. 설정 씬의 조절 오브젝트에서 저장합니다.")]
    public BattleCellBorderProfile profile;
    [Tooltip("이 프리팹이 표시하는 상태입니다.")]
    public BattleCellVisualState presentationState = BattleCellVisualState.CurrentTurn;
    [Tooltip("새 안쪽 보조선 렌더러입니다. 외곽선과 같은 색을 사용합니다.")]
    public MeshRenderer innerOutline;

    private MaterialPropertyBlock block;
    private bool driven;
    private BattleCellBorderShape builtShape;
    private bool builtOuter, builtInner, hasGeometry;
    private readonly Mesh[] meshes = new Mesh[4];
    private readonly Mesh[] originalMeshes = new Mesh[4];
    private readonly MeshFilter[] filters = new MeshFilter[4];
    private BattleCellBorderConfiguration fallback;
    public void SetDriven(bool value) => driven = value;

    private void Update()
    {
        if (driven) return;
        float clock = Time.unscaledTime;
#if UNITY_EDITOR
        if (!Application.isPlaying) clock = (float)UnityEditor.EditorApplication.timeSinceStartup;
#endif
        Render(null, 1f, clock);
    }
    public void Render(BattleCellVisualSettings settings, float borderAlpha, float clock)
    {
        var config = settings != null && settings.profile != null ? settings.profile.settings : profile != null ? profile.settings : null;
        if (config == null)
        {
            if (fallback == null) fallback = new BattleCellBorderConfiguration();
            var style = fallback.For(presentationState);
            style.shape.size=size; style.shape.padding=cellPadding; style.shape.cornerRadius=cornerCut;
            style.shape.outerWidth=outerWidth; style.shape.mainWidth=innerWidth;
            style.outlineColor=outerColor;style.mainColor=innerColor;style.allowSweep=allowSweep;
            style.useFill=useFill;style.fillColor=fillColor;style.fillOpacity=fillOpacity;style.fillTexture=fillTexture;
            config=fallback;
        }
        RenderProfile(config,presentationState,borderAlpha,clock);
    }
    private float fillOcclusionHeight;
    public void RenderProfile(BattleCellBorderConfiguration config, BattleCellVisualState state, float borderAlpha, float clock, float? groundHeight = null)
    {
        if(config==null)return;
        // 왕복 운동과 별개인 바닥 높이입니다. 아주 작은 여유로 바닥 자체와의 깊이 충돌을 피합니다.
        fillOcclusionHeight=(groundHeight ?? transform.position.y)+.01f;
        var style=config.For(state);
        if(!hasGeometry || !builtShape.Equals(style.shape) || builtOuter!=style.showOuter || builtInner!=style.showInner)
            Rebuild(style);
        if(block==null)block=new MaterialPropertyBlock();
        if(outerBorder!=null)outerBorder.enabled=style.showOuter;
        if(innerOutline!=null)innerOutline.enabled=style.showInner;
        if(interior!=null)interior.enabled=style.useFill && style.fillOpacity>0 && style.fillColor.a>0;
        Paint(outerBorder,style.outlineColor,borderAlpha,false,style,config,clock);
        Paint(innerBorder,style.mainColor,borderAlpha,false,style,config,clock);
        Paint(innerOutline,style.outlineColor,borderAlpha,false,style,config,clock);
        Paint(interior,style.fillColor,style.fillOpacity,true,style,config,clock);
    }
    private void Rebuild(BattleCellBorderStyle style)
    {
        var shape=style.shape;
        float padding=Mathf.Max(0,shape.padding);
        Vector2 dimensions=new Vector2(Mathf.Max(.1f,shape.size.x-2*padding),Mathf.Max(.1f,shape.size.y-2*padding));
        float budget=Mathf.Min(dimensions.x,dimensions.y)*.45f;
        float outer=style.showOuter?Mathf.Max(.001f,shape.outerWidth):0;
        float main=Mathf.Max(.001f,shape.mainWidth);
        float inner=style.showInner?Mathf.Max(.001f,shape.innerWidth):0;
        float scale=Mathf.Min(1,budget/(outer+main+inner));outer*=scale;main*=scale;inner*=scale;
        float radius=Mathf.Clamp(shape.cornerRadius,0,Mathf.Min(dimensions.x,dimensions.y)*.5f);
        BuildPart(0,outerBorder,dimensions,radius,Mathf.Max(.001f,outer),shape,false);
        BuildPart(1,innerBorder,dimensions-Vector2.one*2*outer,Mathf.Max(0,radius-outer),main,shape,false);
        BuildPart(2,innerOutline,dimensions-Vector2.one*2*(outer+main),Mathf.Max(0,radius-outer-main),Mathf.Max(.001f,inner),shape,false);
        BuildPart(3,interior,dimensions-Vector2.one*2*(outer+main+inner),Mathf.Max(0,radius-outer-main-inner),.001f,shape,true);
        if(interior!=null)interior.transform.localPosition=new Vector3(0,Mathf.Max(.001f,shape.depth)*.5f,0);
        builtShape=shape;builtOuter=style.showOuter;builtInner=style.showInner;hasGeometry=true;
    }
    private void BuildPart(int index, MeshRenderer renderer,Vector2 dimensions,float radius,float width,BattleCellBorderShape shape,bool fill)
    {
        if(renderer==null)return;
        if(meshes[index]==null)
        {
            filters[index]=renderer.GetComponent<MeshFilter>();
            if(filters[index]==null)return;
            originalMeshes[index]=filters[index].sharedMesh;
            meshes[index]=new Mesh{name="Battle border procedural "+index,hideFlags=HideFlags.HideAndDontSave};
            filters[index].sharedMesh=meshes[index];
        }
        BattleCellBorderMesh.Build(meshes[index],dimensions,radius,width,shape.depth,shape.curveSegments,fill);
        renderer.ResetLocalBounds();
    }
    private void Paint(MeshRenderer target,Color color,float alpha,bool fill,BattleCellBorderStyle style,BattleCellBorderConfiguration config,float clock)
    {
        if(target==null)return;
        block.Clear();color.a*=Mathf.Clamp01(alpha);block.SetColor("_BaseColor",color);
        block.SetFloat("_IsFill",fill?1:0);
        block.SetFloat("_FillOcclusionHeight",fillOcclusionHeight);
        block.SetTexture("_BaseMap",fill && style.fillTexture!=null?style.fillTexture:Texture2D.whiteTexture);
        block.SetFloat("_SweepEnabled",!fill && style.allowSweep && config.useSweep?1:0);
        block.SetColor("_SweepColor",config.sweepColor);block.SetFloat("_SweepIntensity",config.sweepIntensity);
        block.SetFloat("_SweepWidth",config.sweepWidth);block.SetFloat("_SweepSoftness",config.sweepSoftness);
        block.SetFloat("_SweepTilt",config.sweepTilt);block.SetFloat("_SweepDuration",config.sweepDuration);
        block.SetFloat("_SweepInterval",config.sweepInterval);block.SetFloat("_SweepReverse",config.reverseSweep?1:0);
        block.SetFloat("_SweepTime",clock);
        // 세 테두리에서 같은 셀 좌표로 스윕 위치를 맞춥니다.
        float pad=Mathf.Max(0,style.shape.padding);
        block.SetVector("_Size",new Vector4(Mathf.Max(.1f,style.shape.size.x-2*pad),Mathf.Max(.1f,style.shape.size.y-2*pad),0,0));
        target.SetPropertyBlock(block);
    }
    private void OnDisable()
    {
        for(int i=0;i<meshes.Length;i++)
        {
            if(meshes[i]==null)continue;
            if(filters[i]!=null)filters[i].sharedMesh=originalMeshes[i];
            if(Application.isPlaying)Destroy(meshes[i]);else DestroyImmediate(meshes[i]);
            meshes[i]=null;
        }
        hasGeometry=false;
    }
}

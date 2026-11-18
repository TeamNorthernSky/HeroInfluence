using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
#endif

/// <summary>전투씬의 히어로에만 임시 재질을 적용합니다. 원본 FBX 재질은 수정하지 않습니다.</summary>
[ExecuteAlways, DisallowMultipleComponent]
public sealed class JcBattleCharacterLook : MonoBehaviour
{
    [Tooltip("외형값을 저장하는 프로필입니다. 씬의 대상 오브젝트 연결은 프로필에 저장하지 않습니다.")]
    public JcBattleLookProfile profile;
    // JC | 2026-11-18 | 다른 외형 조절값과 동일하게 초안만 변경하고 명시적인 저장 때 프로필에 보존합니다.
    public JcAmplifierSurfaceSettings amplifierSurface = new JcAmplifierSurfaceSettings();
    [Tooltip("보정할 히어로 진형 루트입니다. 이 아래의 불투명 URP Lit 재질만 적용합니다.")]
    public Transform heroRoot;
    [Tooltip("적군 진형 루트입니다. 아래에 배치된 캐릭터와 전투 오브젝트에도 같은 보정을 적용합니다.")]
    public Transform enemyRoot;
    [Tooltip("진형 루트 밖에 배치된 전투 오브젝트가 있다면 지정합니다. 배경과 바닥은 넣지 않습니다.")]
    public Transform[] additionalObjectRoots = new Transform[0];
    [Range(0,2), Tooltip("캐릭터·전투 오브젝트 채도입니다. 1은 원색, 1.15는 색을 약간 더 선명하게 합니다.")]
    public float saturation = 1.15f;
    // JC | 2026-11-18 | 조명 모드를 바꾸지 않고 대상 복제 재질의 채도·밝기만 보정합니다. 시간대별 환경 프로필에는 저장하지 않습니다.
    [InspectorName("채도·밝기 보정 사용")] public bool colorAdjustmentEnabled = true;
    [Range(-2,2), InspectorName("밝기 EV"), Tooltip("0은 기존 밝기, -1은 절반, +1은 두 배입니다. 실제 조명과 그림자의 상대적 차이는 유지합니다.")]
    public float brightnessEv;
    [Tooltip("캐릭터용 부드러운 명암 셰이더입니다.")]
    public Shader characterShader;
    [Tooltip("편집 모드에서도 보정을 미리 봅니다. 끄면 원본 재질로 복구합니다.")]
    public bool previewInEditor = true;
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

    public JcBattleLookSettings CaptureSettings() => JsonUtility.FromJson<JcBattleLookSettings>(JsonUtility.ToJson(this));
    public void ApplySettings(JcBattleLookSettings value){if(value!=null)JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(value),this);RefreshLook();}
    public void LoadProfile(){if(profile!=null)ApplySettings(profile.settings);}

    private sealed class Entry
    {
        public Renderer renderer;
        public Material[] original, shown;
        public readonly List<Material> owned = new List<Material>();
        // JC | 2026-11-18 | 법선 보정은 렌더러별 임시 복사본에만 적용하며 종료 때 원본 메시 참조로 돌아갑니다.
        public MeshFilter meshFilter;
        public Mesh originalMesh, normalMesh, normalSource;
        public float normalBlend = -1;
    }
    private readonly Dictionary<Renderer,Entry> entries = new Dictionary<Renderer,Entry>();
    private double nextScan;
    private bool saving;
    private void OnEnable()
    {
        saving=false;
        if(profile!=null)JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(profile.settings),this);
        nextScan=0;
#if UNITY_EDITOR
        EditorSceneManager.sceneSaving += BeforeSave;
        EditorSceneManager.sceneSaved += AfterSave;
        EditorApplication.playModeStateChanged += OnPlayState;
        AssemblyReloadEvents.beforeAssemblyReload += BeforeReload;
#endif
    }
    private void Update()
    {
        if(saving)return;
        if(heroRoot==null || characterShader==null || (!Application.isPlaying && !previewInEditor)){Restore();return;}
        if(Time.realtimeSinceStartupAsDouble>=nextScan){Scan();nextScan=Time.realtimeSinceStartupAsDouble+.5;}
        foreach(var entry in entries.Values)
        {
            // JC | 2026-11-18 | 매 프레임 메시를 재계산하지 않고 설정이나 참조가 바뀐 경우에만 법선을 갱신합니다.
            UpdateNormals(entry);
            foreach(var m in entry.owned) UpdateMaterial(m,IsMechanical(entry.renderer),entry.renderer);
        }
    }
    public void RefreshLook(){nextScan=0;Update();}
    private void Scan()
    {
        var dead=new List<Renderer>();
        foreach(var pair in entries)if(pair.Key==null)dead.Add(pair.Key);
        foreach(var key in dead){DestroyOwned(entries[key]);entries.Remove(key);}
        var renderers=new HashSet<Renderer>();
        foreach(var root in new[]{heroRoot,enemyRoot})if(root!=null)foreach(var r in root.GetComponentsInChildren<Renderer>(true))renderers.Add(r);
        if(additionalObjectRoots!=null)foreach(var root in additionalObjectRoots)if(root!=null)foreach(var r in root.GetComponentsInChildren<Renderer>(true))renderers.Add(r);
        foreach(var r in renderers)
        {
            if(entries.ContainsKey(r) || r is ParticleSystemRenderer || r is SpriteRenderer)continue;
            bool extra=false;
            if(additionalObjectRoots!=null)foreach(var root in additionalObjectRoots)if(root!=null&&r.transform.IsChildOf(root)){extra=true;break;}
            if(!extra && r.GetComponentInParent<ASB.Work.BattleGrid.GridCell>()==null)continue;
            // 셀 Plane과 인디케이터에는 적용하지 않습니다.
            if(r.name=="Plane" || r.GetComponentInParent<BattleCellBorderVisual>()!=null)continue;
            var original=r.sharedMaterials;
#if UNITY_EDITOR
            // 이전 임시 재질이 플레이 전환 중 소멸한 경우, 원본 프리팹의 슬롯으로 복구합니다.
            if(!Application.isPlaying)
            {
                var source=PrefabUtility.GetCorrespondingObjectFromSource(r);
                if(source!=null){var originals=source.sharedMaterials;for(int i=0;i<original.Length&&i<originals.Length;i++)if(original[i]==null)original[i]=originals[i];r.sharedMaterials=original;}
            }
#endif
            var shown=(Material[])original.Clone();var e=new Entry{renderer=r,original=original,shown=shown};
            for(int i=0;i<original.Length;i++)
            {
                var src=original[i];if(src==null || src.shader==null || src.shader.name!="Universal Render Pipeline/Lit" || (src.HasProperty("_Surface") && src.GetFloat("_Surface")>.5f))continue;
                var copy=new Material(src){name=src.name+" [Battle Soft]",hideFlags=HideFlags.HideAndDontSave};copy.shader=characterShader;copy.renderQueue=src.renderQueue;UpdateMaterial(copy,IsMechanical(r),r);e.owned.Add(copy);shown[i]=copy;
            }
            if(e.owned.Count==0)continue;
            entries.Add(r,e);r.sharedMaterials=shown;
        }
    }
    public bool IsMechanical(Renderer renderer)
    {
        if(!useMechanicalMolding || renderer==null)return false;
        var unit=renderer.GetComponentInParent<BattleCharactor>();
        if(unit!=null && mechanicalUnitIds!=null)foreach(var id in mechanicalUnitIds)if(!string.IsNullOrEmpty(id)&&unit.TemplateIndex==id)return true;
        // 편집용 프리팹은 아직 전투 데이터가 초기화되지 않은 경우가 있습니다.
        for(var t=renderer.transform;t!=null && t!=heroRoot && t!=enemyRoot;t=t.parent)
            if(t.name.StartsWith("Unit_VillanDrone_")||t.name.StartsWith("Unit_VillanAmplifier_")||t.name.StartsWith("Unit_VillanWheel_"))
                if(mechanicalUnitIds!=null)foreach(var id in mechanicalUnitIds)if(!string.IsNullOrEmpty(id)&&(t.name.EndsWith("_"+id)||t.name.EndsWith("_"+id+"(Clone)")))return true;
        return false;
    }
    public bool IsPlayerBackFillTarget(Renderer renderer)
    {
        if (!renderer || !heroRoot || !renderer.transform.IsChildOf(heroRoot)) return false;
        var unit = renderer.GetComponentInParent<BattleCharactor>();
        if (!unit || !unit.IsPlayer) return false;
        return unit.TemplateIndex == "10001" || unit.TemplateIndex == "10002" || unit.TemplateIndex == "10003" || unit.TemplateIndex == "10004";
    }
    private void UpdateMaterial(Material m,bool mechanical,Renderer renderer)
    {
        // JC | 2026-11-18 | 정확한 몸체 메시와 증폭기 ID를 함께 검사하여 구체·드론·히어로에는 새 표면을 적용하지 않습니다.
        bool amplifier = amplifierSurface != null && amplifierSurface.enabled && IsAmplifierSurfaceTarget(renderer)
            && profile.amplifierMask;
        if(amplifier)
        {
            m.EnableKeyword("_JC_AMPLIFIER_SURFACE");
            m.SetTexture("_AmpMask",profile.amplifierMask);
            m.SetColor("_AmpBody",amplifierSurface.body);m.SetColor("_AmpTrim",amplifierSurface.trim);m.SetColor("_AmpAccent",amplifierSurface.accent);
            m.SetFloat("_AmpSmoothness",Mathf.Clamp01(amplifierSurface.smoothness));
            m.SetFloat("_AmpReflection",Mathf.Clamp01(amplifierSurface.reflection));
            m.SetFloat("_AmpOcclusion",Mathf.Clamp01(amplifierSurface.occlusion));
        }
        else m.DisableKeyword("_JC_AMPLIFIER_SURFACE");
        // JC | 2026-11-05 | 보스 환경 수명 안에서만 자체 밝기를 끄며, 씬/모드 종료 시 다음 갱신에 기존 캐릭터 표현으로 돌아갑니다.
        // JC | 2026-11-09 | 공통 조절기의 선택을 따르며 플레이어 4종 제한을 복제 재질에만 기록합니다.
        m.SetFloat("_BossLocalOnly",JC.BattleTesting.JcIndoorEnvironment.UsesLocalCharacterLighting(renderer)?1:0);
        // JC | 2026-11-12 | 기존 캐릭터 조명 모드 선택을 따르며 야외 복귀 시 IBL 변형도 해제합니다.
        if(m.GetFloat("_BossLocalOnly")>.5f) m.EnableKeyword("_JC_INDOOR_IBL_TARGET"); else m.DisableKeyword("_JC_INDOOR_IBL_TARGET");
        m.SetFloat("_JcPlayerBackFillTarget",IsPlayerBackFillTarget(renderer)?1:0);
        var texture=m.GetTexture("_BaseMap");bool neko=texture!=null && texture.name.Contains("cutecatgirl");
        var direction=keyDirection.sqrMagnitude>.001f?keyDirection.normalized:Vector3.up;
        m.SetVector("_KeyDirection",direction);m.SetColor("_KeyTint",keyTint);m.SetColor("_FillTint",fillTint);
        m.SetFloat("_ShadeFloor",mechanical?mechanicalShadeFloor:neko?nekomingShadeFloor:shadeFloor);m.SetFloat("_Wrap",mechanical?mechanicalLightWrap:lightWrap);m.SetFloat("_ShadowStrength",receivedShadow);
        m.SetFloat("_AlbedoLift",neko?nekomingAlbedoLift:albedoLift);m.SetFloat("_RimStrength",rimStrength);
        m.SetFloat("_Saturation",saturation);
        // JC | 2026-11-18 | 글로벌 셰이더/라이트는 건드리지 않습니다. 중립 비교는 초안 수치를 보존한 채 보정만 끕니다.
        m.SetVector("_CharacterColor",new Vector4(colorAdjustmentEnabled?1:0,Mathf.Pow(2,Mathf.Clamp(brightnessEv,-2,2)),0,0));
        // JC | 2026-11-18 | 불투명 몸체 복제 재질에 현재 시간대의 보스 응답만 연결합니다. 공통 채도/EV와 원본 재질·VFX는 바꾸지 않습니다.
        m.SetVector("_BossSurfaceResponse",JC.BattleTesting.JcIndoorEnvironment.BossSurfaceResponse(renderer));
        m.SetFloat("_MoldingStrength",mechanical?moldingStrength:0);m.SetFloat("_MoldingSharpness",moldingSharpness);
    }
    // JC | 2026-11-18 | 몰딩 토글과 독립적으로 동작하며 테스트/본게임의 동일한 40002·40003 몸체를 선택합니다.
    public bool IsAmplifierSurfaceTarget(Renderer renderer)
    {
        if(!profile || !profile.amplifierMesh || !renderer) return false;
        var filter=renderer.GetComponent<MeshFilter>();
        if(!filter) return false;
        // JC | 2026-11-18 | 이미 보정한 임시 메시도 원본 연결로 판별하여 다음 프레임에 보정이 꺼지지 않게 합니다.
        bool original=filter.sharedMesh==profile.amplifierMesh;
        if(!original && entries.TryGetValue(renderer,out var entry))
            original=entry.originalMesh==profile.amplifierMesh && entry.normalMesh && filter.sharedMesh==entry.normalMesh;
        if(!original) return false;
        var unit=renderer.GetComponentInParent<BattleCharactor>();
        if(unit && (unit.TemplateIndex=="40002" || unit.TemplateIndex=="40003")) return true;
        // JC | 2026-11-18 | 프리팹 미리보기에서 유닛 자체를 대상 루트로 지정한 경우도 루트 이름까지 검사합니다.
        for(var t=renderer.transform;t;t=t.parent)
            if(t.name=="Unit_VillanAmplifier_40002" || t.name=="Unit_VillanAmplifier_40002(Clone)" ||
                t.name=="Unit_VillanAmplifier_40003" || t.name=="Unit_VillanAmplifier_40003(Clone)") return true;
        return false;
    }
    // JC | 2026-11-18 | 메시 토폴로지를 변경하지 않는 법선 혼합입니다. 다른 시스템이 교체한 메시에는 원복을 덮어쓰지 않습니다.
    private void UpdateNormals(Entry e)
    {
        float strength=amplifierSurface!=null && amplifierSurface.enabled ? Mathf.Clamp01(amplifierSurface.normalStrength) : 0;
        if(strength<=0 || !profile || !profile.amplifierShadingMesh || !IsAmplifierSurfaceTarget(e.renderer))
        { RestoreNormals(e);return; }
        if(e.normalMesh && e.normalSource==profile.amplifierShadingMesh && Mathf.Approximately(e.normalBlend,strength))return;
        if(!e.meshFilter)e.meshFilter=e.renderer.GetComponent<MeshFilter>();
        if(!e.normalMesh)e.originalMesh=e.meshFilter.sharedMesh;
        // JC | 2026-11-18 | 빌드에서 읽을 수 없는 원본 FBX에는 접근하지 않고 전용 참조 사본만 읽습니다.
        var source=profile.amplifierShadingMesh;
        if(!source.isReadable || source.vertexCount!=e.originalMesh.vertexCount || source.blendShapeCount!=1 || source.GetBlendShapeName(0)!="JC_PanelNormals")
        { RestoreNormals(e);Debug.LogError("증폭기 법선 데이터의 정점 수가 원본과 다릅니다.",this);return; }
        var original=source.normals;var delta=new Vector3[original.Length];
        source.GetBlendShapeFrameVertices(0,0,new Vector3[original.Length],delta,new Vector3[original.Length]);
        var blended=new Vector3[original.Length];
        for(int i=0;i<blended.Length;i++)blended[i]=Vector3.Slerp(original[i],original[i]+delta[i],strength).normalized;
        if(!e.normalMesh){e.normalMesh=Instantiate(source);e.normalMesh.ClearBlendShapes();}
        e.normalMesh.name=e.originalMesh.name+" [JC Panel Normals]";e.normalMesh.hideFlags=HideFlags.HideAndDontSave;
        e.normalMesh.normals=blended;e.normalMesh.RecalculateTangents();e.meshFilter.sharedMesh=e.normalMesh;
        e.normalSource=profile.amplifierShadingMesh;e.normalBlend=strength;
    }
    private static void RestoreNormals(Entry e)
    {
        if(e.meshFilter && e.normalMesh && e.meshFilter.sharedMesh==e.normalMesh)e.meshFilter.sharedMesh=e.originalMesh;
        if(e.normalMesh){if(Application.isPlaying)Destroy(e.normalMesh);else DestroyImmediate(e.normalMesh);}
        e.normalMesh=null;e.originalMesh=null;e.normalSource=null;e.normalBlend=-1;
    }
    private static void DestroyOwned(Entry e)
    {
        RestoreNormals(e);
        foreach(var m in e.owned)if(m!=null){if(Application.isPlaying)Destroy(m);else DestroyImmediate(m);}
    }
    private void Restore()
    {
        foreach(var e in entries.Values)
        {
            if(e.renderer!=null){var current=e.renderer.sharedMaterials;for(int i=0;i<current.Length && i<e.shown.Length;i++)if(e.owned.Contains(current[i]))current[i]=e.original[i];e.renderer.sharedMaterials=current;}
            DestroyOwned(e);
        }
        entries.Clear();nextScan=0;
    }
    private void OnDisable()
    {
#if UNITY_EDITOR
        EditorSceneManager.sceneSaving-=BeforeSave;EditorSceneManager.sceneSaved-=AfterSave;
        EditorApplication.playModeStateChanged-=OnPlayState;
        AssemblyReloadEvents.beforeAssemblyReload-=BeforeReload;
#endif
        Restore();
    }
#if UNITY_EDITOR
    private void BeforeReload(){saving=true;Restore();}
    private void OnPlayState(PlayModeStateChange state)
    {
        if(state==PlayModeStateChange.ExitingEditMode||state==PlayModeStateChange.ExitingPlayMode){saving=true;Restore();}
        else {saving=false;if(state==PlayModeStateChange.EnteredEditMode)LoadProfile();RefreshLook();}
    }
    private void BeforeSave(Scene scene,string path){if(scene==gameObject.scene){saving=true;Restore();}}
    private void AfterSave(Scene scene){if(scene==gameObject.scene){saving=false;EditorApplication.QueuePlayerLoopUpdate();}}
#endif
}

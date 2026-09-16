using System;
using System.Collections.Generic;
using UnityEngine;
namespace JC.BuildingColors
{
    [ExecuteAlways,DisallowMultipleComponent]
    public sealed class BgColorModifier : MonoBehaviour
    {
        [Tooltip("원본 재질·텍스처와 파츠 마스크 정의입니다. 색상 프로필과 분리되어 있습니다.")] public BgColorDefinition definition;
        [Tooltip("저장·불러오기 대상입니다. 연결 변경만으로 조정값을 덮어쓰지 않습니다.")] public BgColorProfile profile;
        [Tooltip("현재 조정값입니다. 프로필 파일은 저장 버튼을 눌러야 변경됩니다.")] public BgPartColor[] parts;
        [Tooltip("현재 조정값에 대응하는 파츠 ID입니다. 영역 정의 확장 시 기존 값을 승계합니다.")] public string[] partIds;
        [Tooltip("이 건물만 원본 텍스처로 비교합니다. 조정값과 프로필은 유지됩니다.")] public bool showOriginal;
        [Tooltip("선택한 파츠 영역을 분홍색으로 강조합니다. -1은 일반 표시입니다. 프로필에는 저장하지 않습니다.")] public int highlightedPart=-1;
        sealed class Target {public Renderer renderer; public int slot,binding; public MaterialPropertyBlock previous;}
        sealed class MeshTarget {public MeshFilter filter;public Mesh original,separated;}
        readonly List<MeshTarget> meshTargets=new List<MeshTarget>();
        readonly List<Target> targets=new List<Target>();
        readonly List<RenderTexture> outputs=new List<RenderTexture>();
        Material processor;
        int appliedHash=int.MinValue;
        double nextScan;
        public int TargetCount=>targets.Count;
        public string Status {get;private set;}
        public static BgPartColor[] Copy(BgPartColor[] values)=>values==null?null:(BgPartColor[])values.Clone();
        public bool CanLoad=>definition&&profile&&profile.buildingId==definition.buildingId&&BgColorDefinition.ValidValues(profile.parts,profile.partIds);
        public void LoadProfile(){if(!CanLoad)throw new InvalidOperationException("건물 종류와 파츠 ID가 유효한 프로필이 필요합니다.");parts=definition.Remap(profile.parts,profile.partIds);partIds=(string[])definition.Ids.Clone();ApplyNow();}
        public void ResetInitial(){if(definition){parts=Copy(definition.Defaults);partIds=(string[])definition.Ids.Clone();showOriginal=false;highlightedPart=-1;ApplyNow();}}
        void OnEnable(){appliedHash=int.MinValue;nextScan=0;}
        void OnValidate(){appliedHash=int.MinValue;}
        void OnDisable(){Release();}
        void OnDestroy(){Release();}
        void Update(){if(!definition){Release();return;} if(Time.realtimeSinceStartupAsDouble>=nextScan){Rebind();nextScan=Time.realtimeSinceStartupAsDouble+2;}ApplyNow();}
        public void Rebind()
        {
            if(!definition||definition.bindings==null){Release();return;}
            var root=GetComponentInParent<BgColorRoot>();var scope=root?root.targetRoot:null;
            var renderers=root?root.TargetRenderers:(scope?scope.GetComponentsInChildren<Renderer>(true):AllSceneRenderers());
            var found=new List<Target>();
            foreach(var r in renderers){if(r.gameObject.scene!=gameObject.scene)continue;var mats=r.sharedMaterials;
                for(int slot=0;slot<mats.Length;slot++)for(int i=0;i<definition.bindings.Length;i++)if(mats[slot]==definition.bindings[i].material){
                    found.Add(new Target{renderer=r,slot=slot,binding=i});break;
                }
            }
            bool same=found.Count==targets.Count;for(int i=0;same&&i<found.Count;i++)same=found[i].renderer==targets[i].renderer&&found[i].slot==targets[i].slot&&found[i].binding==targets[i].binding;
            if(same){BindMeshes();return;}
            RestoreTargets();foreach(var t in found){t.previous=new MaterialPropertyBlock();t.renderer.GetPropertyBlock(t.previous,t.slot);targets.Add(t);}
            BindMeshes();appliedHash=int.MinValue;
        }
        Renderer[] AllSceneRenderers(){var list=new List<Renderer>();foreach(var g in gameObject.scene.GetRootGameObjects())list.AddRange(g.GetComponentsInChildren<Renderer>(true));return list.ToArray();}
        void BindMeshes()
        {
            foreach(var t in targets){var f=t.renderer?t.renderer.GetComponent<MeshFilter>():null;if(!f||meshTargets.Exists(m=>m.filter==f))continue;
                foreach(var b in definition.meshes??Array.Empty<BgMeshBinding>())if(b.original&&b.separated&&(f.sharedMesh==b.original||f.sharedMesh==b.separated)){meshTargets.Add(new MeshTarget{filter=f,original=b.original,separated=b.separated});appliedHash=int.MinValue;break;}
            }
        }
        void RestoreTargets(){foreach(var m in meshTargets)if(m.filter&&(m.filter.sharedMesh==m.separated||m.filter.sharedMesh==m.original))m.filter.sharedMesh=m.original;meshTargets.Clear();foreach(var t in targets)if(t.renderer)t.renderer.SetPropertyBlock(t.previous.isEmpty?null:t.previous,t.slot);targets.Clear();}
        int Hash(){unchecked{int h=definition.GetInstanceID()*31+(showOriginal?1:0)+highlightedPart*71;foreach(var p in parts)h=(((h*31+p.hue.GetHashCode())*31+p.lightness.GetHashCode())*31+p.saturation.GetHashCode())*31+(p.ignoreSourceColorAndShading?1:0);return h;}}
        public void EnsureParts()
        {
            if(!definition||definition.initial==null)return;
            bool same=parts!=null&&parts.Length==definition.Ids.Length&&partIds!=null&&partIds.Length==parts.Length;
            for(int i=0;same&&i<partIds.Length;i++)same=partIds[i]==definition.Ids[i];
            if(same)return;
            parts=definition.Remap(parts,partIds);partIds=(string[])definition.Ids.Clone();appliedHash=int.MinValue;
        }
        public void InvalidatePreview(){appliedHash=int.MinValue;}
        public void ApplyNow()
        {
            if(!isActiveAndEnabled)return;
            if(!definition){Release();return;}
            if(definition.initial==null||definition.initial.Length==0||definition.initial.Length>BgColorDefinition.Capacity)return;
            EnsureParts();
            int hash=Hash();if(hash==appliedHash)return;
            Status=null;
            if(!processor){if(!definition.recolorShader||!definition.recolorShader.isSupported){Status="색상 셰이더를 사용할 수 없습니다.";return;}processor=new Material(definition.recolorShader){hideFlags=HideFlags.HideAndDontSave};}
            while(outputs.Count<definition.bindings.Length)outputs.Add(null);
            bool changed=false;var changes=new Vector4[16];var flatColors=new Vector4[16];
            for(int i=0;i<parts.Length;i++){
                var a=definition.initial[i];var p=parts[i];float sat=Mathf.Clamp(p.saturation,0,100);float ratio=sat==0?0:(a.saturation>.025f?sat/a.saturation:-1);
                changes[i]=new Vector4(Mathf.Clamp01(p.lightness)-a.lightness,(sat-a.saturation)*.004f,Mathf.DeltaAngle(a.hue,p.hue)*Mathf.Deg2Rad,ratio);
                float hue=p.hue*Mathf.Deg2Rad,chroma=sat*.004f;
                flatColors[i]=new Vector4(Mathf.Clamp01(p.lightness),chroma*Mathf.Cos(hue),chroma*Mathf.Sin(hue),p.ignoreSourceColorAndShading?1:0);
                // Flat paint must also run when the target equals the source reference.
                changed|=p.ignoreSourceColorAndShading||Mathf.Abs(changes[i].x)+Mathf.Abs(changes[i].y)+Mathf.Abs(changes[i].z)>1e-6f;
            }
            processor.SetInt("_PartCount",parts.Length);processor.SetVectorArray("_Changes",changes);processor.SetVectorArray("_FlatColors",flatColors);processor.SetFloat("_Highlight",highlightedPart);
            var textures=new Texture[definition.bindings.Length];
            for(int i=0;i<textures.Length;i++){
                var b=definition.bindings[i];var source=b.source?b.source:Texture2D.whiteTexture;
                if(showOriginal){textures[i]=b.original?b.original:Texture2D.whiteTexture;continue;}
                if(!changed&&highlightedPart<0){textures[i]=source;continue;}
                // Split UV atlases retain the original 2048 texels per page.
                int w=Mathf.Min(source.width,4096),h=Mathf.Min(source.height,4096);
                if(!outputs[i]||outputs[i].width!=w||outputs[i].height!=h){DestroyOutput(i);outputs[i]=new RenderTexture(w,h,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB){name=definition.buildingId+" 색상 미리보기",hideFlags=HideFlags.HideAndDontSave,useMipMap=true,autoGenerateMips=true,wrapMode=source.wrapMode,filterMode=FilterMode.Bilinear};outputs[i].Create();}
                processor.SetTexture("_Parts",b.maskParts);processor.SetTexture("_Floor",b.maskFloor);processor.SetTexture("_Extra",b.maskExtra?b.maskExtra:Texture2D.blackTexture);processor.SetTexture("_Extra2",b.maskExtra2?b.maskExtra2:Texture2D.blackTexture);
                var prev=RenderTexture.active;bool write=GL.sRGBWrite;try{GL.sRGBWrite=QualitySettings.activeColorSpace==ColorSpace.Linear;Graphics.Blit(source,outputs[i],processor);}finally{RenderTexture.active=prev;GL.sRGBWrite=write;}
                textures[i]=outputs[i];
            }
            foreach(var m in meshTargets)if(m.filter)m.filter.sharedMesh=showOriginal?m.original:m.separated;
            foreach(var t in targets)if(t.renderer){var block=new MaterialPropertyBlock();t.renderer.GetPropertyBlock(block,t.slot);block.SetTexture("_BaseMap",textures[t.binding]);block.SetTexture("_MainTex",textures[t.binding]);t.renderer.SetPropertyBlock(block,t.slot);}
            appliedHash=hash;
        }
        void DestroyOutput(int i){if(outputs[i]){outputs[i].Release();DestroyOwned(outputs[i]);outputs[i]=null;}}
        void Release(){RestoreTargets();for(int i=0;i<outputs.Count;i++)DestroyOutput(i);outputs.Clear();DestroyOwned(processor);processor=null;appliedHash=int.MinValue;}
        static void DestroyOwned(UnityEngine.Object o){if(!o)return;if(Application.isPlaying)Destroy(o);else DestroyImmediate(o);}
    }
}

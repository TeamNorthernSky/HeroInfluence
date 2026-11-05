using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JC.CharacterColors
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class JcVillainBodyColorController : MonoBehaviour
    {
        [Tooltip("일반 빌런 몸체의 공용 저장 프로필입니다. 자동 생성 시 기본 프로필을 연결합니다.")]
        public JcVillainBodyProfile profile;
        [Tooltip("원본 텍스처와 비교합니다. 조절값과 저장 프로필은 유지됩니다.")]
        public bool showOriginal;
        [Tooltip("색상을 조절할 검은 영역을 분홍색으로 표시합니다. 베이크 파일에는 포함하지 않습니다.")]
        public bool showMask;
        JcVillainBodyProfile draft, draftSource;
        Material processor;
        RenderTexture output;
        int appliedHash;
        bool generated,excluded;
        double nextScan;
        sealed class Binding {
            public Renderer renderer;
            public Texture previousBase,previousMain,previousSurfaceSource,assigned;
            public Vector4 previousSurface,previousRange;
            public bool ownsSurface;
            public readonly JC.Environment.JcNightMaterialBindings surfaceMaterials=new JC.Environment.JcNightMaterialBindings();
        }
        readonly List<Binding> bindings=new List<Binding>();
        MaterialPropertyBlock propertyBlock;
        MaterialPropertyBlock block => propertyBlock ?? (propertyBlock=new MaterialPropertyBlock());
        public int TargetCount => bindings.Count;
        public string Status { get; private set; }
        public JcVillainBodyProfile Settings { get { if(!draft || draftSource!=profile)Reload();return draft; } }
        public void Reload()
        {
            Restore();DestroyOwned(draft);draftSource=profile;
            draft=profile?Instantiate(profile):null;
            // HideAndDontSave includes NotEditable and disables SerializedProperty fields in the tuning Inspector.
            // JC | 2026-11-04 | 저장 색상과 베이크가 일치할 때만 PNG를 재사용하며 임시 조절값은 명시적으로 저장합니다.
            // JC | 2026-11-04 | 임시 복사본에는 저장 방지만 적용해야 플레이 중 Inspector에서 값을 편집할 수 있습니다.
            if(draft){draft.name=profile.name+" (조절 중)";draft.hideFlags=HideFlags.DontSave;}
            generated=false;nextScan=0;
        }
        public static bool SupportsScene(string name) => name=="DHScene_3" || name=="TutorialExploreScene" || name=="TmpBattleScene" || name=="TutorialBattleScene";
        public static bool IsBossKey(string key) => key!=null && key.Trim().Replace("Start_","")=="BE490";
        public static bool ExcludedBattle(Scene scene)
        {
            if(scene.name.IndexOf("Boss",StringComparison.OrdinalIgnoreCase)>=0)return true;
            if(scene.name!="TmpBattleScene" && scene.name!="TutorialBattleScene")return false;
            var context=CombatContext.Instance;
            if(context!=null && (IsBossKey(context.EventBattle?.BattleKey) || IsBossKey(context.CombatEnemy?.EnemyGroupKey)))return true;
            var background=JC.BattleTesting.JcBossBattleBackground.Load();
            if(background && background.Matches(context))return true;
            foreach(var root in scene.GetRootGameObjects())
            {
                if(root.GetComponentsInChildren<MonoBehaviour>(true).Any(c=>c && (c.GetType().Name=="BossController" || c.GetType().Name=="JcBossController")))return true;
                if(root.GetComponentsInChildren<BattleCharactor>(true).Any(c=>int.TryParse(c.TemplateIndex,out int id) && id>=40000 && id<50000))return true;
            }
            return false;
        }
        public static JcVillainBodyColorController Ensure(Scene scene)
        {
            if(!scene.IsValid() || !scene.isLoaded || !SupportsScene(scene.name))return null;
            var existing=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<JcVillainBodyColorController>(true)).FirstOrDefault();
            if(existing)return existing;
            var p=Resources.Load<JcVillainBodyProfile>(JcVillainBodyProfile.ResourceName);if(!p)return null;
            var go=new GameObject("JC_VillainBodyColor"){hideFlags=HideFlags.DontSave};
            SceneManager.MoveGameObjectToScene(go,scene);
            var parent=scene.GetRootGameObjects().FirstOrDefault(r=>r.name=="JC_Environment");if(parent)go.transform.SetParent(parent.transform,false);
            var controller=go.AddComponent<JcVillainBodyColorController>();controller.profile=p;controller.Reload();return controller;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register(){SceneManager.sceneLoaded-=Loaded;SceneManager.sceneLoaded+=Loaded;}
        static void Loaded(Scene scene,LoadSceneMode mode)=>Ensure(scene);
        void OnEnable(){if(draft)draft.hideFlags=HideFlags.DontSave;generated=false;nextScan=0;}
        void OnDisable(){Restore();ReleaseOutput();}
        void OnDestroy(){Restore();ReleaseOutput();DestroyOwned(draft);}
        void OnValidate(){generated=false;nextScan=0;}
        void LateUpdate()
        {
            if(!Settings)return;
            bool scan=Time.realtimeSinceStartupAsDouble>=nextScan;
            ApplyNow(scan);
            if(scan)nextScan=Time.realtimeSinceStartupAsDouble+1;
        }
        public bool Accepts(Renderer renderer)
        {
            var p=Settings;if(!renderer || !p || !p.source || renderer.gameObject.scene!=gameObject.scene || renderer.GetComponentInParent<Canvas>(true))return false;
            if(!(renderer is SkinnedMeshRenderer) && !(renderer is MeshRenderer))return false;
            // All approved body renderers use a single material: keep renderer-level hit-flash blocks compatible.
            var mats=renderer.sharedMaterials;if(mats.Length!=1 || !mats[0] || !mats[0].HasProperty("_BaseMap"))return false;
            var tex=mats[0].GetTexture("_BaseMap");if(tex!=p.source)return false;
            var mesh=renderer is SkinnedMeshRenderer skin?skin.sharedMesh:renderer.GetComponent<MeshFilter>()?.sharedMesh;
            // JC | 2026-11-04 | 공용 텍스처를 사용하는 다른 모델과 무기까지 보정되지 않도록 몸체 메시도 대조합니다.
            if(!mesh || p.bodyMeshes==null || !p.bodyMeshes.Contains(mesh))return false;
            for(var t=renderer.transform;t;t=t.parent)
                if(t.name.IndexOf("Arnold",StringComparison.OrdinalIgnoreCase)>=0 || t.name.Contains("아놀드"))return false;
            return true;
        }
        void Scan()
        {
            var found=gameObject.scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Renderer>(true)).Where(Accepts).ToArray();
            foreach(var b in bindings.Where(b=>!b.renderer || !found.Contains(b.renderer)).ToArray()){Restore(b);bindings.Remove(b);}
            foreach(var r in found)
            {
                if(bindings.Any(b=>b.renderer==r))continue;
                r.GetPropertyBlock(block);
                bindings.Add(new Binding{renderer=r,previousBase=block.GetTexture("_BaseMap"),previousMain=block.GetTexture("_MainTex"),
                    previousSurfaceSource=block.GetTexture("_JcVillainSource"),previousSurface=block.GetVector("_JcVillainParams"),previousRange=block.GetVector("_JcVillainBlackRange")});
            }
        }
        public void ApplyNow(bool rescan=true)
        {
            var p=Settings;
            if(!p || !p.source || !p.processorShader){Status="기본 프로필의 원본·셰이더 연결이 필요합니다.";Restore();return;}
            if(rescan)excluded=ExcludedBattle(gameObject.scene);
            var context=CombatContext.Instance;
            bool bossContext=(gameObject.scene.name=="TmpBattleScene" || gameObject.scene.name=="TutorialBattleScene") && context!=null &&
                (IsBossKey(context.EventBattle?.BattleKey)||IsBossKey(context.CombatEnemy?.EnemyGroupKey));
            if(!SupportsScene(gameObject.scene.name) || excluded || bossContext){Restore();Status="대상 밖 씬 또는 보스전: 보정 제외";return;}
            if(rescan)Scan();
            Texture selected=p.source;
            if(!showOriginal)
            {
                if(!showMask && p.useBaked && p.BakeMatches)selected=p.bakedTexture;
                else
                {
                    int hash=p.tuning.Signature^(showMask?int.MinValue:0)^p.source.GetInstanceID();
                    if(!generated || hash!=appliedHash){Render(p.tuning,showMask);appliedHash=hash;generated=true;}
                    selected=output;
                }
            }
            foreach(var b in bindings)if(b.renderer)
            {
                bool surface=p.surface.enabled && !showOriginal && !showMask;
                if(surface){int group=JC.Environment.JcNightMaterialBindings.CharacterGroup(b.renderer);b.surfaceMaterials.Bind(b.renderer,0,null,group>0,group);}
                else b.surfaceMaterials.Dispose();
                b.renderer.GetPropertyBlock(block);
                // JC | 2026-11-04 | 반사와 금속성은 조명에 따라 달라지므로 기본색 PNG와 분리해 현재 렌더러의 속성 블록에 전달합니다.
                var parameters=new Vector4(surface?1:0,Mathf.Clamp(p.surface.reflection,0,4),Mathf.Clamp01(p.surface.smoothness),Mathf.Clamp01(p.surface.metallic));
                var range=new Vector4(Mathf.Clamp(p.tuning.blackThreshold,.05f,.45f),Mathf.Clamp(p.tuning.feather,.005f,.15f),Mathf.Clamp(p.tuning.saturationLimit,.1f,.3f),0);
                if(block.GetTexture("_BaseMap")!=selected || block.GetTexture("_MainTex")!=selected || block.GetVector("_JcVillainParams")!=parameters ||
                    block.GetVector("_JcVillainBlackRange")!=range || block.GetTexture("_JcVillainSource")!=p.source)
                {
                    block.SetTexture("_BaseMap",selected);block.SetTexture("_MainTex",selected);
                    block.SetTexture("_JcVillainSource",p.source);block.SetVector("_JcVillainParams",parameters);block.SetVector("_JcVillainBlackRange",range);
                    b.renderer.SetPropertyBlock(block);
                }
                b.ownsSurface=true;
                b.assigned=selected;
            }
            Status=showOriginal?"원본 비교":showMask?"검은 영역 보기":p.useBaked&&p.BakeMatches?"저장된 베이크 사용":p.useBaked?"베이크와 조절값 불일치: 실시간 미리보기":"실시간 미리보기";
        }
        public RenderTexture RenderForBake(){Render(Settings.tuning,false);generated=false;return output;}
        void Render(JcVillainBodyTuning tuning,bool mask)
        {
            var p=Settings;
            if(!processor){processor=new Material(p.processorShader){hideFlags=HideFlags.HideAndDontSave};}
            if(!output || output.width!=p.source.width || output.height!=p.source.height)
            {
                if(output){output.Release();DestroyOwned(output);}
                output=new RenderTexture(p.source.width,p.source.height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB){name="일반 빌런 몸체 색상",hideFlags=HideFlags.HideAndDontSave,useMipMap=true,autoGenerateMips=true,wrapMode=p.source.wrapMode,filterMode=FilterMode.Bilinear};output.Create();
            }
            processor.SetFloat("_BlackBodyMode",1);processor.SetFloat("_BlackBodyMaskView",mask?1:0);
            var linear=tuning.tint.linear;processor.SetVector("_BlackBodyTint",new Vector4(linear.r,linear.g,linear.b,1));
            processor.SetVector("_BlackBodyRange",new Vector4(Mathf.Clamp(tuning.blackThreshold,.05f,.45f),Mathf.Clamp(tuning.feather,.005f,.15f),Mathf.Clamp(tuning.saturationLimit,.1f,.3f),Mathf.Clamp01(tuning.strength)));
            processor.SetVector("_BlackBodyTone",new Vector4(Mathf.Clamp(tuning.sourceLightness,.05f,.4f),Mathf.Clamp(tuning.detail,.25f,1.5f),0,0));
            var prev=RenderTexture.active;bool write=GL.sRGBWrite;
            try{GL.sRGBWrite=QualitySettings.activeColorSpace==ColorSpace.Linear;Graphics.Blit(p.source,output,processor);}
            finally{RenderTexture.active=prev;GL.sRGBWrite=write;}
        }
        void Restore(Binding b)
        {
            if(!b.renderer){b.surfaceMaterials.Dispose();return;}
            b.renderer.GetPropertyBlock(block);
            var source=draft?draft.source:profile?profile.source:null;
            // Restore only the keys still owned by this controller; preserve current hit/charge effects.
            if(block.GetTexture("_BaseMap")==b.assigned)block.SetTexture("_BaseMap",b.previousBase?b.previousBase:source);
            if(block.GetTexture("_MainTex")==b.assigned)block.SetTexture("_MainTex",b.previousMain?b.previousMain:source);
            if(b.ownsSurface && block.GetTexture("_JcVillainSource")==source){
                block.SetTexture("_JcVillainSource",b.previousSurfaceSource?b.previousSurfaceSource:Texture2D.whiteTexture);
                block.SetVector("_JcVillainParams",b.previousSurface);block.SetVector("_JcVillainBlackRange",b.previousRange);
            }
            b.renderer.SetPropertyBlock(block);
            b.surfaceMaterials.Dispose();
        }
        void Restore(){foreach(var b in bindings)Restore(b);bindings.Clear();}
        void ReleaseOutput(){if(output){output.Release();DestroyOwned(output);}output=null;DestroyOwned(processor);processor=null;generated=false;}
        static void DestroyOwned(UnityEngine.Object value){if(!value)return;if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
    }
}

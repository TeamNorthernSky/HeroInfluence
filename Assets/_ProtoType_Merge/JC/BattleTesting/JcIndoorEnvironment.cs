using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace JC.BattleTesting
{
    // JC | 2026-11-09 | 본게임/테스트 양쪽의 단일 설치·조절 진입점입니다. 기존 프로필과 전문 적용기를
    // 재사용하고 초안만 편집합니다. 보스 원경·창문은 보스 확장으로 남겨 은행과 전역 환경을 섞지 않습니다.
    [DisallowMultipleComponent]
    public sealed class JcIndoorEnvironment : MonoBehaviour
    {
        public JcBankEnvironment Bank { get; private set; }
        public JcBossEnvironment Boss { get; private set; }
        public JcBankBackgroundMaterials BankMaterials { get; private set; }
        public JcBossBackgroundMaterials BossMaterials { get; private set; }
        public JcBankToonController Toon { get; private set; }
        public JC.Environment.JcContactShadingController Contact { get; private set; }
        public JcIndoorEnvironmentProfile Source => Bank ? (JcIndoorEnvironmentProfile)Bank.Source : Boss ? Boss.Source : null;
        public JcIndoorEnvironmentProfile Draft => Bank ? (JcIndoorEnvironmentProfile)Bank.Draft : Boss ? Boss.Draft : null;
        public bool IsBoss => Boss;
        readonly List<Behaviour> owned = new List<Behaviour>();
        static readonly HashSet<JcIndoorEnvironment> instances = new HashSet<JcIndoorEnvironment>();
        static readonly string[] Globals = { "_JcIndoorMode", "_JcIndoorSsao", "_JcBankFillDirection", "_JcBankFillColor" };
        Vector4[] baseline;
        bool initialized, acquired;
        UniversalAdditionalCameraData cameraData;
        int originalRenderer;
        JcBankBattleBackground bankSettings;
        [System.Serializable] sealed class CameraSettings { public int m_RendererIndex = -1; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetInstances() => instances.Clear();
        T Add<T>(string label) where T : Behaviour
        {
            var go = new GameObject(label); go.transform.SetParent(transform,false);
            var item = go.AddComponent<T>(); item.hideFlags = HideFlags.HideInInspector; owned.Add(item); return item;
        }
        public void Initialize(GameObject stage, Camera camera, JcBankBattleBackground bank, JcBossBattleBackground boss)
        {
            if(initialized || !stage || (bank == null) == (boss == null)) return;
            initialized = true; instances.Add(this); Acquire();
            // JC | 2026-11-09 | 은행 전용 렌더러 선택도 이 조절기 수명에 묶고 중지 시 원래 선택을 복원합니다.
            bankSettings=bank;
            if(bank && camera)
            {
                cameraData=camera.GetUniversalAdditionalCameraData();
                originalRenderer=JsonUtility.FromJson<CameraSettings>(JsonUtility.ToJson(cameraData)).m_RendererIndex;
                bank.ApplyCamera(camera);
            }
            var contact = bank ? bank.contactShadingProfile : boss.contactShadingProfile;
            var toon = bank ? bank.toonProfile : boss.toonProfile;
            if(contact) { Contact = Add<JC.Environment.JcContactShadingController>("JC_ContactShading"); Contact.Initialize(contact,stage.transform); }
            if(bank)
            {
                BankMaterials = Add<JcBankBackgroundMaterials>("JC_Materials");
                BankMaterials.Initialize(stage,bank.materialProfile,Contact,toon ? toon.surfaceShader : null);
                Bank = Add<JcBankEnvironment>("JC_Lighting"); Bank.Initialize(bank.environmentProfile,stage.transform);
            }
            else
            {
                BossMaterials = Add<JcBossBackgroundMaterials>("JC_Materials");
                BossMaterials.ContactShading = Contact;
                BossMaterials.Initialize(stage,boss.glassProfile,camera,boss.surfaceProfile);
                Boss = Add<JcBossEnvironment>("JC_Lighting"); Boss.Initialize(boss.environmentProfile,stage.transform);
                BossMaterials.Environment = Boss; Boss.SurfaceController = BossMaterials; BossMaterials.ApplySurfaces();
            }
            if(toon) { Toon = Add<JcBankToonController>("JC_Toon"); Toon.Initialize(toon); }
            ApplyDraft();
        }
        void Acquire()
        {
            if(acquired || SceneManager.GetActiveScene()!=gameObject.scene) return;
            baseline = new Vector4[Globals.Length];
            for(int i=0;i<Globals.Length;i++) baseline[i]=Shader.GetGlobalVector(Globals[i]);
            acquired=true;
        }
        public void ApplySharedSettings()
        {
            if(!Draft || !isActiveAndEnabled || SceneManager.GetActiveScene()!=gameObject.scene) return;
            Acquire();
            // JC | 2026-11-09 | 은행 실제광 경로와 보스의 창문 전용광 경로를 명시적으로 구별합니다.
            Shader.SetGlobalVector("_JcIndoorMode",new Vector4(Bank?2:1,0,0,0));
            Shader.SetGlobalVector("_JcIndoorSsao",new Vector4(Boss && Draft.ssaoEnabled ? Draft.ssaoIntensity : 0,
                Draft.ssaoRadius,Draft.ssaoDirectLightingStrength,0));
        }
        public void ApplyDraft()
        {
            if(!isActiveAndEnabled || SceneManager.GetActiveScene()!=gameObject.scene) return;
            // JC | 2026-11-09 | 추가 로드된 씬으로 돌아올 때도 원래 전투의 렌더러 선택을 재획득합니다.
            if(bankSettings && cameraData)bankSettings.ApplyCamera(cameraData.GetComponent<Camera>());
            ApplySharedSettings();
            if(Bank) Bank.ApplyDraft();
            if(Boss) Boss.ApplyDraft();
            if(Toon) Toon.ApplyDraft();
            if(Contact) Contact.ApplyDraft();
        }
        public static bool UsesLocalCharacterLighting(Renderer renderer)
        {
            foreach(var item in instances)
            {
                if(!item || !item.isActiveAndEnabled || !item.Draft || !renderer || renderer.gameObject.scene != item.gameObject.scene) continue;
                var mode=item.Draft.characterLighting;
                if(mode==JcIndoorEnvironmentProfile.CharacterLighting.Soft) return false;
                if(mode==JcIndoorEnvironmentProfile.CharacterLighting.InteriorLights) return true;
                return item.Boss && item.Boss.Isolated;
            }
            return JcBossEnvironment.UsesLocalLighting(renderer);
        }
        void LateUpdate()
        {
            if(!initialized) return;
            if(SceneManager.GetActiveScene()!=gameObject.scene) { if(acquired) Suspend(); return; }
            if(!acquired) { foreach(var item in owned) if(item) item.enabled=true; ApplyDraft(); }
        }
        void Suspend()
        {
            // JC | 2026-11-09 | 전문 적용기가 자기 변경을 먼저 복원하고 공통 전역값을 마지막에 되돌립니다.
            for(int i=owned.Count-1;i>=0;i--) if(owned[i]) owned[i].enabled=false;
            if(!acquired) return;
            for(int i=0;i<Globals.Length;i++) Shader.SetGlobalVector(Globals[i],baseline[i]);
            acquired=false;
            if(cameraData) cameraData.SetRenderer(originalRenderer);
        }
        void OnEnable() { if(initialized) { if(bankSettings && cameraData)bankSettings.ApplyCamera(cameraData.GetComponent<Camera>()); foreach(var item in owned) if(item) item.enabled=true; ApplyDraft(); } }
        void OnDisable() => Suspend();
        void OnDestroy() { Suspend(); instances.Remove(this); }
    }
}

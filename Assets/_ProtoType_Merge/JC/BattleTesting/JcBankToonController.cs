using UnityEngine;
using UnityEngine.SceneManagement;
namespace JC.BattleTesting
{
    public sealed class JcBankToonController : MonoBehaviour
    {
        [SerializeField, HideInInspector] JcBankToonProfile source, draft;
        public JcBankToonProfile Source => source;
        public JcBankToonProfile Draft => draft;
        // JC | 2026-11-06 | 후면 보조광은 은행 환경 조명에서 소유합니다. 툰 토글은 명암에만 관여합니다.
        static readonly string[] Globals = { "_JcBankToon", "_JcBankToonOptions", "_JcBankToonTint" };
        Vector4[] baseline;
        bool acquired;
        // JC | 2026-11-06 | 은행 실내 환경을 만든 뒤 연결합니다. 기존 실외 조명 차단과 주광 지정은 유지합니다.
        public void Initialize(JcBankToonProfile profile)
        {
            source = profile;
            ReloadProfile();
        }
        public void ReloadProfile()
        {
            ReleaseDraft();
            if (source) { draft = Instantiate(source); draft.name = source.name + " (조절 중)"; draft.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave; }
            ApplyDraft();
        }
        public void ApplyDraft()
        {
            if (!draft || !isActiveAndEnabled || SceneManager.GetActiveScene() != gameObject.scene) { Restore(); return; }
            if (!acquired) { baseline = new Vector4[Globals.Length]; for(int i=0;i<Globals.Length;i++)baseline[i]=Shader.GetGlobalVector(Globals[i]); acquired=true; }
            float strength = draft.toonEnabled ? Mathf.Clamp01(draft.strength) : 0;
            Shader.SetGlobalVector(Globals[0],new Vector4(strength,Mathf.Clamp(draft.steps,2,4),Mathf.Clamp(draft.softness,.01f,.49f),0));
            Shader.SetGlobalVector(Globals[1],new Vector4(draft.background?1:0,draft.characters?1:0,0,0));
            Shader.SetGlobalVector(Globals[2],draft.shadowTint.linear);
        }
        void LateUpdate() { if (draft && (acquired != (SceneManager.GetActiveScene() == gameObject.scene))) ApplyDraft(); }
        void OnEnable() => ApplyDraft();
        void OnDisable() => Restore();
        void OnDestroy() { Restore(); ReleaseDraft(); }
        // JC | 2026-11-06 | 에디트 카메라 미리보기 종료 시 전역 툰 값과 임시 초안도 명시적으로 정리합니다.
        public void ReleaseEditorPreview() => OnDestroy();
        // JC | 2026-11-06 | 은행 이탈·비활성화 시 전역값을 되돌려 다른 씬의 기존 셰이딩에 남지 않게 합니다.
        void Restore() { if(!acquired)return;for(int i=0;i<Globals.Length;i++)Shader.SetGlobalVector(Globals[i],baseline[i]);acquired=false; }
        void ReleaseDraft() { if(!draft)return;if(Application.isPlaying)Destroy(draft);else DestroyImmediate(draft);draft=null; }
    }
}

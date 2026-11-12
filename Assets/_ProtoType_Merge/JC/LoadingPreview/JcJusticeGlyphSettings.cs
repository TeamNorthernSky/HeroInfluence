using UnityEngine;

namespace JC.LoadingPreview
{
    // JC | 2026-11-12 | Inspector의 임시 조절값과 저장 프로필을 분리합니다. 플레이 중 저장한 값도 종료 후 유지됩니다.
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class JcJusticeGlyphSettings : MonoBehaviour
    {
        [SerializeField, Tooltip("이 비교 씬 전용 저장 프로필입니다.")] private JcJusticeGlyphProfile profile;
        [SerializeField, Tooltip("같은 씬의 원본/수정안 비교 재생기입니다.")] private JcLoadingGlyphComparison comparison;
        [SerializeField, Tooltip("원형 중심 (0,0) 기준. X 오른쪽, Y 위쪽. 등장 이동이 끝난 시작 중심이며 준비 시 여기서 왼쪽으로 6 이동합니다.")] private Vector2 startCenter = new Vector2(-30f, 0f);
        [SerializeField, Tooltip("타격 후 유지하는 중심 위치입니다. 원형 중심 기준 UI 좌표입니다.")] private Vector2 endCenter = new Vector2(6f, 0f);
        [SerializeField, Range(.25f, 2f), Tooltip("1배=현재 초기 크기(0.46). 원 테두리 안에 맞도록 실제 확대는 제한될 수 있습니다.")] private float initialSize = 1f;
        [SerializeField, Range(.25f, 2f), Tooltip("1배=현재 최종 크기(0.72). 원 테두리 안에 맞도록 실제 확대는 제한될 수 있습니다.")] private float finalSize = 1f;
        public Vector2 StartCenter => startCenter;
        public Vector2 EndCenter => endCenter;
        public float InitialSize => initialSize;
        public float FinalSize => finalSize;
        public JcJusticeGlyphProfile Profile => profile;

        private void OnEnable() { LoadSaved(); }
        // JC | 2026-11-12 | 씬에 지정된 재생기만 연결합니다. 전체 씬 검색이나 다른 비교 오브젝트의 자동 선택은 하지 않습니다.
        public void ApplyPreview() { if (comparison) comparison.ApplyJusticeSettings(this); }
        public void LoadSaved()
        {
            if (!profile) return;
            startCenter = profile.startCenter; endCenter = profile.endCenter;
            initialSize = profile.initialSize; finalSize = profile.finalSize;
            ApplyPreview();
        }
#if UNITY_EDITOR
        // JC | 2026-11-12 | 명시적인 버튼에서만 전용 프로필을 저장합니다. 씬 저장이나 본편 로딩 수정은 하지 않습니다.
        public void SaveCurrent()
        {
            if (!profile) return;
            UnityEditor.Undo.RecordObject(profile, "저스티스 비교 설정 저장");
            profile.startCenter = startCenter; profile.endCenter = endCenter;
            profile.initialSize = initialSize; profile.finalSize = finalSize;
            UnityEditor.EditorUtility.SetDirty(profile);
            UnityEditor.AssetDatabase.SaveAssetIfDirty(profile);
        }
#endif
    }
}

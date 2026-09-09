using System.Collections.Generic;
using UnityEngine;

namespace JC.Env
{
    /// <summary>
    /// 지역 팔레트 적용기 — 씬에 하나 두고, 프로파일을 텍스처로 구워 전역 셰이더 변수로 밀어 넣는다.
    /// <c>JC/Environment/Region Tint Lit</c> 셰이더가 <c>_JcRegionTex</c>/<c>_JcRegionBounds</c> 를 읽는다.
    ///
    /// ExecuteAlways: 에디트 모드에서도 켜지는 순간 밀어 넣어 씬뷰에서 바로 보인다. 꺼지면 바운드를 0 으로 되돌려
    /// 셰이더의 B축이 조용히 비활성화된다(전역 변수는 씬을 넘어 살아남으므로 정리 필수).
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class JcRegionPaletteController : MonoBehaviour
    {
        [Tooltip("지역 팔레트 프로파일(정본).")]
        [SerializeField] private JcRegionPaletteProfile profile;

        [Tooltip("프로파일 ④ 나무 재질 값을 기록할 재질들(보통 M_Tree_RegionTint 하나). 재질은 출력물 — 직접 열어 만지지 말 것.")]
        [SerializeField] private List<Material> treeMaterials = new List<Material>();

        [Tooltip("이 값이 0 이 아니면 프로파일 대신 이 사본을 밀어 넣는다 — 에디터 초안(draft) 전용. 손대지 말 것.")]
        [System.NonSerialized] public JcRegionPaletteProfile overrideSource;

        private static readonly int ID_Tex = Shader.PropertyToID("_JcRegionTex");
        private static readonly int ID_Bounds = Shader.PropertyToID("_JcRegionBounds");

        private Texture2D _baked;

        public JcRegionPaletteProfile Profile { get => profile; set { profile = value; Push(); } }
        public Texture2D BakedTexture => _baked;

        private void OnEnable() => Push();

        private void OnDisable()
        {
            Shader.SetGlobalVector(ID_Bounds, Vector4.zero);
        }

        private void OnDestroy()
        {
            if (_baked != null) DestroyImmediate(_baked);
        }

        private void OnValidate()
        {
            if (isActiveAndEnabled) Push();
        }

        /// <summary>프로파일(또는 초안)을 다시 굽고 전역 변수에 밀어 넣는다.</summary>
        [ContextMenu("재베이크 · 적용")]
        public void Push()
        {
            var src = overrideSource != null ? overrideSource : profile;
            if (src == null)
            {
                Shader.SetGlobalVector(ID_Bounds, Vector4.zero);
                return;
            }
            _baked = src.Bake(_baked);
            Shader.SetGlobalTexture(ID_Tex, _baked);
            Shader.SetGlobalVector(ID_Bounds, src.BoundsVector);
            for (int i = 0; i < treeMaterials.Count; i++)
            {
                var m = treeMaterials[i];
                if (m == null) continue;
                src.ApplyToMaterial(m);
#if UNITY_EDITOR
                if (!Application.isPlaying) UnityEditor.EditorUtility.SetDirty(m);
#endif
            }
        }

        public List<Material> TreeMaterials => treeMaterials;
    }
}

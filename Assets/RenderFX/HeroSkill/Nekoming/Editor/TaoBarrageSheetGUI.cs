using UnityEngine;
using UnityEditor;

namespace JC.VFX
{
    /// <summary>
    /// 배리지 시트 셰이더(JC/VFX/TaoBarrageSheet)의 커스텀 머티리얼 GUI — 명시 저장 버튼(260807 공식 규격).
    /// 기본 프로퍼티 UI 그대로 + 하단 💾 버튼: 조절값은 메모리에 유지되고, 클릭 시에만 파일로 확정.
    /// 시트·베일 재질 4종(B/A)이 이 셰이더를 쓰므로 전부 커버 — 다른 셰이더 재질에는 영향 없음.
    /// </summary>
    public class TaoBarrageSheetGUI : ShaderGUI
    {
        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            base.OnGUI(materialEditor, properties);
            EditorGUILayout.Space();
            if (GUILayout.Button("💾 디스크 저장 (조절값 파일 확정)", GUILayout.Height(28)))
            {
                foreach (var t in materialEditor.targets)
                {
                    if (t == null) continue;
                    EditorUtility.SetDirty(t);
                    AssetDatabase.SaveAssetIfDirty(t);
                    Debug.Log($"[JC] 디스크 저장 완료: {t.name}");
                }
            }
        }
    }
}

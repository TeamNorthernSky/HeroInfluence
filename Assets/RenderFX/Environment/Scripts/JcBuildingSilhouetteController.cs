using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// JC_Environment의 건물 반투명 및 메시 가림 판정 튜닝 창구.
/// 공유 머티리얼 및 프리셋 에셋은 런타임에 수정하지 않는다.
/// </summary>
[ExecuteAlways, DisallowMultipleComponent]
[AddComponentMenu("JC Environment/건물 반투명 설정")]
public sealed class JcBuildingSilhouetteController : MonoBehaviour
{
    [SerializeField, Tooltip("저장/불러오기 대상. 화면에서 조절하는 값과 분리되어 있습니다.")]
    private JcBuildingSilhouettePreset preset;

    [SerializeField, Tooltip("플레이 시작 및 플레이 중 재활성화 시 저장된 프리셋을 불러옵니다.")]
    private bool loadPresetOnPlay = true;

    [SerializeField] private JcBuildingSilhouetteSettings settings = JcBuildingSilhouetteSettings.Default;

    private static readonly List<JcBuildingSilhouetteController> Controllers = new List<JcBuildingSilhouetteController>();

    public JcBuildingSilhouettePreset Preset { get => preset; set => preset = value; }
    public JcBuildingSilhouetteSettings Settings { get => settings.Sanitized(); set => settings = value.Sanitized(); }

    private void OnEnable()
    {
        if (!Controllers.Contains(this)) Controllers.Add(this);
        if (Application.IsPlaying(gameObject) && loadPresetOnPlay) LoadPreset();
    }

    private void OnDisable() => Controllers.Remove(this);
    private void OnDestroy() => Controllers.Remove(this);
    private void OnValidate() => settings = settings.Sanitized();

    private void OnDrawGizmosSelected()
    {
        var value = Settings;
        if (!value.showOcclusionBox) return;
        foreach (var registry in FindObjectsByType<PartyRegistry>(FindObjectsSortMode.None))
        {
            if (registry.gameObject.scene != gameObject.scene || registry.PlayerParty == null) continue;
            Vector3 center = registry.PlayerParty.transform.position + value.occlusionBoxOffset;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(center, value.occlusionBoxSize);
            Gizmos.DrawSphere(center, 0.04f);
        }
    }

    [ContextMenu("프리셋 불러오기")]
    public void LoadPreset()
    {
        if (preset != null) Settings = preset.settings;
    }

    public static bool TryGetSettings(Camera camera, out JcBuildingSilhouetteSettings value)
    {
        value = default;
        if (camera == null) return false;
        Scene scene = camera.cameraType == CameraType.SceneView
            ? SceneManager.GetActiveScene() : camera.gameObject.scene;

        // 다른 씬/프리뷰 카메라에는 설정이 새지 않는다. 중복 설치는 설치 메뉴에서 방지한다.
        for (int i = Controllers.Count - 1; i >= 0; i--)
        {
            var controller = Controllers[i];
            if (controller == null) { Controllers.RemoveAt(i); continue; }
            if (!controller.isActiveAndEnabled || controller.gameObject.scene != scene) continue;
            value = controller.Settings;
            return true;
        }
        return false;
    }
}

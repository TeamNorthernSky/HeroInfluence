using System.Collections.Generic;
using UnityEngine;

// JC | 2026-11-13 | 씬별 초안을 등록하므로 배지 갱신마다 오브젝트 검색이나 프로필 복사를 하지 않습니다.
[ExecuteAlways, DisallowMultipleComponent]
public sealed class JcInfluenceBadgeSettings : MonoBehaviour
{
    public JcInfluenceBadgeProfile profile;
    [SerializeField] private JcInfluenceBadgeConfiguration draft;
    static readonly Dictionary<int, JcInfluenceBadgeSettings> Scenes = new Dictionary<int, JcInfluenceBadgeSettings>();
    public JcInfluenceBadgeConfiguration Draft { get { if (draft == null) LoadProfile(); return draft; } }
    public static JcInfluenceBadgeSettings ForScene(int scene) => Scenes.TryGetValue(scene, out var value) && value && value.isActiveAndEnabled ? value : null;
    void OnEnable() { LoadProfile(); Scenes[gameObject.scene.handle] = this; }
    void OnDisable() { if (Scenes.TryGetValue(gameObject.scene.handle, out var value) && value == this) Scenes.Remove(gameObject.scene.handle); }
    public void LoadProfile() => draft = profile ? profile.settings.Copy() : new JcInfluenceBadgeConfiguration();
}

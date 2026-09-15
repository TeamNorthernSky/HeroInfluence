using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace JC.Env
{
    /// <summary>DOF 조정값과 실행용 Volume 사본을 소유합니다. 에셋 저장은 Editor Inspector에서만 수행합니다.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class JcDofController : MonoBehaviour
    {
        [Tooltip("불러오기·저장에 사용할 DOF 프로파일. 연결만 바꾸면 현재 값은 유지됩니다. 불러오기 버튼으로 적용하세요.")]
        [SerializeField] private JcDofProfile profile;
        [Tooltip("실시간 값을 적용할 기존 DOF Volume입니다. 이 연결은 씬에 저장되며 프로파일 저장 대상은 아닙니다.")]
        [SerializeField] private Volume targetVolume;
        [Tooltip("자동 초점용 게임 카메라. 비우면 같은 씬의 Camera.main을 사용합니다. 연결은 씬에만 저장됩니다.")]
        [SerializeField] private Camera targetCamera;
        [Tooltip("플레이 시작 때 연결된 프로파일의 값을 한 번 불러옵니다. 끄면 씬에 저장된 현재 조정값으로 시작합니다. 이 옵션은 씬에 저장됩니다.")]
        [SerializeField] private bool loadProfileOnPlay = true;
        [Tooltip("현재 조정값입니다. 변경은 화면에 즉시 반영되며 프로파일 파일은 저장 버튼을 눌러야 변경됩니다.")]
        [SerializeField] private JcDofSettings settings = JcDofSettings.Default;

        private static readonly Dictionary<Volume, JcDofController> Owners = new Dictionary<Volume, JcDofController>();
        private Volume _boundVolume;
        private VolumeProfile _previousProfile;
        private VolumeProfile _runtimeProfile;
        private DepthOfField _dof;
        private bool _wasPlaying;

        public JcDofProfile Profile { get => profile; set => profile = value; }
        public Volume TargetVolume { get => targetVolume; set { targetVolume = value; ApplyNow(); } }
        public Camera TargetCamera { get => targetCamera; set { targetCamera = value; ApplyNow(); } }
        public JcDofSettings Settings { get => settings; set { settings = value.Sanitized(); ApplyNow(); } }
        public float CurrentFocusDistance { get; private set; }
        public float CurrentGaussianStart { get; private set; }
        public float CurrentGaussianEnd { get; private set; }
        public bool HasAutoFocus { get; private set; }
        public string Status { get; private set; }

        internal static bool IsDriving(Volume volume)
            => volume != null && Owners.TryGetValue(volume, out var owner) && owner != null && owner.isActiveAndEnabled;

        private void OnEnable()
        {
            _wasPlaying = Application.IsPlaying(gameObject);
            if (_wasPlaying && loadProfileOnPlay && profile != null) LoadProfile();
            else ApplyNow();
        }

        private void OnValidate() => settings = settings.Sanitized();

        private void LateUpdate()
        {
            // 도메인/씬 리로드를 끈 플레이 진입에서도 시작 프로파일을 한 번 적용합니다.
            bool playing = Application.IsPlaying(gameObject);
            if (playing && !_wasPlaying && loadProfileOnPlay && profile != null) settings = profile.settings.Sanitized();
            _wasPlaying = playing;
            ApplyNow();
        }

        private void OnDisable() => ReleaseVolume();
        private void OnDestroy() => ReleaseVolume();

        public void LoadProfile()
        {
            if (profile != null) Settings = profile.settings;
        }

        public void ApplyNow()
        {
            if (!isActiveAndEnabled) return;
            if (_boundVolume != targetVolume || _runtimeProfile == null) ReleaseVolume();
            HasAutoFocus = false;
            Status = null;
            if (targetVolume == null) { Status = "적용할 DOF Volume을 연결하세요."; return; }
            if (_runtimeProfile == null && !BindVolume()) return;

            var s = settings.Sanitized();
            float focus = s.focusDistance;
            if (s.autoFocus && s.mode != DepthOfFieldMode.Off)
            {
                var cam = targetCamera != null ? targetCamera : Camera.main;
                if (cam != null && (targetCamera != null || cam.gameObject.scene == gameObject.scene))
                {
                    var plane = new Plane(Vector3.up, new Vector3(0f, s.groundY, 0f));
                    HasAutoFocus = plane.Raycast(new Ray(cam.transform.position, cam.transform.forward), out float distance) && distance > 0f;
                    if (HasAutoFocus) focus = Mathf.Max(0.1f, distance);
                }
                if (!HasAutoFocus) Status = "자동 초점용 카메라 또는 전방 바닥 교차점을 찾지 못해 수동 거리를 적용합니다.";
            }

            CurrentFocusDistance = focus;
            CurrentGaussianStart = HasAutoFocus ? Mathf.Max(0f, focus + s.gaussianStartOffset) : s.gaussianStart;
            CurrentGaussianEnd = Mathf.Max(CurrentGaussianStart + 0.01f,
                HasAutoFocus ? focus + s.gaussianEndOffset : s.gaussianEnd);
            _dof.active = true;
            _dof.mode.Override(s.mode);
            _dof.gaussianStart.Override(CurrentGaussianStart);
            _dof.gaussianEnd.Override(CurrentGaussianEnd);
            _dof.gaussianMaxRadius.Override(s.gaussianMaxRadius);
            _dof.highQualitySampling.Override(s.highQualitySampling);
            _dof.focusDistance.Override(focus);
            _dof.aperture.Override(s.aperture);
            _dof.focalLength.Override(s.focalLength);
            _dof.bladeCount.Override(s.bladeCount);
            _dof.bladeCurvature.Override(s.bladeCurvature);
            _dof.bladeRotation.Override(s.bladeRotation);
            if (!targetVolume.isActiveAndEnabled || targetVolume.weight <= 0f)
                Status = "대상 Volume이 비활성 또는 Weight 0입니다. 설정은 반영되지만 화면 효과는 나오지 않습니다.";
        }

        private bool BindVolume()
        {
            if (IsDriving(targetVolume) && Owners[targetVolume] != this)
            {
                Status = "같은 Volume을 다른 DOF 컨트롤러가 사용 중입니다.";
                return false;
            }
            _boundVolume = targetVolume;
            // 기존 추적기의 사본도 보존하여 컨트롤러를 끄면 원래 경로로 복귀합니다.
            _previousProfile = targetVolume.HasInstantiatedProfile() ? targetVolume.profile : null;
            var source = _previousProfile != null ? _previousProfile : targetVolume.sharedProfile;
            _runtimeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            _runtimeProfile.name = "JC_DOF (실시간 사본)";
            _runtimeProfile.hideFlags = HideFlags.HideAndDontSave;
            if (source != null)
                foreach (var component in source.components)
                {
                    if (component == null) continue;
                    var copy = Instantiate(component);
                    copy.hideFlags = HideFlags.HideAndDontSave;
                    _runtimeProfile.components.Add(copy);
                }
            if (!_runtimeProfile.TryGet(out _dof)) _dof = _runtimeProfile.Add<DepthOfField>(true);
            _dof.hideFlags = HideFlags.HideAndDontSave;
            targetVolume.profile = _runtimeProfile;
            Owners[targetVolume] = this;
            return true;
        }

        private void ReleaseVolume()
        {
            if (!ReferenceEquals(_boundVolume, null))
            {
                if (Owners.TryGetValue(_boundVolume, out var owner) && owner == this) Owners.Remove(_boundVolume);
                if (_boundVolume != null && _boundVolume.HasInstantiatedProfile() && _boundVolume.profile == _runtimeProfile)
                    _boundVolume.profile = _previousProfile;
            }
            if (_runtimeProfile != null)
            {
                foreach (var component in _runtimeProfile.components) DestroyOwned(component);
                DestroyOwned(_runtimeProfile);
            }
            _runtimeProfile = null;
            _previousProfile = null;
            _boundVolume = null;
            _dof = null;
        }

        private static void DestroyOwned(Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }
    }
}

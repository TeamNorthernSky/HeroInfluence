using UnityEngine;

namespace HeroInfluence.Events
{
    [DisallowMultipleComponent]
    public sealed class ArnoldEntrancePlayer : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private AnimationClip fallingPose;
        [SerializeField] private AnimationClip landingPose;
        [SerializeField, Tooltip("바닥에 닿는 순간 한 번 재생할 착지 먼지입니다.")] private GameObject landingEffectPrefab;
        [SerializeField, Min(0.01f), Tooltip("원본 이펙트 크기에 곱하는 배율입니다.")] private float landingEffectScale = 0.25f;
        [SerializeField, Min(0f)] private float dropHeight = 3.5f;
        [SerializeField, Min(0.05f)] private float fallSeconds = 0.65f;
        [SerializeField, Min(0.05f)] private float landingSeconds = 0.28f;
        private Vector3 landingPosition;
        private Vector3 restingLocalPosition;
        private bool originalRootMotion;
        private AnimatorCullingMode originalCullingMode;
        private float[] originalLayerWeights;
        private readonly System.Collections.Generic.List<Behaviour> pausedControllers = new System.Collections.Generic.List<Behaviour>();
        private bool running;
        private bool landed;
        private GameObject landingEffect;
        private CameraShakeEffect landingCamera;

        public bool IsPlaying => running;

        private void Awake()
        {
            ResolveVisual();
        }

        public bool PlayEntrance()
        {
            if (!Application.isPlaying || !isActiveAndEnabled || running)
                return false;

            ResolveVisual();
            if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null ||
                visualRoot == null || visualRoot == transform || !visualRoot.IsChildOf(transform) ||
                fallingPose == null || landingPose == null)
            {
                Debug.LogWarning("Arnold entrance requires an active Animator, child visual and pose clips.", this);
                return false;
            }
            foreach (string state in new[] { "Falling", "Landing", "Recover", "Idle" })
                if (!animator.HasState(0, Animator.StringToHash("Base Layer." + state)))
                {
                    Debug.LogWarning($"Arnold entrance state is missing: '{state}'.", this);
                    return false;
                }

            BeginEntrance();
            return true;
        }

        private void ResolveVisual()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
            if (visualRoot == null && animator != null) visualRoot = animator.transform;
        }

        private void BeginEntrance()
        {
            landingPosition = visualRoot.position;
            restingLocalPosition = visualRoot.localPosition;
            originalRootMotion = animator.applyRootMotion;
            originalCullingMode = animator.cullingMode;
            PauseController(GetComponent<UnitVisualMotionController>());
            PauseController(GetComponent<BattleTurnIdlePose>());
            PauseController(GetComponentInParent<EnemyMovementVisualController>());
            originalLayerWeights = new float[animator.layerCount];
            for (int i = 1; i < animator.layerCount; i++)
            {
                originalLayerWeights[i] = animator.GetLayerWeight(i);
                animator.SetLayerWeight(i, 0f);
            }
            landed = false;
            if (landingEffect != null) Destroy(landingEffect);
            running = true;
            visualRoot.position = landingPosition + Vector3.up * dropHeight;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.SetFloat("FallSpeed", fallingPose.length / Mathf.Max(0.05f, fallSeconds));
            animator.SetFloat("LandSpeed", landingPose.length / Mathf.Max(0.05f, landingSeconds));
            animator.Play("Falling", 0, 0f);
            animator.Update(0f);
        }

        private void PauseController(Behaviour controller)
        {
            if (controller == null || !controller.enabled) return;
            pausedControllers.Add(controller);
            controller.enabled = false;
        }

        private void LateUpdate()
        {
            if (!running) return;
            if (animator == null || !animator.isActiveAndEnabled || visualRoot == null)
            {
                CancelEntrance();
                return;
            }

            var state = animator.GetCurrentAnimatorStateInfo(0);
            if (state.IsName("Recover") && state.normalizedTime >= 1f)
            {
                FinishEntrance();
                return;
            }
            if (animator.IsInTransition(0))
                state = animator.GetNextAnimatorStateInfo(0);

            if (state.IsName("Falling"))
            {
                float t = Mathf.Clamp01(state.normalizedTime);
                visualRoot.position = landingPosition + Vector3.up * (dropHeight * (1f - t * t));
            }
            else if (state.IsName("Landing") || state.IsName("Recover"))
            {
                visualRoot.position = landingPosition;
                if (!landed)
                {
                    landed = true;
                    PlayLandingEffect();
                    Camera camera = Camera.main;
                    if (camera != null && camera.gameObject.scene == gameObject.scene)
                        landingCamera = JcArnoldShake.Landing(camera, this);
                }
            }
            else if (state.IsName("Idle"))
                FinishEntrance();
        }

        private void FinishEntrance()
        {
            if (visualRoot != null) visualRoot.localPosition = restingLocalPosition;
            if (animator != null)
            {
                if (animator.isActiveAndEnabled)
                {
                    animator.Play("Idle", 0, 0f);
                    animator.Update(0f);
                }
                for (int i = 1; i < animator.layerCount && i < originalLayerWeights.Length; i++)
                    animator.SetLayerWeight(i, originalLayerWeights[i]);
                animator.applyRootMotion = originalRootMotion;
                animator.cullingMode = originalCullingMode;
            }
            running = false;
            foreach (Behaviour controller in pausedControllers)
                if (controller != null) controller.enabled = true;
            pausedControllers.Clear();
        }

        public void CancelEntrance()
        {
            if (landingCamera != null) landingCamera.CancelImpact(this);
            landingCamera = null;
            if (running) FinishEntrance();
            if (landingEffect != null) Destroy(landingEffect);
        }

        private void PlayLandingEffect()
        {
            if (landingEffectPrefab == null) return;
            landingEffect = Instantiate(landingEffectPrefab, landingPosition, landingEffectPrefab.transform.rotation);
            landingEffect.transform.localScale *= landingEffectScale;
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(landingEffect, gameObject.scene);
            var particles = landingEffect.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var particle in particles)
            {
                particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                // 원본 이펙트는 보존하고, 생성된 복사본만 일회성으로 재생합니다.
                var main = particle.main;
                main.loop = false;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            }
            foreach (var particle in particles)
                if (particle.gameObject.activeInHierarchy) particle.Play(false);
            StartCoroutine(ReleaseLandingEffect(landingEffect, particles));
        }

        private System.Collections.IEnumerator ReleaseLandingEffect(GameObject instance, ParticleSystem[] particles)
        {
            yield return null;
            while (instance != null)
            {
                bool alive = false;
                foreach (var particle in particles)
                    if (particle != null && particle.IsAlive(false)) { alive = true; break; }
                if (!alive) break;
                yield return null;
            }
            if (instance != null) Destroy(instance);
        }

        private void OnDisable()
        {
            CancelEntrance();
        }
    }
}

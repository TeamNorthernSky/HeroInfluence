using UnityEngine;

namespace HeroInfluence.Events
{
    [DisallowMultipleComponent]
    public sealed class ArnoldEntrancePlayer : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private AnimationClip fallingPose;
        [SerializeField] private AnimationClip landingPose;
        [SerializeField, Tooltip("바닥에 닿는 순간 한 번 재생할 착지 먼지입니다.")] private GameObject landingEffectPrefab;
        [SerializeField, Min(0.01f), Tooltip("원본 이펙트 크기에 곱하는 배율입니다.")] private float landingEffectScale = 0.25f;
        [SerializeField, Min(0f)] private float dropHeight = 3.5f;
        [SerializeField, Min(0.05f)] private float fallSeconds = 0.65f;
        [SerializeField, Min(0.05f)] private float landingSeconds = 0.28f;
        [SerializeField, Min(0f)] private float walkDistance = 2f;
        [SerializeField, Min(0.05f)] private float walkSpeed = 0.85f;
        [SerializeField] private bool playOnStart = true;
        [SerializeField, Tooltip("테스트용 반복 재생입니다.")] private bool repeat;
        [SerializeField, Min(0.1f)] private float repeatDelay = 1.5f;

        private Vector3 landingPosition;
        private Vector3 walkDirection;
        private float walked;
        private float currentWalkSpeed;
        private float repeatTimer;
        private bool running;
        private bool landed;
        private GameObject landingEffect;

        public bool IsPlaying => running;

        private void Awake()
        {
            landingPosition = transform.position;
            walkDirection = transform.forward;
            if (animator == null) animator = GetComponentInChildren<Animator>();
        }

        private void Start()
        {
            if (playOnStart) Replay();
        }

        [ContextMenu("등장 연출 다시 재생 (Play Mode)")]
        public void Replay()
        {
            if (!Application.isPlaying || animator == null || fallingPose == null || landingPose == null)
                return;

            walked = 0f;
            landed = false;
            if (landingEffect != null) Destroy(landingEffect);
            currentWalkSpeed = 0f;
            repeatTimer = 0f;
            running = true;
            transform.position = landingPosition + Vector3.up * dropHeight;
            animator.applyRootMotion = false;
            animator.SetBool("WalkFinished", false);
            animator.SetFloat("FallSpeed", fallingPose.length / Mathf.Max(0.05f, fallSeconds));
            animator.SetFloat("LandSpeed", landingPose.length / Mathf.Max(0.05f, landingSeconds));
            animator.Play("Falling", 0, 0f);
            animator.Update(0f);
        }

        private void LateUpdate()
        {
            if (!running)
            {
                if (repeat && playOnStart && animator != null)
                {
                    repeatTimer += Time.deltaTime;
                    if (repeatTimer >= repeatDelay) Replay();
                }
                return;
            }

            var state = animator.GetCurrentAnimatorStateInfo(0);
            if (animator.IsInTransition(0))
                state = animator.GetNextAnimatorStateInfo(0);

            if (state.IsName("Falling"))
            {
                float t = Mathf.Clamp01(state.normalizedTime);
                transform.position = landingPosition + Vector3.up * (dropHeight * (1f - t * t));
            }
            else if (state.IsName("Landing") || state.IsName("Recover"))
            {
                transform.position = landingPosition;
                if (!landed)
                {
                    landed = true;
                    PlayLandingEffect();
                }
            }
            else if (state.IsName("Walk"))
            {
                // 무릎을 펴는 동작에서 첫 발을 내딛는 동안 이동 속도도 부드럽게 올립니다.
                currentWalkSpeed = Mathf.MoveTowards(currentWalkSpeed, walkSpeed, walkSpeed * Time.deltaTime / 0.18f);
                walked = Mathf.MoveTowards(walked, walkDistance, currentWalkSpeed * Time.deltaTime);
                transform.position = landingPosition + walkDirection * walked;
                if (walked >= walkDistance) animator.SetBool("WalkFinished", true);
            }
            else if (state.IsName("Idle"))
            {
                transform.position = landingPosition + walkDirection * walked;
                running = false;
                repeatTimer = 0f;
            }
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
            if (running) transform.position = landingPosition + walkDirection * walked;
            running = false;
            if (landingEffect != null) Destroy(landingEffect);
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

/// <summary>
/// 절망의 굴렁쇠(FV40005) 임시 외형. 원본 원기둥(Model) 대신 톱니 바퀴를 만들어 띄우고 회전시킨다.
/// <list type="bullet">
/// <item>외형: 링 + 금속판 8개 + 보라 에너지 링 3개. 소환 시 아래에서 솟아오른 뒤 부유·회전.
///   프리셋에 wheelVisualPrefab(Ultra Mega Magic Orb)이 있으면 그 프리팹으로 대신한다. 파괴도 wheelBreakPrefab이 있으면 대신한다.
///   이때 중심 높이는 보이는 반지름+부유 진폭+바닥 간격 이상으로 유지한다.</item>
/// <item>원기둥 재등장 방지: BattleCharactor.EnableVisuals(부활·초기 상태 설정)가 Awake 때 기록한 렌더러를 다시 켜므로,
///   직후 발생하는 OnHpChanged에서 살아 있으면 Model 렌더러를 다시 끈다.</item>
/// <item>소환: 프리셋에 summonPortalPrefab이 있으면 코드 소환진 대신 바닥 포털을 재생한다(기획 Y07).</item>
/// <item>사망: (프리팹 외형이면) 중심광을 한 번 키운 뒤 바퀴를 숨기고 파괴 이펙트를 씬 루트에 생성한다(시체 제거보다 오래 남아야 한다, 기획 Y10).</item>
/// <item>돌진 위치 유지: 시퀀스가 <see cref="BeginDashHold"/>를 부르면 원위치를 기억한다. 반격·자해까지 끝난 뒤 발행되는
///   BattleManager.OnSkillResolved에서 살아 있으면 원위치로 되돌린다. 사망 시에는 그 자리를 유지하고, 부활하면 되돌린다.</item>
/// </list>
/// transform·렌더러만 만지고 게임 상태는 바꾸지 않는다. 원본: JcYuliaUnitVfx(JC 전투 테스트 씬).
/// </summary>
[DisallowMultipleComponent]
public sealed class HoopWheelVisual : MonoBehaviour
{
    private const string JcVisualTypeName = "JcYuliaUnitVfx";
    private const float WheelRadius = .48f;
    private const float WheelHeight = .72f;
    private const float WheelFloorClearance = .2f;
    private const float SummonDropDistance = .7f;
    private const float SpinDegreesPerSecond = 35f;
    private const float HoverFrequency = 2.3f;
    private const float PortalLift = .02f;

    [Tooltip("피격·사망·부활 이벤트원이자 돌진으로 움직이는 루트. 프리팹 루트의 BattleCharactor를 연결한다.")]
    [SerializeField] private BattleCharactor owner;
    [Tooltip("숨길 원본 외형(원기둥) 루트. 이 하위 Renderer만 끈다.")]
    [SerializeField] private Transform modelRoot;
    [SerializeField] private HoopVfxPreset preset;

    private Transform _wheel;
    private float _wheelHeight = WheelHeight;
    private Mesh _wheelMesh;
    private bool _built;
    private bool _deadVisual;
    private float _spawnElapsed;
    private float _spin;
    private bool _dashHoldActive;
    private Vector3 _dashHome;
    private bool _ownerSubscribed;
    private BattleManager _subscribedBattle;
    private Coroutine _breakFlash;
    private Vector3 _breakFlashBaseScale;
    private Vector3 _breakFlashBasePosition;
    private GameObject _breakEffect;
    private readonly List<VisualEffect> _flashEffects = new List<VisualEffect>();
    private readonly List<(VisualEffect effect, int id, Vector4 color)> _flashColors = new List<(VisualEffect, int, Vector4)>();
    private readonly List<VFXExposedProperty> _exposed = new List<VFXExposedProperty>();

    /// <summary>충전·궤적 이펙트 기준점. 바퀴가 없으면 루트.</summary>
    public Transform WheelCenter => _wheel != null && _wheel.gameObject.activeInHierarchy ? _wheel : transform;
    public HoopVfxPreset Preset => preset;
    internal bool IsDashHoldActive => _dashHoldActive;

    private void OnEnable()
    {
        if (owner != null && !_ownerSubscribed)
        {
            owner.OnHpChanged += HandleHpChanged;
            owner.OnDied += HandleDied;
            _ownerSubscribed = true;
        }
    }

    private void OnDisable()
    {
        if (owner != null && _ownerSubscribed)
        {
            owner.OnHpChanged -= HandleHpChanged;
            owner.OnDied -= HandleDied;
            _ownerSubscribed = false;
        }
        SubscribeBattle(null);
        CancelBreakFlash();
    }

    private void Start()
    {
        // JC 전투 테스트 씬은 같은 프리팹에 자체 외형(JcYuliaUnitVfx)을 Instantiate 직후 붙인다 → 이중 외형 방지.
        // ASB 코드가 JC 타입에 의존하지 않도록 문자열 타입명으로 조회한다.
        if (GetComponent(JcVisualTypeName) != null)
        {
            enabled = false;
            return;
        }

        if (owner == null || preset == null)
        {
            Debug.LogWarning($"[HoopWheelVisual] owner/preset 미연결 → 원본 외형 유지. ({name})", this);
            enabled = false;
            return;
        }

        if (modelRoot == null)
        {
            modelRoot = transform.Find("Model");
            Debug.LogWarning($"[HoopWheelVisual] modelRoot 미연결 → 자식 'Model'로 폴백({(modelRoot != null ? "찾음" : "없음")}). ({name})", this);
        }

        BuildWheel();
        if (owner.IsDead)
        {
            _deadVisual = true;
            _wheel.gameObject.SetActive(false);
        }
        else
        {
            SpawnSummon();
            UnitSfxProfile.Play(owner, UnitSfxEvent.Summon);
        }
    }

    private void SpawnSummon()
    {
        if (preset.summonPortalPrefab == null)
        {
            HoopVfxEffect.Spawn(preset, HoopEffectKind.Summon, transform.position);
            return;
        }

        // 씬 루트에 생성: 포털은 바닥에 남아 링만 사라진다. 회전·크기는 프리팹 루트 값(바닥을 향하게 맞춘 값).
        GameObject portal = Instantiate(preset.summonPortalPrefab, transform.position + Vector3.up * PortalLift,
            preset.summonPortalPrefab.transform.rotation);
        JC.VFX.VfxEffect effect = portal.GetComponent<JC.VFX.VfxEffect>();
        if (effect != null)
        {
            effect.PlaybackSpeed = BattleManager.Instance != null ? Mathf.Max(.01f, BattleManager.Instance.CurrentBattleSpeed) : 1f;
            effect.Play();
        }
        else
        {
            EffectFallbackRelease.Ensure(portal, preset.summonSeconds);
        }
    }

    private void LateUpdate()
    {
        // BattleManager는 씬마다 다르고 생성 순서도 보장되지 않아 지연 구독한다.
        if (!ReferenceEquals(_subscribedBattle, BattleManager.Instance))
        {
            SubscribeBattle(BattleManager.Instance);
        }

        if (!_built || _deadVisual || owner == null || owner.IsDead) return;

        float dt = Time.deltaTime * BattleSpeed();
        _spawnElapsed += dt;

        Vector3 p = Vector3.up * _wheelHeight;
        p.y += Mathf.Sin(_spawnElapsed * HoverFrequency) * preset.hoverHeight;
        p.y -= Mathf.Max(0f, 1f - _spawnElapsed / preset.summonSeconds) * SummonDropDistance;
        if (preset.wheelVisualPrefab != null)
            p.y = Mathf.Max(p.y, preset.wheelVisualRadius + WheelFloorClearance);
        _wheel.localPosition = p;

        _spin = (_spin + dt * SpinDegreesPerSecond) % 360f;
        Camera cam = Camera.main;
        if (cam != null)
        {
            _wheel.rotation = Quaternion.LookRotation(cam.transform.position - _wheel.position, Vector3.up)
                              * Quaternion.Euler(0f, 0f, _spin);
        }
    }

    private void OnDestroy()
    {
        if (_wheelMesh != null) Destroy(_wheelMesh);
    }

    /// <summary>돌진 도착 위치를 유지한다. home은 돌진 전 루트 월드 위치.</summary>
    public void BeginDashHold(Vector3 home)
    {
        _dashHoldActive = true;
        _dashHome = home;
    }

    // 반격 드레인 + OnPostExecution(자해) 이후 발행된다. 살아남았으면(자해 무효 등 예외) 원위치로 되돌린다.
    internal void HandleSkillResolved(SkillResolutionContext context)
    {
        if (!_dashHoldActive || context == null || owner == null || context.Actor != owner) return;
        if (owner.IsDead) return;   // 사망: 파괴 이펙트가 도착 위치에 나오도록 유지. 부활 시 되돌린다.
        RestoreDashHome();
    }

    private void RestoreDashHome()
    {
        if (!_dashHoldActive) return;
        _dashHoldActive = false;
        if (owner != null) owner.transform.position = _dashHome;
    }

    private void HandleHpChanged(float current, float max)
    {
        if (!_built || owner == null || owner.IsDead || current <= 0f) return;

        // EnableVisuals가 원본 렌더러를 다시 켰을 수 있다(부활·초기 상태 설정) → 항상 재보장.
        SetModelRenderersEnabled(false);
        if (_deadVisual)
        {
            CancelBreakFlash();
            if (_breakEffect != null) Destroy(_breakEffect);
            _breakEffect = null;
            _deadVisual = false;
            _wheel.gameObject.SetActive(true);
            RestoreDashHome();
        }
    }

    private void HandleDied(BattleCharactor _)
    {
        if (!_built || _deadVisual) return;
        _deadVisual = true;   // 이후 LateUpdate는 부유·회전을 멈춘다(중심광은 그 자리에서).
        UnitSfxProfile.Play(owner, UnitSfxEvent.Death);
        // 연기를 중심광과 같은 프레임에 시작해 두 재료가 하나의 파괴로 이어지게 한다.
        SpawnBreakEffect();
        if (preset.wheelVisualPrefab != null && preset.breakFlashSeconds > 0f && isActiveAndEnabled)
        {
            _breakFlashBaseScale = _wheel.localScale;
            _breakFlashBasePosition = _wheel.localPosition;
            _breakFlash = StartCoroutine(BreakFlash());
            return;
        }

        BreakWheel();
    }

    // 기획 Y10: 구체 중심광 1회 증광 → 구체 제거와 연기 방출 동시.
    private IEnumerator BreakFlash()
    {
        CacheFlashColors();
        for (float t = 0f; t < preset.breakFlashSeconds; t += Time.deltaTime * BattleSpeed())
        {
            float k = BreakFlashProgress(t / preset.breakFlashSeconds);
            float scale = Mathf.Lerp(1f, preset.breakFlashScale, k);
            _wheel.localScale = _breakFlashBaseScale * scale;
            Vector3 position = _breakFlashBasePosition;
            position.y = Mathf.Max(position.y, preset.wheelVisualRadius * scale + WheelFloorClearance);
            _wheel.localPosition = position;
            ApplyFlashColors(Mathf.Lerp(1f, preset.breakFlashIntensity, k));
            yield return null;
        }

        _breakFlash = null;
        BreakWheel();
        // 부활에 대비해 원래 외형으로 돌려 둔다(숨긴 상태).
        _wheel.localScale = _breakFlashBaseScale;
        _wheel.localPosition = _breakFlashBasePosition;
        ApplyFlashColors(1f);
    }

    private void BreakWheel()
    {
        SpawnBreakEffect();
        _wheel.gameObject.SetActive(false);
    }

    private void SpawnBreakEffect()
    {
        if (_breakEffect != null) return;
        if (preset.wheelBreakPrefab != null)
        {
            // 씬 루트에 생성: 굴렁쇠 오브젝트는 사망 0.8초 뒤 파괴되지만 연기는 그보다 오래 남아야 한다.
            GameObject smoke = Instantiate(preset.wheelBreakPrefab, _wheel.position, Quaternion.identity);
            _breakEffect = smoke;
            JC.VFX.VfxEffect effect = smoke.GetComponent<JC.VFX.VfxEffect>();
            if (effect != null) effect.Play();
            else EffectFallbackRelease.Ensure(smoke, preset.destroySeconds);
        }
        else
        {
            HoopVfxEffect fallback = HoopVfxEffect.Spawn(preset, HoopEffectKind.WheelBreak, _wheel.position);
            _breakEffect = fallback != null ? fallback.gameObject : null;
        }
    }

    // 비활성화·부활로 중심광이 끊기면 외형을 원래대로 돌린다(연기는 내지 않는다).
    private void CancelBreakFlash()
    {
        if (_breakFlash == null) return;
        StopCoroutine(_breakFlash);
        _breakFlash = null;
        _wheel.localScale = _breakFlashBaseScale;
        _wheel.localPosition = _breakFlashBasePosition;
        ApplyFlashColors(1f);
        if (_wheel != null) _wheel.gameObject.SetActive(false);
    }

    // 프리팹 외형(Magic Orb)의 노출 색(Vector4) 속성을 기억해 두고 배율을 곱한다. 이름에 의존하지 않는다.
    private void CacheFlashColors()
    {
        _flashColors.Clear();
        _wheel.GetComponentsInChildren(true, _flashEffects);
        for (int i = 0; i < _flashEffects.Count; i++)
        {
            VisualEffect effect = _flashEffects[i];
            if (effect == null || effect.visualEffectAsset == null) continue;
            _exposed.Clear();
            effect.visualEffectAsset.GetExposedProperties(_exposed);
            for (int p = 0; p < _exposed.Count; p++)
            {
                if (_exposed[p].type != typeof(Vector4)) continue;
                int id = Shader.PropertyToID(_exposed[p].name);
                if (effect.HasVector4(id)) _flashColors.Add((effect, id, effect.GetVector4(id)));
            }
        }
    }

    private void ApplyFlashColors(float intensity)
    {
        for (int i = 0; i < _flashColors.Count; i++)
        {
            (VisualEffect effect, int id, Vector4 color) = _flashColors[i];
            if (effect != null) effect.SetVector4(id, ScaleColor(color, intensity));
        }
    }

    private static float BattleSpeed()
    {
        return BattleManager.Instance != null ? Mathf.Max(0f, BattleManager.Instance.CurrentBattleSpeed) : 1f;
    }

    /// <summary>중심광 진행도(0~1)에 따른 증광 정도. 점점 빨리 밝아진다(끝에서 연기로 바뀐다).</summary>
    internal static float BreakFlashProgress(float normalized)
    {
        float t = Mathf.Clamp01(normalized);
        return t * t;
    }

    /// <summary>HDR 색의 밝기만 배율로 키운다(알파 유지).</summary>
    internal static Vector4 ScaleColor(Vector4 color, float intensity)
    {
        return new Vector4(color.x * intensity, color.y * intensity, color.z * intensity, color.w);
    }

    private void SubscribeBattle(BattleManager battle)
    {
        if (_subscribedBattle != null) _subscribedBattle.OnSkillResolved -= HandleSkillResolved;
        _subscribedBattle = battle;
        if (_subscribedBattle != null) _subscribedBattle.OnSkillResolved += HandleSkillResolved;
    }

    private void SetModelRenderersEnabled(bool value)
    {
        if (modelRoot == null) return;
        Renderer[] renderers = modelRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null) renderers[i].enabled = value;
        }
    }

    private void BuildWheel()
    {
        SetModelRenderersEnabled(false);

        _wheelHeight = ResolveWheelHeight(preset);
        _wheel = new GameObject("HoopWheelVisual").transform;
        _wheel.SetParent(transform, false);
        float spawnHeight = _wheelHeight - SummonDropDistance;
        if (preset.wheelVisualPrefab != null)
            spawnHeight = Mathf.Max(spawnHeight, preset.wheelVisualRadius + WheelFloorClearance);
        _wheel.localPosition = Vector3.up * spawnHeight;

        if (preset.wheelVisualPrefab != null)
        {
            // Ultra Mega 외형(Magic Orb). 부유·회전·숨김은 _wheel이 그대로 담당한다.
            // Model 아래가 아니므로 SetModelRenderersEnabled가 VFXRenderer를 끄지 않는다.
            Instantiate(preset.wheelVisualPrefab, _wheel, false);
            _built = true;
            return;
        }

        _wheelMesh = CreateTorus(WheelRadius, .055f);
        _wheel.gameObject.AddComponent<MeshFilter>().sharedMesh = _wheelMesh;
        _wheel.gameObject.AddComponent<MeshRenderer>().sharedMaterial = preset.frameMaterial;

        // 금속 외곽 프레임 8개(톱니처럼 보이는 부분). 정식 모델 대신 쓰는 임시 외형.
        Mesh cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.PI * .25f;
            var frame = new GameObject("WheelFramePlate");
            frame.transform.SetParent(_wheel, false);
            frame.transform.localPosition = new Vector3(Mathf.Cos(a) * WheelRadius, Mathf.Sin(a) * WheelRadius, 0f);
            frame.transform.localRotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg);
            frame.transform.localScale = new Vector3(.16f, .09f, .12f);
            frame.AddComponent<MeshFilter>().sharedMesh = cube;
            frame.AddComponent<MeshRenderer>().sharedMaterial = preset.frameMaterial;
        }

        for (int i = 0; i < 3; i++)
        {
            var go = new GameObject("VioletWheelArc");
            go.transform.SetParent(_wheel, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = preset.energyMaterial;
            line.useWorldSpace = false;
            line.positionCount = 65;
            line.widthMultiplier = preset.lineWidth * (i == 0 ? .7f : .35f);
            line.startColor = line.endColor = i == 0 ? preset.core : preset.energy;
            float radius = .38f + i * .07f;
            for (int n = 0; n < 65; n++)
            {
                float a = n / 64f * Mathf.PI * 2f;
                line.SetPosition(n, new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, .012f));
            }
        }

        _built = true;
    }

    /// <summary>
    /// 바퀴 중심 높이(루트 기준). 프리팹 외형은 카메라를 향한 판이라 수평 시점에서 반지름만큼 내려간다 →
    /// 반지름+부유 진폭+바닥 간격 아래로 두지 않는다. 코드 외형(반지름 0.48)은 기존 높이를 그대로 쓴다.
    /// </summary>
    internal static float ResolveWheelHeight(HoopVfxPreset preset)
    {
        if (preset == null || preset.wheelVisualPrefab == null) return WheelHeight;
        return Mathf.Max(WheelHeight, preset.wheelVisualRadius + preset.hoverHeight + WheelFloorClearance);
    }

    private static Mesh CreateTorus(float radius, float tube)
    {
        const int rings = 48, sides = 8;
        var v = new Vector3[(rings + 1) * (sides + 1)];
        var uv = new Vector2[v.Length];
        var triangles = new int[rings * sides * 6];
        for (int r = 0; r <= rings; r++)
        {
            for (int s = 0; s <= sides; s++)
            {
                float a = r / (float)rings * Mathf.PI * 2f;
                float b = s / (float)sides * Mathf.PI * 2f;
                int n = r * (sides + 1) + s;
                v[n] = new Vector3(Mathf.Cos(a) * (radius + Mathf.Cos(b) * tube),
                                   Mathf.Sin(a) * (radius + Mathf.Cos(b) * tube),
                                   Mathf.Sin(b) * tube);
                uv[n] = new Vector2(r / (float)rings, s / (float)sides);
            }
        }

        int t = 0;
        for (int r = 0; r < rings; r++)
        {
            for (int s = 0; s < sides; s++)
            {
                int n = r * (sides + 1) + s;
                triangles[t++] = n; triangles[t++] = n + 1; triangles[t++] = n + sides + 1;
                triangles[t++] = n + 1; triangles[t++] = n + sides + 2; triangles[t++] = n + sides + 1;
            }
        }

        var mesh = new Mesh { name = "HoopTemporaryWheel" };
        mesh.vertices = v;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}

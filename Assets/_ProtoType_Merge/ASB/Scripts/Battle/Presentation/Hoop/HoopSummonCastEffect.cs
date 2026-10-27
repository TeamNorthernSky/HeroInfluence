using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

/// <summary>
/// 율리아 절망의 굴레(400013) 시전 연출: 기획 Y07 + 율리아 연출 시뮬레이션의 굴렁쇠 소환(원본: UliaVfx.YuliaCastVfx.CastAndSummon).
/// <list type="bullet">
/// <item>Spawn(yulia_cast_charge): 손 위에서 에너지 3갈래가 빠르게 돌며 커진다. 동시에 소환 칸마다 에너지 집결(top_down_bomb)이 시작된다.</item>
/// <item>Stop(yulia_cast_charge_end): 회전을 멈추고 각 칸으로 포물선 이동 → 바닥 링으로 변한다.</item>
/// <item>그 칸에 굴렁쇠가 생기면(<see cref="TryClaimSummon"/>) 집결 에너지를 터뜨리고 구체를 튀기며 보이게 한다
///   (<see cref="HoopWheelVisual.RevealFromSummon"/>). 이어서 바닥 링만 소거한다.</item>
/// </list>
/// 소환 판정·칸·시점은 바꾸지 않는다. 칸은 BossController 설정에서 읽기만 하고, 생성은 기존대로 시전 뒤 OnPostExecution.
/// 굴렁쇠가 끝내 생기지 않으면(칸 점유 등) 링만 소거한다. 시간은 전투 배속 기준.
/// </summary>
[DisallowMultipleComponent]
public sealed class HoopSummonCastEffect : MonoBehaviour, ISkillEffectBehaviour, ISkillEffectHandle
{
    private const string HoopEnemyId = "FV40005";
    private const float TileMatchDistance = .5f;
    // top_down_bomb_Core_Active 원본의 집결·방출 중심 높이(프리팹 로컬). 구체 중심에 맞춘다.
    private const float NativeGatherCenterHeight = 1.77f;
    private const float RingLift = .09f;
    private const float GatherFloorLift = .04f;

    private static readonly List<HoopSummonCastEffect> Active = new List<HoopSummonCastEffect>();

    [Header("Source VFX (UliaVfx 납품 패키지)")]
    [Tooltip("손 에너지·분리 구체 (5_vfx_MagicOrb08_skill).")]
    [SerializeField] private GameObject orbPrefab;
    [Tooltip("바닥 링 (5_Burst_rings_Ring_Summon, circles만 정지 상태로 사용).")]
    [SerializeField] private GameObject ringPrefab;
    [Tooltip("칸별 에너지 집결·방출 (top_down_bomb_Core_Active).")]
    [SerializeField] private GameObject gatherPrefab;
    [Tooltip("분리 구체의 꼬리 재질 (Orbs_electric_Ulia_SkillCast_notEdit의 orbs 재질).")]
    [SerializeField] private Material splitTrailMaterial;
    [Tooltip("굴렁쇠 구체 중심 높이를 HoopWheelVisual과 같게 계산한다.")]
    [SerializeField] private HoopVfxPreset preset;

    [Header("Size")]
    [Tooltip("율리아 키(m). 시뮬레이션 2.061252. 에너지·링·집결 크기의 기준.")]
    [SerializeField, Min(.1f)] private float characterHeight = 2.061252f;
    [Tooltip("바닥 링 지름 = 키 × 이 값. 시뮬레이션 1.12.")]
    [SerializeField, Min(.1f)] private float ringScaleByHeight = 1.12f;
    [Tooltip("칸별 집결 크기 = 키 × 이 값. 시뮬레이션 0.13.")]
    [SerializeField, Min(.01f)] private float gatherScaleByHeight = .13f;

    [Header("Timing (전투 배속 1 기준 초)")]
    [Tooltip("손 위 회전이 다 커지는 시간. 400013 Timeline의 충전 Cue 구간(0.02→0.45초)에 맞춘다.")]
    [SerializeField, Min(.05f)] private float spinGrowSeconds = .43f;
    [Tooltip("충전 종료 Cue가 오지 않아도 이 시간 뒤에는 칸으로 보낸다.")]
    [SerializeField, Min(.1f)] private float maxSpinSeconds = .9f;
    [Tooltip("손 위 회전 속도(도/초). 시뮬레이션 1800.")]
    [SerializeField] private float spinDegreesPerSecond = 1800f;
    [Tooltip("칸까지 포물선 이동 시간. 시뮬레이션 비율(시작 모션의 28%)을 400013 시전 길이에 맞춘 값.")]
    [SerializeField, Min(.01f)] private float splitTravelSeconds = .17f;
    [Tooltip("도착한 에너지가 바닥 링으로 펴지는 시간. 시뮬레이션 0.083.")]
    [SerializeField, Min(.01f)] private float ringMorphSeconds = .083f;
    [Tooltip("굴렁쇠가 나타난 뒤 바닥 링이 함께 보이는 시간.")]
    [SerializeField, Min(0f)] private float ringHoldSeconds = .6f;
    [Tooltip("링 소거 시간. 시뮬레이션 0.15.")]
    [SerializeField, Min(.01f)] private float ringFadeSeconds = .15f;
    [Tooltip("소환 완료 알림이 오지 않을 때 링을 정리하는 안전 시간.")]
    [SerializeField, Min(.1f)] private float releaseTimeoutSeconds = 10f;
    [Tooltip("집결 원본의 방출 시각(초). 시뮬레이션 2.7.")]
    [SerializeField, Min(.1f)] private float gatherNativeBurstSeconds = 2.7f;
    [Tooltip("시전 시작부터 방출까지 예상 시간. 집결 입자 속도 = 원본 방출 시각 ÷ 이 값.")]
    [SerializeField, Min(.1f)] private float expectedReleaseSeconds = .7f;

    private sealed class Tile
    {
        public Vector3 Floor;
        public HoopWheelVisual Visual;
        public GameObject Split;
        public GameObject Ring;
        public GameObject Gather;
        public float GatherTail;
        public readonly List<ParticleSystem> Release = new List<ParticleSystem>();
        public bool Released;
        public bool Closed;
    }

    private readonly List<Tile> _tiles = new List<Tile>();
    private Transform _root;
    private Transform _hand;
    private GameObject _core;
    private Vector3 _energyCenter;
    private float _orbitRadius;
    private float _spinAngle;
    private bool _orbiting;
    private bool _playing;
    private bool _splitRequested;
    private BattleCharactor _caster;
    private bool _summonCompleted;
    private int _openTiles;

    /// <summary>굴렁쇠 외형이 생길 때 호출: 이 칸으로 소환 에너지가 오는 중이면 등록하고 true(외형은 숨긴 채 기다린다).</summary>
    internal static bool TryClaimSummon(HoopWheelVisual visual)
    {
        if (visual == null) return false;
        Vector3 position = visual.transform.position;
        for (int i = Active.Count - 1; i >= 0; i--)
        {
            HoopSummonCastEffect effect = Active[i];
            if (effect == null)
            {
                Active.RemoveAt(i);
                continue;
            }
            if (effect.TryClaim(visual, position)) return true;
        }
        return false;
    }

    /// <summary>시전 후 실제 소환이 끝났음을 알린다. 미소환 칸만 정리하고, 소환된 칸은 굴렁쇠가 보인 뒤 링을 접는다.</summary>
    internal static void NotifySummonCompleted(BattleCharactor caster)
    {
        if (caster == null) return;
        for (int i = Active.Count - 1; i >= 0; i--)
        {
            HoopSummonCastEffect effect = Active[i];
            if (effect == null)
            {
                Active.RemoveAt(i);
                continue;
            }
            if (effect._caster == caster) effect._summonCompleted = true;
        }
    }

    public void Play(SkillEffectContext ctx)
    {
        if (_playing) return;
        _playing = true;
        _caster = ctx?.Caster;

        // held Cue라 손 소켓에 붙어 생성된다. 에너지는 월드에서 움직이므로 떼어 내고 손 위치만 따라간다.
        transform.SetParent(null, true);
        _hand = ctx?.SocketTransform;
        _energyCenter = ResolveEnergyCenter();

        CollectTiles(_caster);
        if (_tiles.Count == 0 || orbPrefab == null)
        {
            Destroy(gameObject);
            return;
        }

        Active.Add(this);
        _root = new GameObject("HoopSummonCast_Effects").transform;
        _openTiles = _tiles.Count;
        for (int i = 0; i < _tiles.Count; i++) StartGather(_tiles[i]);

        _core = EnergyOrb(_energyCenter, .025f, false);
        for (int i = 0; i < _tiles.Count; i++) _tiles[i].Split = EnergyOrb(_energyCenter, .025f, true);
        _orbitRadius = characterHeight * .035f;
        _spinAngle = 0f;
        _orbiting = true;
        StartCoroutine(Run());
    }

    /// <summary>충전 종료 Cue: 회전을 멈추고 칸으로 보낸다. 연출 정리(StopAllHandles)로 와도 같은 의미다.</summary>
    public void Stop() => _splitRequested = true;

    public bool Signal(SkillEffectContext ctx)
    {
        _splitRequested = true;
        return true;
    }

    private bool TryClaim(HoopWheelVisual visual, Vector3 position)
    {
        for (int i = 0; i < _tiles.Count; i++)
        {
            Tile tile = _tiles[i];
            if (tile.Closed || tile.Visual != null) continue;
            Vector3 offset = tile.Floor - position;
            offset.y = 0f;
            if (offset.sqrMagnitude > TileMatchDistance * TileMatchDistance) continue;
            tile.Visual = visual;
            return true;
        }
        return false;
    }

    private void CollectTiles(BattleCharactor caster)
    {
        _tiles.Clear();
        BossController boss = caster != null ? caster.GetComponent<BossController>() : null;
        if (boss == null) return;

        var positions = new List<Vector3>();
        boss.CollectEmptySkillSummonPositions(HoopEnemyId, positions);
        for (int i = 0; i < positions.Count; i++) _tiles.Add(new Tile { Floor = positions[i] });
    }

    private void LateUpdate()
    {
        // 애니메이션이 손을 옮긴 뒤 따라간다.
        _energyCenter = ResolveEnergyCenter();
        if (!_orbiting) return;

        _spinAngle += Dt() * spinDegreesPerSecond;
        if (_core != null)
        {
            _core.transform.position = _energyCenter;
            _core.transform.rotation = Quaternion.AngleAxis(_spinAngle, Vector3.forward);
        }

        float step = 360f / Mathf.Max(1, _tiles.Count);
        for (int i = 0; i < _tiles.Count; i++)
        {
            GameObject split = _tiles[i].Split;
            if (split == null) continue;
            float angle = (_spinAngle + i * step) * Mathf.Deg2Rad;
            split.transform.position = _energyCenter
                + new Vector3(Mathf.Cos(angle), .28f * Mathf.Sin(angle * 2f), Mathf.Sin(angle)) * _orbitRadius;
            split.transform.rotation = Quaternion.AngleAxis(_spinAngle, Vector3.up);
        }
    }

    private IEnumerator Run()
    {
        // 1) 손 위 회전: 에너지 3갈래가 반지름을 키우며 돈다.
        float elapsed = 0f;
        while (!_splitRequested && elapsed < maxSpinSeconds)
        {
            elapsed += Dt();
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / spinGrowSeconds));
            if (_core != null) _core.transform.localScale = Vector3.one * Mathf.Lerp(.025f, .21f, p);
            _orbitRadius = characterHeight * Mathf.Lerp(.035f, .32f, p);
            for (int i = 0; i < _tiles.Count; i++) SetScale(_tiles[i].Split, Mathf.Lerp(.025f, .075f, p));
            yield return null;
        }
        _orbiting = false;

        // 2) 분리: 각 칸으로 포물선 이동.
        var starts = new Vector3[_tiles.Count];
        var destinations = new Vector3[_tiles.Count];
        float coreScale = _core != null ? _core.transform.localScale.x : 0f;
        for (int i = 0; i < _tiles.Count; i++)
        {
            starts[i] = _tiles[i].Split != null ? _tiles[i].Split.transform.position : _energyCenter;
            destinations[i] = _tiles[i].Floor + Vector3.up * RingLift;
        }
        for (elapsed = 0f; elapsed < splitTravelSeconds; )
        {
            elapsed += Dt();
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / splitTravelSeconds));
            SetScale(_core, Mathf.Lerp(coreScale, .001f, Mathf.Clamp01(p * 5f)));
            for (int i = 0; i < _tiles.Count; i++)
            {
                GameObject split = _tiles[i].Split;
                if (split == null) continue;
                Vector3 control = Vector3.Lerp(starts[i], destinations[i], .45f) + Vector3.up * (characterHeight * .35f);
                split.transform.position = (1f - p) * (1f - p) * starts[i] + 2f * (1f - p) * p * control + p * p * destinations[i];
                split.transform.localScale = Vector3.one * Mathf.Lerp(.075f, .12f, p);
            }
            yield return null;
        }
        if (_core != null) _core.SetActive(false);

        // 3) 도착한 에너지가 바닥 링으로 펴진다.
        for (int i = 0; i < _tiles.Count; i++) _tiles[i].Ring = GroundRing(destinations[i]);
        for (elapsed = 0f; elapsed < ringMorphSeconds; )
        {
            elapsed += Dt();
            float p = Mathf.Clamp01(elapsed / ringMorphSeconds);
            for (int i = 0; i < _tiles.Count; i++)
            {
                Tile tile = _tiles[i];
                if (tile.Split != null) tile.Split.transform.localScale = new Vector3(.12f, Mathf.Lerp(.12f, .002f, p), .12f) * (1f - p);
                SetScale(tile.Ring, Mathf.Lerp(.02f, 1f, Mathf.SmoothStep(0f, 1f, p)));
            }
            yield return null;
        }

        // 4) 칸마다 굴렁쇠가 생기면 터뜨린다.
        for (int i = 0; i < _tiles.Count; i++)
        {
            if (_tiles[i].Split != null) _tiles[i].Split.SetActive(false);
            StartCoroutine(RunTile(_tiles[i]));
        }
    }

    private IEnumerator RunTile(Tile tile)
    {
        float waited = 0f;
        while (tile.Visual == null && !_summonCompleted && waited < releaseTimeoutSeconds)
        {
            waited += Dt();
            yield return null;
        }

        // OnPostExecution에서 Instantiate한 유닛의 Start가 다음 프레임에 호출되므로 등록 기회를 준다.
        if (_summonCompleted && tile.Visual == null) yield return null;

        if (tile.Visual != null)
        {
            Release(tile);
            yield return Wait(ringHoldSeconds);
        }
        else
        {
            StopGather(tile);
        }
        tile.Closed = true;

        for (float elapsed = 0f; elapsed < ringFadeSeconds; )
        {
            elapsed += Dt();
            SetScale(tile.Ring, Mathf.Lerp(1f, .001f, Mathf.Clamp01(elapsed / ringFadeSeconds)));
            yield return null;
        }
        if (tile.Ring != null) tile.Ring.SetActive(false);

        if (--_openTiles <= 0) Destroy(gameObject);
    }

    private void Release(Tile tile)
    {
        tile.Released = true;
        for (int i = 0; i < tile.Release.Count; i++)
        {
            if (tile.Release[i] != null) tile.Release[i].Play(false);
        }
        if (tile.Gather != null) Destroy(tile.Gather, tile.GatherTail + .25f);
        tile.Gather = null;
        tile.Visual.RevealFromSummon();
    }

    private void StopGather(Tile tile)
    {
        if (tile.Gather == null) return;
        foreach (ParticleSystem ps in tile.Gather.GetComponentsInChildren<ParticleSystem>(true))
            ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        Destroy(tile.Gather, .6f);
        tile.Gather = null;
    }

    private void OnDestroy()
    {
        Active.Remove(this);
        // 연출이 중간에 끊겨도 굴렁쇠가 숨은 채 남지 않게 한다.
        for (int i = 0; i < _tiles.Count; i++)
        {
            Tile tile = _tiles[i];
            if (tile.Visual != null && !tile.Released) tile.Visual.RevealFromSummon();
            if (tile.Gather != null) Destroy(tile.Gather);
        }
        if (_root != null) Destroy(_root.gameObject);
    }

    private void StartGather(Tile tile)
    {
        if (gatherPrefab == null) return;

        // 원본의 집결·방출 중심(로컬 1.77)을 굴렁쇠 구체 중심에 맞추고, 바닥 발광은 칸 바닥에 둔다.
        float scale = characterHeight * gatherScaleByHeight;
        float orbHeight = preset != null ? HoopWheelVisual.ResolveWheelHeight(preset) : characterHeight * .55f;
        GameObject go = Spawn(gatherPrefab, tile.Floor + Vector3.up * (orbHeight - NativeGatherCenterHeight * scale),
            scale, Quaternion.identity, null);
        go.name = "HoopSummon_TileEnergyGather";
        foreach (Transform child in go.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == "target_glow" || child.name == "target_shadow")
                child.position = tile.Floor + Vector3.up * GatherFloorLift;
        }

        // 원본 방출 시각을 예상 방출 시점에 맞춘다. 방출 입자는 굴렁쇠가 나타날 때 직접 재생한다.
        float speed = gatherNativeBurstSeconds / expectedReleaseSeconds * BattleSpeed();
        Transform releaseRoot = go.transform.Find("explosion");
        foreach (ParticleSystem ps in go.GetComponentsInChildren<ParticleSystem>(true))
        {
            ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
            main.simulationSpeed = speed;
            main.stopAction = ParticleSystemStopAction.None;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            bool release = (releaseRoot != null && (ps.transform == releaseRoot || ps.transform.IsChildOf(releaseRoot)))
                || main.startDelay.constantMin >= gatherNativeBurstSeconds - .1f;
            if (release)
            {
                main.startDelay = 0f;
                tile.GatherTail = Mathf.Max(tile.GatherTail, (main.duration + main.startLifetime.constantMax) / speed);
                if (ps.gameObject.activeInHierarchy) tile.Release.Add(ps);
            }
            else if (ps.gameObject.activeInHierarchy)
            {
                ps.Play(false);
            }
        }
        tile.Gather = go;
    }

    // 원본 링 프리팹의 circles 입자 하나를 바닥에 눕혀 정지시킨 링(+안쪽 링). 크기는 루트 배율로 펴고 접는다.
    private GameObject GroundRing(Vector3 position)
    {
        if (ringPrefab == null) return null;
        GameObject ring = Spawn(ringPrefab, position, 1f, Quaternion.identity, _root);
        ring.name = "HoopSummon_GroundRing";
        ParticleSystem circle = null;
        foreach (ParticleSystem ps in ring.GetComponentsInChildren<ParticleSystem>(true))
        {
            ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (ps.name != "circles")
            {
                if (ps.gameObject != ring) ps.gameObject.SetActive(false);
                continue;
            }
            circle = ps;
            ps.transform.localPosition = Vector3.zero;
            ps.transform.localRotation = Quaternion.identity;
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.startSpeed = 0f;
            main.startLifetime = 10000f;
            main.startSize = characterHeight * ringScaleByHeight;
            main.startRotation3D = true;
            main.startRotationX = Mathf.PI * .5f;
            main.startRotationY = 0f;
            main.startRotationZ = 0f;
            main.startColor = new Color(.72f, .22f, 1f, 1f);
            var shape = ps.shape; shape.enabled = false;
            var size = ps.sizeOverLifetime; size.enabled = false;
            var color = ps.colorOverLifetime; color.enabled = false;
            var emission = ps.emission; emission.enabled = false;
            ps.GetComponent<ParticleSystemRenderer>().alignment = ParticleSystemRenderSpace.Local;
            ps.Emit(1);
            ps.Pause(false);
        }
        // 안쪽 링을 하나 더 그려 전투 카메라에서 도착 칸이 읽히게 한다.
        if (circle != null)
        {
            GameObject inner = Instantiate(circle.gameObject, ring.transform);
            inner.name = "Inner_Summon_Circle";
            inner.transform.localScale = Vector3.one * .72f;
            ParticleSystem ps = inner.GetComponent<ParticleSystem>();
            ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Emit(1);
            ps.Pause(false);
        }
        ring.transform.localScale = Vector3.one * .02f;
        return ring;
    }

    private GameObject EnergyOrb(Vector3 position, float scale, bool trail)
    {
        GameObject orb = Spawn(orbPrefab, position, scale, Quaternion.identity, _root);
        foreach (VisualEffect vfx in orb.GetComponentsInChildren<VisualEffect>(true))
        {
            if (vfx.HasVector4("Color")) vfx.SetVector4("Color", new Vector4(2.2f, .35f, 5f, 1f));
            if (vfx.HasVector4("Color02")) vfx.SetVector4("Color02", new Vector4(3.4f, .35f, 6f, 1f));
        }
        if (trail && splitTrailMaterial != null)
        {
            var ribbon = orb.AddComponent<TrailRenderer>();
            ribbon.sharedMaterial = splitTrailMaterial;
            ribbon.time = .16f / BattleSpeed();
            ribbon.minVertexDistance = .02f;
            ribbon.startWidth = characterHeight * .045f;
            ribbon.endWidth = 0f;
            ribbon.startColor = new Color(.75f, .2f, 1f, .8f);
            ribbon.endColor = new Color(.35f, .05f, 1f, 0f);
            ribbon.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ribbon.receiveShadows = false;
        }
        return orb;
    }

    private static GameObject Spawn(GameObject prefab, Vector3 position, float scale, Quaternion rotation, Transform parent)
    {
        GameObject go = Instantiate(prefab, position, rotation, parent);
        go.transform.localScale = Vector3.one * scale;
        foreach (MonoBehaviour script in go.GetComponentsInChildren<MonoBehaviour>(true)) script.enabled = false;
        float speed = BattleSpeed();
        foreach (ParticleSystem ps in go.GetComponentsInChildren<ParticleSystem>(true))
        {
            ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.simulationSpeed = speed;
            if (ps.gameObject.activeInHierarchy) ps.Play(false);
        }
        foreach (VisualEffect vfx in go.GetComponentsInChildren<VisualEffect>(true))
        {
            vfx.playRate = speed;
            vfx.Reinit();
            vfx.Play();
        }
        return go;
    }

    private Vector3 ResolveEnergyCenter()
    {
        return _hand != null ? _hand.position + Vector3.up * (characterHeight * .18f) : _energyCenter;
    }

    private static void SetScale(GameObject go, float scale)
    {
        if (go != null) go.transform.localScale = Vector3.one * scale;
    }

    private static IEnumerator Wait(float seconds)
    {
        for (float t = 0f; t < seconds; t += Dt()) yield return null;
    }

    private static float Dt() => Time.deltaTime * BattleSpeed();

    private static float BattleSpeed()
    {
        return BattleManager.Instance != null ? Mathf.Max(.01f, BattleManager.Instance.CurrentBattleSpeed) : 1f;
    }
}

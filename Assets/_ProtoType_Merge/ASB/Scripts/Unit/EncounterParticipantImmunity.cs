using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전투 참여자(EncounterParticipant — 율리아전에서는 증폭기)가 하나라도 살아 있는 동안 이 유닛은 피해를 받지 않는다.
/// <para>
/// [2026-10-23 사용자 결정] 율리아: 증폭기가 하나라도 살아 있으면 피해 완전 무효(공격 대상으로는 계속 고를 수 있음).
/// 전부 쓰러지면 피해를 받고, 하나라도 부활하면 다시 무효가 된다. 정지장 막(YuliaStasisVfx)도 같은 조건으로 보인다.
/// </para>
/// 코어에는 범용 걸쇠(BattleCharactor.AddDamageImmunity)만 있고, 참여자 생존 판단은 이 컴포넌트가 한다.
/// 의존은 단방향(이 컴포넌트 → 코어). 참여자가 없는 씬(스킬 프리뷰 등)에서는 무효가 걸리지 않는다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BattleCharactor))]
public sealed class EncounterParticipantImmunity : MonoBehaviour
{
    private readonly List<BattleCharactor> _participants = new List<BattleCharactor>();
    private BattleCharactor _self;
    private IDisposable _immunity;
    private bool _bound;

    public bool IsImmune => _immunity != null;

    private void Awake()
    {
        _self = GetComponent<BattleCharactor>();
    }

    private void Start()
    {
        // 스폰·초기화(EnemyScript.Initialize)가 끝난 뒤 묶는다. 증폭기는 율리아와 같은 프레임에 스폰된다.
        BindParticipants();
        _bound = true;
        Refresh();
    }

    private void OnEnable()
    {
        if (_bound) Refresh();
    }

    // BattleCharactor.OnDisable도 걸쇠를 모두 지운다. 다시 켜지면 OnEnable에서 맞춘다.
    private void OnDisable()
    {
        Release();
    }

    private void OnDestroy()
    {
        UnbindParticipants();
        Release();
    }

    // 참여자 사망(HP 0)·부활(HP 복귀)은 모두 OnHpChanged로 들어온다. 사망 직전 OnHpChanged는 IsDead 전일 수 있어 OnDied도 본다.
    private void BindParticipants()
    {
        foreach (EncounterParticipant participant in FindObjectsByType<EncounterParticipant>(FindObjectsSortMode.None))
        {
            BattleCharactor unit = participant.GetComponent<BattleCharactor>();
            if (unit == null || unit == _self || _participants.Contains(unit)) continue;
            unit.OnHpChanged += HandleParticipantHpChanged;
            unit.OnDied += HandleParticipantDied;
            _participants.Add(unit);
        }
    }

    private void UnbindParticipants()
    {
        for (int i = 0; i < _participants.Count; i++)
        {
            if (_participants[i] == null) continue;
            _participants[i].OnHpChanged -= HandleParticipantHpChanged;
            _participants[i].OnDied -= HandleParticipantDied;
        }
        _participants.Clear();
    }

    private void HandleParticipantHpChanged(float currentHp, float maxHp) => Refresh();

    private void HandleParticipantDied(BattleCharactor _) => Refresh();

    private int CountAliveParticipants()
    {
        int alive = 0;
        for (int i = 0; i < _participants.Count; i++)
            if (_participants[i] != null && !_participants[i].IsDead) alive++;
        return alive;
    }

    private void Refresh()
    {
        if (!isActiveAndEnabled || _self == null) return;

        if (ShouldBeImmune(CountAliveParticipants()))
        {
            if (_immunity == null) _immunity = _self.AddDamageImmunity();
        }
        else
        {
            Release();
        }
    }

    private void Release()
    {
        _immunity?.Dispose();
        _immunity = null;
    }

    /// <summary>피해 무효 여부. 참여자가 하나라도 살아 있을 때만(전부 쓰러져 부활 대기 중이면 피해를 받는다).</summary>
    public static bool ShouldBeImmune(int aliveParticipants)
    {
        return aliveParticipants > 0;
    }
}

using System;
using UnityEngine;

namespace JC.BattleTestbed
{
    /// <summary>테스트씬에 생성된 유닛에만 붙이는 불사/회복 규칙입니다.</summary>
    [DisallowMultipleComponent]
    public sealed class JcBattleTestbedVitals : MonoBehaviour
    {
        private BattleCharactor unit;
        private IDisposable minimumHp;
        private float recoveryDelay;
        private float recoverAt = -1f;
        private bool manualDeath;

        public void Initialize(BattleCharactor target, float delay)
        {
            Release();
            unit = target;
            recoveryDelay = Mathf.Max(0f, delay);
            minimumHp = unit.AddMinimumHpConstraint(1f, this);
            unit.OnHpChanged += OnHpChanged;
            RefillInfluence();
        }

        private void OnHpChanged(float hp, float maximum)
        {
            if (!manualDeath && hp < maximum && recoverAt < 0f)
                recoverAt = Time.unscaledTime + recoveryDelay;
        }

        private void LateUpdate()
        {
            if (unit == null) return;
            // G로 만든 시체는 정상 부활 스킬이 살린 뒤에만 불사를 다시 적용합니다.
            if (manualDeath)
            {
                if (unit.IsDead) return;
                manualDeath = false;
                minimumHp = unit.AddMinimumHpConstraint(1f, this);
                OnHpChanged(unit.CurrentHp, unit.MaxHp);
            }
            if (!unit.IsDead && recoverAt >= 0f && Time.unscaledTime >= recoverAt)
            {
                recoverAt = -1f;
                unit.SetHp(unit.MaxHp);
            }
            RefillInfluence();
        }

        private void RefillInfluence()
        {
            if (unit != null && unit.IsPlayer && !unit.IsDead && unit.CurrentInfluence < unit.MaxInfluence)
                unit.SetInfluence(unit.MaxInfluence);
        }

        public void KillForRevivalTest()
        {
            if (unit == null || unit.IsDead || manualDeath) return;
            manualDeath = true;
            recoverAt = -1f;
            minimumHp?.Dispose();
            minimumHp = null;
            unit.TakeDamage(unit.MaxHp + unit.CurrentHp + 1f);
        }

        private void Release()
        {
            if (unit != null) unit.OnHpChanged -= OnHpChanged;
            minimumHp?.Dispose();
            minimumHp = null;
            recoverAt = -1f;
        }

        private void OnDestroy() => Release();
    }
}

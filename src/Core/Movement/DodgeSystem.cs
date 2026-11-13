using System;

namespace Calabash.Core.Movement
{
    public enum DodgeResult
    {
        /// <summary>Уклонение не удалось (откат/нет зарядов).</summary>
        Failed = 0,
        /// <summary>Обычное уклонение.</summary>
        Normal = 1,
        /// <summary>Идеальное уклонение: в последний момент, мир теряет серый.</summary>
        Perfect = 2,
    }

    public sealed class DodgeConfig
    {
        /// <summary>Длительность рывка в секундах.</summary>
        public float Duration = 0.18f;

        /// <summary>Откат между уклонениями.</summary>
        public float Cooldown = 0.75f;

        /// <summary>Окно идеального уклонения до удара, в секундах.</summary>
        public float PerfectWindow = 0.12f;

        /// <summary>Замедление времени при идеальном уклонении.</summary>
        public float PerfectTimeScale = 0.25f;

        /// <summary>Сколько секунд длится замедление.</summary>
        public float PerfectDuration = 0.35f;

        /// <summary>Сколько зарядов уклонения доступно подряд.</summary>
        public int Charges = 1;
    }

    /// <summary>
    /// Уклонение в стиле Ghostrunner. Ключевое правило дизайна:
    /// это ЕДИНСТВЕННЫЙ способ не получить урон от воды.
    /// </summary>
    public sealed class DodgeSystem
    {
        readonly DodgeConfig _config;
        float _cooldownLeft;
        float _dodgeLeft;
        float _perfectLeft;

        public DodgeSystem(DodgeConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            Charges = config.Charges;
            _cooldownLeft = 0f;
        }

        public DodgeConfig Config => _config;

        /// <summary>Доступные заряды.</summary>
        public int Charges { get; private set; }

        /// <summary>Идёт ли сейчас рывок.</summary>
        public bool IsDodging => _dodgeLeft > 0f;

        /// <summary>Работает ли замедление времени от идеального уклонения.</summary>
        public bool IsPerfectActive => _perfectLeft > 0f;

        /// <summary>Множитель времени для эффектов (1 = норма).</summary>
        public float TimeScale => IsPerfectActive ? _config.PerfectTimeScale : 1f;

        public float CooldownLeft => _cooldownLeft;

        public void Tick(float deltaSeconds, float cooldownMultiplier = 1f)
        {
            if (deltaSeconds <= 0f)
                return;

            if (_dodgeLeft > 0f)
            {
                _dodgeLeft -= deltaSeconds;
                if (_dodgeLeft < 0f) _dodgeLeft = 0f;
            }

            if (_perfectLeft > 0f)
            {
                _perfectLeft -= deltaSeconds;
                if (_perfectLeft < 0f) _perfectLeft = 0f;
            }

            if (_cooldownLeft > 0f)
            {
                // cooldownMultiplier — множитель СКОРОСТИ восстановления:
                // 2.0 = откат идёт вдвое быстрее, 0.5 = вдвое медленнее.
                _cooldownLeft -= deltaSeconds * Math.Max(0.01f, cooldownMultiplier);
                if (_cooldownLeft <= 0f)
                {
                    _cooldownLeft = 0f;
                    if (Charges < _config.Charges)
                        Charges = _config.Charges;
                }
            }
        }

        /// <summary>
        /// Попытка уклонения.
        /// </summary>
        /// <param name="secondsUntilHit">
        /// Сколько секунд осталось до попадания. Если &lt;= 0 — удара нет,
        /// это обычный рывок. Если в пределах PerfectWindow — идеальное.
        /// </param>
        public DodgeResult TryDodge(float secondsUntilHit)
        {
            if (Charges <= 0 || _cooldownLeft > 0f)
                return DodgeResult.Failed;

            Charges--;
            _dodgeLeft = _config.Duration;

            if (Charges <= 0)
                _cooldownLeft = _config.Cooldown;

            bool perfect = secondsUntilHit > 0f && secondsUntilHit <= _config.PerfectWindow;
            if (perfect)
            {
                _perfectLeft = _config.PerfectDuration;
                return DodgeResult.Perfect;
            }

            return DodgeResult.Normal;
        }

        /// <summary>Проверка: уклонение сейчас невозможно.</summary>
        public bool IsOnCooldown => Charges <= 0;

        /// <summary>Полный сброс (новая попытка после Лимбо).</summary>
        public void Reset()
        {
            Charges = _config.Charges;
            _cooldownLeft = 0f;
            _dodgeLeft = 0f;
            _perfectLeft = 0f;
        }
    }
}

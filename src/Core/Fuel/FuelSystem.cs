using System;

namespace Calabash.Core.Fuel
{
    /// <summary>Настройки топлива. Все числа в игровых единицах, не в секундах кадра.</summary>
    public sealed class FuelConfig
    {
        /// <summary>Максимальный запас топлива.</summary>
        public float MaxFuel = 100f;

        /// <summary>Сколько топлива сгорает за секунду в обычном состоянии.</summary>
        public float BurnPerSecond = 2.2f;

        /// <summary>Сколько топлива даёт одно убийство.</summary>
        public float KillRefuel = 17f;

        /// <summary>Множитель горения из прокачки «Быстрое горение».</summary>
        public float BurnRateMultiplier = 1f;

        /// <summary>Порог «голоден» (доля 0..1).</summary>
        public float HungryAt = 0.40f;

        /// <summary>Порог «умирает» (доля 0..1).</summary>
        public float StarvingAt = 0.15f;

        /// <summary>Максимальный запас топлива не может быть превышен убийством.</summary>
        public bool ClampToMax = true;
    }

    public enum FuelState
    {
        /// <summary>Горит ровно, угрозы нет.</summary>
        Normal,
        /// <summary>Топлива мало, трещины гаснут.</summary>
        Hungry,
        /// <summary>Почти потух, мир теряет краски.</summary>
        Starving,
        /// <summary>Потух. Смерть.</summary>
        Extinguished,
    }

    /// <summary>
    /// Топливо = здоровье. Постоянно убывает, пополняется убийствами.
    /// Пока герой ничего не убивает, он сгорает — это и есть давление игры.
    /// </summary>
    public sealed class FuelSystem
    {
        readonly FuelConfig _config;

        public FuelSystem(FuelConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            Current = config.MaxFuel;
            State = FuelState.Normal;
        }

        public FuelConfig Config => _config;

        /// <summary>Текущий запас топлива.</summary>
        public float Current { get; private set; }

        public float Max => _config.MaxFuel;

        /// <summary>Доля запаса 0..1.</summary>
        public float Fraction => _config.MaxFuel <= 0f ? 0f : Current / _config.MaxFuel;

        public FuelState State { get; private set; }

        public bool IsDead => State == FuelState.Extinguished;

        /// <summary>
        /// Тик времени. Возвращает true, если на этом тике герой потух.
        /// </summary>
        public bool Tick(float deltaSeconds)
        {
            if (deltaSeconds <= 0f || IsDead)
                return false;

            float burn = _config.BurnPerSecond * _config.BurnRateMultiplier * deltaSeconds;
            Current -= burn;

            if (Current <= 0f)
            {
                Current = 0f;
                State = FuelState.Extinguished;
                return true;
            }

            UpdateState();
            return false;
        }

        /// <summary>Пополнение за убийство. Возвращает реально добавленное количество.</summary>
        public float RefuelFromKill()
        {
            if (IsDead)
                return 0f;

            float before = Current;
            Current += _config.KillRefuel;

            if (_config.ClampToMax && Current > _config.MaxFuel)
                Current = _config.MaxFuel;

            UpdateState();
            return Current - before;
        }

        /// <summary>Прямое изменение топлива (урон от воды, способности и т.п.).</summary>
        public float Modify(float delta)
        {
            if (IsDead || delta == 0f)
                return 0f;

            float before = Current;
            Current += delta;

            if (Current < 0f) Current = 0f;
            if (_config.ClampToMax && Current > _config.MaxFuel) Current = _config.MaxFuel;

            if (Current <= 0f)
                State = FuelState.Extinguished;
            else
                UpdateState();

            return Current - before;
        }

        /// <summary>Восстановление для второй попытки (возврат из Лимбо).</summary>
        public void Revive(float fraction = 1f)
        {
            if (fraction < 0f) fraction = 0f;
            if (fraction > 1f) fraction = 1f;

            Current = _config.MaxFuel * fraction;
            State = Current <= 0f ? FuelState.Extinguished : FuelState.Normal;
            UpdateState();
        }

        void UpdateState()
        {
            if (Current <= 0f)
            {
                State = FuelState.Extinguished;
                return;
            }

            float f = Fraction;
            if (f <= _config.StarvingAt) State = FuelState.Starving;
            else if (f <= _config.HungryAt) State = FuelState.Hungry;
            else State = FuelState.Normal;
        }
    }
}

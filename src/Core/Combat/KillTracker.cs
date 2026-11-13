using System;

namespace Calabash.Core.Combat
{
    /// <summary>Настройки квоты убийств на уровне.</summary>
    public sealed class QuotaConfig
    {
        /// <summary>Сколько жертв на уровне всего.</summary>
        public int Population = 48;

        /// <summary>Доля, которую нужно убить, чтобы пройти уровень (0..1).</summary>
        public float RequiredFraction = 0.85f;

        /// <summary>Сколько топлива даёт убийство (дублирует Fuel, для отчётов).</summary>
        public bool CountOnlyUniqueVictims = true;
    }

    /// <summary>
    /// Считает убийства и определяет, выполнена ли квота уровня.
    /// Квота — это и есть «механика голода»: убил достаточно — прошёл.
    /// </summary>
    public sealed class KillTracker
    {
        readonly QuotaConfig _config;

        public KillTracker(QuotaConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            if (config.Population <= 0)
                throw new ArgumentException("На уровне должна быть хотя бы одна жертва.", nameof(config));
            if (config.RequiredFraction < 0f || config.RequiredFraction > 1f)
                throw new ArgumentException("Порог должен быть в диапазоне 0..1.", nameof(config));
        }

        public QuotaConfig Config => _config;

        public int Kills { get; private set; }

        public int Population => _config.Population;

        /// <summary>Сколько ещё нужно убить до прохождения.</summary>
        public int RemainingToQuota
        {
            get
            {
                int need = RequiredKills - Kills;
                return need < 0 ? 0 : need;
            }
        }

        /// <summary>Абсолютное число убийств, необходимое для прохождения.</summary>
        public int RequiredKills => (int)Math.Ceiling(_config.Population * _config.RequiredFraction);

        /// <summary>Доля убитых от населения 0..1.</summary>
        public float KillFraction
            => _config.Population <= 0 ? 0f : Math.Min(1f, (float)Kills / _config.Population);

        /// <summary>Квота выполнена?</summary>
        public bool QuotaMet => Kills >= RequiredKills;

        /// <summary>Убил ли он хоть кого-то. Нужно для развилки Рай/Лимбо.</summary>
        public bool HasKilledAnyone => Kills > 0;

        /// <summary>Всех ли убил (для «полного прохождения»).</summary>
        public bool AllKilled => Kills >= _config.Population;

        /// <summary>Регистрирует убийство. Возвращает false, если убивать уже некого.</summary>
        public bool RegisterKill()
        {
            if (Kills >= _config.Population)
                return false;

            Kills++;
            return true;
        }

        /// <summary>Откат (для тестов и отладки).</summary>
        public bool UnregisterKill()
        {
            if (Kills <= 0)
                return false;
            Kills--;
            return true;
        }
    }
}

using System;

namespace Calabash.Core.Decay
{
    /// <summary>
    /// Пять стадий тления. Привязаны к доле убитых жертв, а не ко времени:
    /// тело — это счётчик.
    /// </summary>
    public enum DecayStage
    {
        /// <summary>0 — «Пепел». Обычный мужик, подпалины на ладонях.</summary>
        Ash = 0,
        /// <summary>1 — «Тлеющий». Трещины на предплечьях, дым изо рта.</summary>
        Smouldering = 1,
        /// <summary>2 — «Головня». Кисти обуглены, пальцы не чувствуют.</summary>
        Ember = 2,
        /// <summary>3 — «Печь». Рёбра светятся, кровь испаряется.</summary>
        Furnace = 3,
        /// <summary>4 — «Костёр». Лицо сгорело, в глазницах огонь.</summary>
        Bonfire = 4,
    }

    /// <summary>Пороги стадий в долях убитых жертв (0..1).</summary>
    public sealed class DecayConfig
    {
        public float AshUntil = 0.10f;
        public float SmoulderingUntil = 0.30f;
        public float EmberUntil = 0.55f;
        public float FurnaceUntil = 0.80f;
        // выше FurnaceUntil — Bonfire

        /// <summary>Проверка, что пороги монотонно растут.</summary>
        public bool IsValid =>
            AshUntil > 0f &&
            AshUntil < SmoulderingUntil &&
            SmoulderingUntil < EmberUntil &&
            EmberUntil < FurnaceUntil &&
            FurnaceUntil <= 1f;
    }

    /// <summary>
    /// Переводит долю убийств в стадию тления и в визуальные параметры.
    /// Никакой графики — только числа, которые читает рендер.
    /// </summary>
    public sealed class DecaySystem
    {
        readonly DecayConfig _config;

        public DecaySystem(DecayConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            if (!config.IsValid)
                throw new ArgumentException("Пороги стадий тления должны монотонно расти.", nameof(config));
        }

        public DecayStage StageFor(float killFraction)
        {
            if (killFraction < 0f) killFraction = 0f;
            if (killFraction > 1f) killFraction = 1f;

            if (killFraction < _config.AshUntil) return DecayStage.Ash;
            if (killFraction < _config.SmoulderingUntil) return DecayStage.Smouldering;
            if (killFraction < _config.EmberUntil) return DecayStage.Ember;
            if (killFraction < _config.FurnaceUntil) return DecayStage.Furnace;
            return DecayStage.Bonfire;
        }

        /// <summary>
        /// Насколько стадия «созрела» внутри себя, 0..1. Нужно шейдеру для
        /// плавного перехода трещин, а не мгновенного щелчка.
        /// </summary>
        public float StageProgress(float killFraction)
        {
            if (killFraction < 0f) killFraction = 0f;
            if (killFraction > 1f) killFraction = 1f;

            var stage = StageFor(killFraction);
            float lo = LowerBound(stage);
            float hi = UpperBound(stage);
            if (hi - lo <= 0f) return 1f;
            return (killFraction - lo) / (hi - lo);
        }

        float LowerBound(DecayStage s)
        {
            switch (s)
            {
                case DecayStage.Ash: return 0f;
                case DecayStage.Smouldering: return _config.AshUntil;
                case DecayStage.Ember: return _config.SmoulderingUntil;
                case DecayStage.Furnace: return _config.EmberUntil;
                default: return _config.FurnaceUntil;
            }
        }

        float UpperBound(DecayStage s)
        {
            switch (s)
            {
                case DecayStage.Ash: return _config.AshUntil;
                case DecayStage.Smouldering: return _config.SmoulderingUntil;
                case DecayStage.Ember: return _config.EmberUntil;
                case DecayStage.Furnace: return _config.FurnaceUntil;
                default: return 1f;
            }
        }

        /// <summary>Яркость свечения трещин 0..1 — для шейдера рук.</summary>
        public float GlowIntensity(float killFraction)
            => 0.15f + 0.85f * Math.Min(1f, StageProgress(killFraction) * 0.25f + (int)StageFor(killFraction) * 0.25f);

        /// <summary>Человечность 1..0. На «Костре» от человека остаются только руки.</summary>
        public float Humanity(float killFraction)
            => 1f - (int)StageFor(killFraction) / 4f;
    }
}

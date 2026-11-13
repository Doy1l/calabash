using System;
using Calabash.Core.Combat;
using Calabash.Core.Fuel;

namespace Calabash.Core.Endings
{
    /// <summary>Три концовки и промежуточное состояние.</summary>
    public enum EndingKind
    {
        /// <summary>Ещё играем.</summary>
        None = 0,
        /// <summary>Рай без убийств: уснул и не тронул никого.</summary>
        HeavenPure = 1,
        /// <summary>Рай через искупление: отказался продолжать в Лимбо.</summary>
        HeavenRedeemed = 2,
        /// <summary>Ад: прошёл игру, убив достаточно.</summary>
        HellConqueror = 3,
        /// <summary>Ад: умер второй раз после возврата из Лимбо. Цепи, вечно.</summary>
        HellChained = 4,
    }

    /// <summary>Состояние забега.</summary>
    public enum RunPhase
    {
        /// <summary>Идёт бой.</summary>
        Playing = 0,
        /// <summary>Сгорел впервые после убийств — весы, вопрос «продолжить?».</summary>
        Limbo = 1,
        /// <summary>Забег закончен концовкой.</summary>
        Finished = 2,
    }

    /// <summary>Ответ на вопрос весов в Лимбо.</summary>
    public enum LimboChoice
    {
        /// <summary>Ещё не ответил.</summary>
        Pending = 0,
        /// <summary>Да — вернуться в бойню с сохранённой прокачкой.</summary>
        Continue = 1,
        /// <summary>Нет — принять искупление.</summary>
        Refuse = 2,
    }

    public sealed class RunDirectorConfig
    {
        /// <summary>Сколько топлива восстанавливается при возврате из Лимбо (доля).</summary>
        public float LimboReviveFraction = 1f;

        /// <summary>Даёт ли Лимбо вторую попытку. По дизайну — ровно одну.</summary>
        public bool AllowSecondChance = true;
    }

    /// <summary>
    /// Ведёт забег: следит за смертью, развилкой Лимбо и выводит концовку.
    ///
    /// Правила (по дизайну):
    ///  - умер, никого не убив            → Рай (HeavenPure)
    ///  - умер после убийств, 1-й раз     → Лимбо, вопрос «продолжить?»
    ///      - ответ «нет»                 → Рай через искупление
    ///      - ответ «да»                  → возврат в бой, прокачка сохранена
    ///  - умер второй раз после возврата  → Ад, цепи (HellChained)
    ///  - выполнил квоту убийств          → Ад, покоритель (HellConqueror)
    /// </summary>
    public sealed class RunDirector
    {
        readonly RunDirectorConfig _config;
        readonly FuelSystem _fuel;
        readonly KillTracker _kills;

        public RunDirector(RunDirectorConfig config, FuelSystem fuel, KillTracker kills)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _fuel = fuel ?? throw new ArgumentNullException(nameof(fuel));
            _kills = kills ?? throw new ArgumentNullException(nameof(kills));
            Phase = RunPhase.Playing;
            Ending = EndingKind.None;
            LimboAnswer = LimboChoice.Pending;
        }

        public RunPhase Phase { get; private set; }
        public EndingKind Ending { get; private set; }
        public LimboChoice LimboAnswer { get; private set; }

        /// <summary>Сколько раз возвращался из Лимбо. Больше одной попытки нет.</summary>
        public int LimboReturns { get; private set; }

        public bool IsPlaying => Phase == RunPhase.Playing;
        public bool InLimbo => Phase == RunPhase.Limbo;
        public bool IsFinished => Phase == RunPhase.Finished;

        /// <summary>
        /// Тик: гоняет топливо и разрешает смерть. Возвращает true,
        /// если на этом тике состояние изменилось (смерть/Лимбо/концовка).
        /// </summary>
        public bool Tick(float deltaSeconds)
        {
            if (Phase != RunPhase.Playing)
                return false;

            _fuel.Tick(deltaSeconds);

            // Топливо могло обнулиться как от горения, так и от внешнего урона
            // (вода, способности). Проверяем состояние, а не возврат Tick —
            // иначе смерть от внешней причины останется незамеченной.
            if (_fuel.IsDead)
            {
                ResolveDeath();
                return true;
            }

            // Квота выполнена — забег сразу закончен победой в Ад
            if (_kills.QuotaMet)
            {
                Finish(EndingKind.HellConqueror);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Убийство: пополняет топливо, считает квоту и проверяет её выполнение.
        /// Возвращает true, если убийство засчитано.
        /// </summary>
        public bool RegisterKill()
        {
            if (Phase != RunPhase.Playing)
                return false;

            if (!_kills.RegisterKill())
                return false;

            _fuel.RefuelFromKill();

            if (_kills.QuotaMet)
                Finish(EndingKind.HellConqueror);

            return true;
        }

        /// <summary>Смерть по внешней причине (не от топлива) — например, добили враги.</summary>
        public void DieFromExternalCause()
        {
            if (Phase != RunPhase.Playing)
                return;
            _fuel.Modify(-_fuel.Current);
            ResolveDeath();
        }

        /// <summary>Ответ на вопрос весов. Работает только в фазе Лимбо.</summary>
        public bool AnswerLimbo(LimboChoice choice)
        {
            if (Phase != RunPhase.Limbo)
                return false;
            if (choice == LimboChoice.Pending)
                return false;

            LimboAnswer = choice;

            if (choice == LimboChoice.Refuse)
            {
                Finish(EndingKind.HeavenRedeemed);
                return true;
            }

            // Продолжить: вторая попытка. Больше одной не даём.
            if (!_config.AllowSecondChance || LimboReturns >= 1)
            {
                Finish(EndingKind.HellChained);
                return true;
            }

            LimboReturns++;
            _fuel.Revive(_config.LimboReviveFraction);
            Phase = RunPhase.Playing;
            return true;
        }

        void ResolveDeath()
        {
            // Чистая смерть: никого не тронул — Рай без убийств
            if (!_kills.HasKilledAnyone)
            {
                Finish(EndingKind.HeavenPure);
                return;
            }

            // Уже возвращался один раз — вторая смерть = цепи
            if (LimboReturns >= 1)
            {
                Finish(EndingKind.HellChained);
                return;
            }

            // Первая смерть после убийств — весы
            Phase = RunPhase.Limbo;
            LimboAnswer = LimboChoice.Pending;
        }

        /// <summary>Принудительно завершить забег (для отладки и тестов).</summary>
        public void Finish(EndingKind kind)
        {
            if (kind == EndingKind.None)
                throw new ArgumentException("Нельзя завершить забег без концовки.", nameof(kind));

            Ending = kind;
            Phase = RunPhase.Finished;
        }
    }
}

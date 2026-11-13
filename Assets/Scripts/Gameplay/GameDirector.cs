// Этот файл лежит в Assets/ и компилируется ТОЛЬКО в Unity.
// Он намеренно тонкий: вся логика в Calabash.Core, здесь только склейка
// с движком. Если тут появится игровая математика — это ошибка.

#if UNITY_5_3_OR_NEWER
using UnityEngine;
using Calabash.Core.Fuel;
using Calabash.Core.Decay;
using Calabash.Core.Endings;
using Calabash.Unity;

namespace Calabash.Gameplay
{
    /// <summary>
    /// Единственный MonoBehaviour, который нужен для запуска механик.
    /// Вешается на пустой объект в сцене; все системы живут внутри фасада.
    ///
    /// Настройки подобраны по дизайн-документу — менять их в инспекторе,
    /// а не в коде.
    /// </summary>
    [AddComponentMenu("Calabash/Game Director")]
    public sealed class GameDirector : MonoBehaviour
    {
        [Header("Забег")]
        [Tooltip("Сколько жертв на уровне всего")]
        [SerializeField] int population = 48;

        [Tooltip("Доля убитых, нужная для прохождения (0..1)")]
        [Range(0f, 1f)]
        [SerializeField] float requiredFraction = 0.85f;

        [Tooltip("Зерно генератора карточек. 0 — случайное")]
        [SerializeField] int seed = 0;

        [Header("Отладка")]
        [Tooltip("Останавливать время на время выбора карточек")]
        [SerializeField] bool slowTimeOnCardPick = true;

        [Tooltip("Сколько секунд длится замедление при выборе")]
        [SerializeField] float cardSlowTimeScale = 0.2f;

        CalabashGame _game;

        public CalabashGame Game => _game;

        // --- События для остальных систем (VFX, UI, звук) ---

        public event System.Action<int, int> KillCountChanged;    // (убито, нужно)
        public event System.Action<FuelState> FuelStateChanged;
        public event System.Action<DecayStage> DecayStageChanged;
        public event System.Action<RunPhase> PhaseChanged;
        public event System.Action<EndingKind> RunFinished;
        public event System.Action LimboEntered;

        void Awake()
        {
            _game = new CalabashGame();

            _game.KillRegistered += _ =>
                KillCountChanged?.Invoke(_game.Kills, _game.RequiredKills);

            _game.FuelStateChanged += s => FuelStateChanged?.Invoke(s);
            _game.DecayStageChanged += s => DecayStageChanged?.Invoke(s);
            _game.PhaseChanged += p =>
            {
                PhaseChanged?.Invoke(p);
                if (p == RunPhase.Limbo) LimboEntered?.Invoke();
            };
            _game.RunFinished += e => RunFinished?.Invoke(e);
        }

        void Start()
        {
            _game.StartRun(population, requiredFraction, seed);
        }

        void Update()
        {
            if (_game == null)
                return;

            var ev = _game.Tick(Time.deltaTime);

            if (ev.IsDead)
                HandleDeath();
        }

        void HandleDeath()
        {
            // Фасад уже перевёл забег в Лимбо или в концовку.
            // Здесь только реакция движка.
            if (_game.IsFinished)
                Time.timeScale = 1f;
        }

        // --- Публичное API для других систем ---

        /// <summary>Вызывать из кода убийства врага.</summary>
        public bool ReportKill() => _game != null && _game.KillEnemy();

        /// <summary>Урон водой (вёдра, мокрые тряпки).</summary>
        public void ApplyWaterDamage(float amount) => _game?.ApplyWaterDamage(amount);

        /// <summary>Ответ на вопрос весов. Вешать на кнопки UI Лимбо.</summary>
        public void AnswerLimbo(bool continueKilling)
        {
            if (_game == null) return;
            _game.AnswerLimbo(continueKilling ? LimboChoice.Continue : LimboChoice.Refuse);
            Time.timeScale = 1f;
        }

        /// <summary>Открыть выбор карточек. Возвращает ровно три.</summary>
        public System.Collections.Generic.IReadOnlyList<Calabash.Core.Progression.UpgradeCard> OpenCardChoice()
        {
            if (_game == null) return null;
            if (slowTimeOnCardPick) Time.timeScale = cardSlowTimeScale;
            return _game.DrawCards();
        }

        /// <summary>Взять карточку и вернуть время в норму.</summary>
        public void TakeCard(string cardId)
        {
            if (_game == null) return;
            _game.TakeCard(cardId);
            Time.timeScale = 1f;
        }

        /// <summary>Уклонение. secondsUntilHit — до попадания воды.</summary>
        public Calabash.Core.Movement.DodgeResult TryDodge(float secondsUntilHit = 0f)
            => _game?.TryDodge(secondsUntilHit) ?? Calabash.Core.Movement.DodgeResult.Failed;

        /// <summary>Применить способность.</summary>
        public bool UseAbility(Calabash.Core.Abilities.AbilityId id)
            => _game != null && _game.UseAbility(id);

        /// <summary>Разблокировать способность (на уровнях 5/10/15).</summary>
        public bool UnlockAbility(Calabash.Core.Abilities.AbilityId id)
            => _game != null && _game.UnlockAbility(id);
    }
}
#endif

using System;
using System.Collections.Generic;
using Calabash.Core.Combat;
using Calabash.Core.Endings;
using Calabash.Core.Fuel;
using Calabash.Core.Decay;
using Calabash.Core.Movement;
using Calabash.Core.Progression;
using Calabash.Core.Abilities;

namespace Calabash.Unity
{
    /// <summary>
    /// Тонкий фасад над чистой логикой. MonoBehaviour-скрипты зовут только его,
    /// поэтому вся игровая математика остаётся тестируемой без Unity.
    ///
    /// Использование в Unity:
    ///   var game = new CalabashGame();
    ///   game.StartRun(population: 48, requiredFraction: 0.85f);
    ///   // каждый кадр:
    ///   var ev = game.Tick(Time.deltaTime);
    ///   if (ev.KilledNow) SpawnFireBurst();
    ///   if (ev.EnteredLimbo) ShowScalesUI();
    ///
    /// Этот класс НЕ ссылается на UnityEngine, чтобы компилироваться и в тестах.
    /// </summary>
    public sealed class CalabashGame
    {
        FuelSystem _fuel;
        KillTracker _kills;
        RunDirector _run;
        DecaySystem _decay;
        DodgeSystem _dodge;
        PlayerStats _stats;
        AbilityLoadout _abilities;
        CardDeck _deck;

        public event Action<int> KillRegistered;
        public event Action<FuelState> FuelStateChanged;
        public event Action<RunPhase> PhaseChanged;
        public event Action<EndingKind> RunFinished;
        public event Action<DecayStage> DecayStageChanged;

        public CalabashGame()
        {
            _stats = new PlayerStats();
        }

        public RunPhase Phase => _run?.Phase ?? RunPhase.Playing;
        public EndingKind Ending => _run?.Ending ?? EndingKind.None;
        public bool IsPlaying => _run != null && _run.IsPlaying;
        public bool InLimbo => _run != null && _run.InLimbo;
        public bool IsFinished => _run != null && _run.IsFinished;

        public float Fuel => _fuel?.Current ?? 0f;
        public float FuelFraction => _fuel?.Fraction ?? 0f;
        public FuelState FuelState => _fuel?.State ?? FuelState.Normal;

        public int Kills => _kills?.Kills ?? 0;
        public int RequiredKills => _kills?.RequiredKills ?? 0;
        public bool QuotaMet => _kills != null && _kills.QuotaMet;

        public DecayStage Stage =>
            _decay != null && _kills != null ? _decay.StageFor(_kills.KillFraction) : DecayStage.Ash;

        public PlayerStats Stats => _stats;
        public AbilityLoadout Abilities => _abilities;
        public DodgeSystem Dodge => _dodge;
        public IReadOnlyList<UpgradeCard> LastDrawnCards { get; private set; }

        /// <summary>
        /// Начинает забег. Числа по умолчанию соответствуют дизайн-документу.
        /// </summary>
        public void StartRun(int population = 48, float requiredFraction = 0.85f,
                             int seed = 0)
        {
            var fuelConfig = new FuelConfig
            {
                BurnRateMultiplier = _stats.BurnRateMultiplier,
                MaxFuel = 100f + _stats.MaxFuelBonus,
                KillRefuel = 17f + _stats.KillRefuelBonus,
            };

            _fuel = new FuelSystem(fuelConfig);
            _kills = new KillTracker(new QuotaConfig
            {
                Population = population,
                RequiredFraction = requiredFraction,
            });
            _run = new RunDirector(new RunDirectorConfig(), _fuel, _kills);
            _decay = new DecaySystem(new DecayConfig());
            _dodge = new DodgeSystem(new DodgeConfig());
            _abilities = new AbilityLoadout();
            _deck = new CardDeck(DefaultCards(), seed);
            LastDrawnCards = null;
        }

        /// <summary>
        /// Один кадр. Возвращает сводку изменений — чтобы MonoBehaviour
        /// не сравнивал состояния вручную.
        /// </summary>
        public FrameEvents Tick(float deltaSeconds)
        {
            var ev = new FrameEvents();

            if (_run == null)
                return ev;

            var phaseBefore = _run.Phase;
            var fuelStateBefore = _fuel.State;
            var stageBefore = Stage;

            _dodge.Tick(deltaSeconds, _stats.DodgeCooldownMultiplier);
            _abilities.Tick(deltaSeconds, _stats.DodgeCooldownMultiplier);

            bool changed = _run.Tick(deltaSeconds);
            ev.StateChanged = changed;

            if (_fuel.State != fuelStateBefore)
            {
                ev.FuelStateChanged = true;
                ev.NewFuelState = _fuel.State;
                FuelStateChanged?.Invoke(_fuel.State);
            }

            if (_run.Phase != phaseBefore)
            {
                ev.PhaseChanged = true;
                ev.NewPhase = _run.Phase;
                PhaseChanged?.Invoke(_run.Phase);
            }

            if (_run.InLimbo)
                ev.EnteredLimbo = phaseBefore != RunPhase.Limbo;

            if (_run.IsFinished && phaseBefore != RunPhase.Finished)
            {
                ev.RunFinished = true;
                ev.Ending = _run.Ending;
                RunFinished?.Invoke(_run.Ending);
            }

            var stageNow = Stage;
            if (stageNow != stageBefore)
            {
                ev.DecayStageChanged = true;
                ev.NewDecayStage = stageNow;
                DecayStageChanged?.Invoke(stageNow);
            }

            ev.IsDead = _fuel.IsDead;
            return ev;
        }

        /// <summary>Регистрирует убийство врага.</summary>
        public bool KillEnemy()
        {
            if (_run == null || !_run.IsPlaying)
                return false;

            bool ok = _run.RegisterKill();
            if (ok)
            {
                KillRegistered?.Invoke(_kills.Kills);
            }
            return ok;
        }

        /// <summary>Урон от воды: повышенный, но не смертельный сам по себе.</summary>
        public void ApplyWaterDamage(float amount)
        {
            if (_fuel == null) return;
            float mitigated = amount * (1f - _stats.WaterResistance);
            _fuel.Modify(-mitigated);
        }

        /// <summary>Ответ на вопрос весов в Лимбо.</summary>
        public bool AnswerLimbo(LimboChoice choice)
            => _run != null && _run.AnswerLimbo(choice);

        /// <summary>Попытка уклонения. secondsUntilHit — до попадания воды.</summary>
        public DodgeResult TryDodge(float secondsUntilHit = 0f)
            => _dodge?.TryDodge(secondsUntilHit) ?? DodgeResult.Failed;

        /// <summary>Вытянуть карточки на выбор (всегда три).</summary>
        public IReadOnlyList<UpgradeCard> DrawCards()
        {
            LastDrawnCards = _deck?.Draw(3);
            return LastDrawnCards;
        }

        /// <summary>Взять карточку из последнего набора.</summary>
        public bool TakeCard(string cardId)
        {
            if (_deck == null || LastDrawnCards == null)
                return false;

            bool offered = false;
            foreach (var c in LastDrawnCards)
                if (c.Id == cardId) { offered = true; break; }

            if (!offered)
                return false;

            if (!_deck.Take(cardId))
                return false;

            if (cardId != CardDeck.SkipCardId)
            {
                foreach (var c in LastDrawnCards)
                {
                    if (c.Id == cardId)
                    {
                        _stats.Add(c);
                        break;
                    }
                }
            }

            LastDrawnCards = null;
            return true;
        }

        /// <summary>Разблокировать способность (на уровнях 5/10/15).</summary>
        public bool UnlockAbility(AbilityId id) => _abilities != null && _abilities.Unlock(id);

        /// <summary>Применить способность, если готова.</summary>
        public bool UseAbility(AbilityId id)
        {
            var slot = _abilities?.Get(id);
            if (slot == null || !slot.TryUse())
                return false;

            // Способности стоят топлива
            if (slot.Def.FuelCost > 0f)
                _fuel.Modify(-slot.Def.FuelCost);

            return true;
        }

        /// <summary>Каталог карточек по умолчанию — по дизайн-документу.</summary>
        public static UpgradeCard[] DefaultCards()
        {
            return new[]
            {
                new UpgradeCard("fast_burn", "Быстрое горение", Branch.Heat,
                    "Топливо тратится быстрее, урон выше.",
                    new CardEffect { BurnRateMultiplier = 1.35f, DamageBonus = 0.25f }),

                new UpgradeCard("speed", "Скорость", Branch.Ash,
                    "Двигаешься быстрее.",
                    new CardEffect { MoveSpeedMultiplier = 1.2f }),

                new UpgradeCard("heat", "Жар", Branch.Heat,
                    "Больше урона и радиуса.",
                    new CardEffect { DamageBonus = 0.3f, AreaBonus = 0.5f }),

                new UpgradeCard("thick_skin", "Толстая кожа", Branch.Ember,
                    "Меньше урона, медленнее движение.",
                    new CardEffect { MoveSpeedMultiplier = 0.92f, WaterResistance = 0.25f }),

                new UpgradeCard("dry_cloth", "Сухая тряпка", Branch.Ember,
                    "Сопротивление воде.",
                    new CardEffect { WaterResistance = 0.5f }),

                new UpgradeCard("second_wind", "Второе дыхание", Branch.Heat,
                    "Больше топлива и больше с убийства.",
                    new CardEffect { MaxFuelBonus = 25f, KillRefuelBonus = 5f }),

                new UpgradeCard("silence", "Молчание", Branch.Ash,
                    "Огонь замолкает. Реплики пропадают.",
                    new CardEffect { SilencesFire = true }),

                new UpgradeCard("light_step", "Лёгкий шаг", Branch.Ash,
                    "Уклонение восстанавливается быстрее.",
                    new CardEffect { DodgeCooldownMultiplier = 1.6f }),

                new UpgradeCard("scatter", "Разлёт", Branch.Heat,
                    "Ошмётки летят дальше и их больше.",
                    new CardEffect { ExtraProjectiles = 3, AreaBonus = 0.3f }),
            };
        }
    }

    /// <summary>Сводка изменений за кадр — чтобы MonoBehaviour не гадал.</summary>
    public struct FrameEvents
    {
        public bool StateChanged;
        public bool IsDead;
        public bool EnteredLimbo;
        public bool PhaseChanged;
        public RunPhase NewPhase;
        public bool RunFinished;
        public EndingKind Ending;
        public bool FuelStateChanged;
        public FuelState NewFuelState;
        public bool DecayStageChanged;
        public DecayStage NewDecayStage;
    }
}

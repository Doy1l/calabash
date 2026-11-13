using System;
using Calabash.Core.Combat;
using Calabash.Core.Decay;
using Calabash.Core.Endings;
using Calabash.Core.Fuel;
using Calabash.Core.Movement;
using Calabash.Core.Progression;
using Calabash.Core.Abilities;

namespace Calabash.Tests
{
    public static class Program
    {
        public static int Main()
        {
            Console.WriteLine("Calabash — проверка игровых механик");

            FuelTests();
            DecayTests();
            KillTests();
            EndingTests();
            CardTests();
            AbilityTests();
            DodgeTests();
            IntegrationTests();
            InvariantTests();
            EdgeCaseTests.Run();
            GameFacadeTests.Run();

            return TestRunner.Summary();
        }

        // ================================================================ ТОПЛИВО

        static void FuelTests()
        {
            TestRunner.Section("Топливо = здоровье");

            TestRunner.Run("стартует полным", () =>
            {
                var f = new FuelSystem(new FuelConfig());
                TestRunner.Near(100f, f.Current);
                TestRunner.Eq(FuelState.Normal, f.State);
                TestRunner.False(f.IsDead);
            });

            TestRunner.Run("убывает со временем", () =>
            {
                var f = new FuelSystem(new FuelConfig { BurnPerSecond = 10f });
                f.Tick(1f);
                TestRunner.Near(90f, f.Current);
                f.Tick(2f);
                TestRunner.Near(70f, f.Current);
            });

            TestRunner.Run("потухание возвращает true один раз", () =>
            {
                var f = new FuelSystem(new FuelConfig { BurnPerSecond = 100f });
                TestRunner.True(f.Tick(1f), "первый тик должен убить");
                TestRunner.True(f.IsDead);
                TestRunner.False(f.Tick(1f), "второй тик уже мёртв");
            });

            TestRunner.Run("топливо не уходит в минус", () =>
            {
                var f = new FuelSystem(new FuelConfig { BurnPerSecond = 1000f });
                f.Tick(5f);
                TestRunner.Near(0f, f.Current);
            });

            TestRunner.Run("убийство пополняет топливо", () =>
            {
                var f = new FuelSystem(new FuelConfig { BurnPerSecond = 10f, KillRefuel = 25f });
                f.Tick(4f);              // 100 - 40 = 60
                TestRunner.Near(60f, f.Current);
                float gained = f.RefuelFromKill();
                TestRunner.Near(25f, gained);
                TestRunner.Near(85f, f.Current);
            });

            TestRunner.Run("пополнение не превышает максимум", () =>
            {
                var f = new FuelSystem(new FuelConfig { KillRefuel = 50f });
                float gained = f.RefuelFromKill();
                TestRunner.Near(0f, gained, 0.001f, "нечего добавлять");
                TestRunner.Near(100f, f.Current);
            });

            TestRunner.Run("пороги состояний работают", () =>
            {
                var f = new FuelSystem(new FuelConfig { BurnPerSecond = 10f, HungryAt = 0.4f, StarvingAt = 0.15f });
                TestRunner.Eq(FuelState.Normal, f.State);
                f.Tick(6.1f);            // 39% — голоден
                TestRunner.Eq(FuelState.Hungry, f.State, "при 39%");
                f.Tick(3f);              // ~9% — умирает
                TestRunner.Eq(FuelState.Starving, f.State, "при 9%");
            });

            TestRunner.Run("множитель горения ускоряет расход", () =>
            {
                var f = new FuelSystem(new FuelConfig { BurnPerSecond = 10f, BurnRateMultiplier = 2f });
                f.Tick(1f);
                TestRunner.Near(80f, f.Current);
            });

            TestRunner.Run("мёртвый не пополняется", () =>
            {
                var f = new FuelSystem(new FuelConfig { BurnPerSecond = 1000f });
                f.Tick(1f);
                float gained = f.RefuelFromKill();
                TestRunner.Near(0f, gained);
                TestRunner.Near(0f, f.Current);
            });

            TestRunner.Run("оживление после Лимбо", () =>
            {
                var f = new FuelSystem(new FuelConfig { BurnPerSecond = 1000f });
                f.Tick(1f);
                TestRunner.True(f.IsDead);
                f.Revive(1f);
                TestRunner.Near(100f, f.Current);
                TestRunner.False(f.IsDead);
                TestRunner.Eq(FuelState.Normal, f.State);
            });
        }

        // ================================================================ ТЛЕНИЕ

        static void DecayTests()
        {
            TestRunner.Section("Стадии тления (тело — счётчик)");

            var d = new DecaySystem(new DecayConfig());

            TestRunner.Run("стадия 0 в начале", () =>
            {
                TestRunner.Eq(DecayStage.Ash, d.StageFor(0f));
                TestRunner.Eq(DecayStage.Ash, d.StageFor(0.05f));
            });

            TestRunner.Run("стадии растут по порогам", () =>
            {
                TestRunner.Eq(DecayStage.Smouldering, d.StageFor(0.15f));
                TestRunner.Eq(DecayStage.Ember, d.StageFor(0.40f));
                TestRunner.Eq(DecayStage.Furnace, d.StageFor(0.70f));
                TestRunner.Eq(DecayStage.Bonfire, d.StageFor(0.95f));
            });

            TestRunner.Run("границы стадий точные", () =>
            {
                TestRunner.Eq(DecayStage.Ash, d.StageFor(0.099f));
                TestRunner.Eq(DecayStage.Smouldering, d.StageFor(0.10f));
                TestRunner.Eq(DecayStage.Ember, d.StageFor(0.30f));
                TestRunner.Eq(DecayStage.Furnace, d.StageFor(0.55f));
                TestRunner.Eq(DecayStage.Bonfire, d.StageFor(0.80f));
            });

            TestRunner.Run("прогресс внутри стадии 0..1", () =>
            {
                float p = d.StageProgress(0.15f);
                TestRunner.Greater(p, 0f);
                TestRunner.Less(p, 1.001f);
            });

            TestRunner.Run("человечность падает до нуля", () =>
            {
                TestRunner.Near(1f, d.Humanity(0f));
                TestRunner.Near(0f, d.Humanity(0.9f));
                TestRunner.Near(0.5f, d.Humanity(0.4f));
            });

            TestRunner.Run("некорректные пороги отвергаются", () =>
            {
                TestRunner.Throws<ArgumentException>(() =>
                    new DecaySystem(new DecayConfig { AshUntil = 0.5f, SmoulderingUntil = 0.2f }));
            });

            TestRunner.Run("доля вне диапазона обрезается", () =>
            {
                TestRunner.Eq(DecayStage.Ash, d.StageFor(-5f));
                TestRunner.Eq(DecayStage.Bonfire, d.StageFor(99f));
            });
        }

        // ================================================================ КВОТА

        static void KillTests()
        {
            TestRunner.Section("Квота убийств");

            TestRunner.Run("нужное число считается верно", () =>
            {
                var k = new KillTracker(new QuotaConfig { Population = 48, RequiredFraction = 0.85f });
                TestRunner.Eq(41, k.RequiredKills, "ceil(48*0.85)");
            });

            TestRunner.Run("квота выполняется", () =>
            {
                var k = new KillTracker(new QuotaConfig { Population = 10, RequiredFraction = 0.8f });
                TestRunner.Eq(8, k.RequiredKills);
                for (int i = 0; i < 7; i++) k.RegisterKill();
                TestRunner.False(k.QuotaMet, "7 из 8");
                k.RegisterKill();
                TestRunner.True(k.QuotaMet, "8 из 8");
            });

            TestRunner.Run("убивать больше населения нельзя", () =>
            {
                var k = new KillTracker(new QuotaConfig { Population = 3, RequiredFraction = 1f });
                TestRunner.True(k.RegisterKill());
                TestRunner.True(k.RegisterKill());
                TestRunner.True(k.RegisterKill());
                TestRunner.False(k.RegisterKill(), "четвёртое убийство невозможно");
                TestRunner.Eq(3, k.Kills);
            });

            TestRunner.Run("доля убийств корректна", () =>
            {
                var k = new KillTracker(new QuotaConfig { Population = 20, RequiredFraction = 0.5f });
                for (int i = 0; i < 5; i++) k.RegisterKill();
                TestRunner.Near(0.25f, k.KillFraction);
            });

            TestRunner.Run("остаток до квоты не отрицателен", () =>
            {
                var k = new KillTracker(new QuotaConfig { Population = 5, RequiredFraction = 0.4f });
                TestRunner.Eq(2, k.RequiredKills);
                k.RegisterKill();
                k.RegisterKill();
                k.RegisterKill();
                TestRunner.Eq(0, k.RemainingToQuota);
            });

            TestRunner.Run("некорректная конфигурация отвергается", () =>
            {
                TestRunner.Throws<ArgumentException>(() =>
                    new KillTracker(new QuotaConfig { Population = 0 }));
                TestRunner.Throws<ArgumentException>(() =>
                    new KillTracker(new QuotaConfig { Population = 10, RequiredFraction = 1.5f }));
            });
        }

        // ================================================================ КОНЦОВКИ

        static void EndingTests()
        {
            TestRunner.Section("Три концовки");

            // --- Рай без убийств ---
            TestRunner.Run("АФК без убийств ведёт в Рай", () =>
            {
                var fuel = new FuelSystem(new FuelConfig { BurnPerSecond = 100f });
                var kills = new KillTracker(new QuotaConfig { Population = 20, RequiredFraction = 0.9f });
                var run = new RunDirector(new RunDirectorConfig(), fuel, kills);

                run.Tick(2f);   // сгорел, никого не тронув
                TestRunner.True(run.IsFinished, "забег закончен");
                TestRunner.Eq(EndingKind.HeavenPure, run.Ending);
            });

            // --- Лимбо после убийств ---
            TestRunner.Run("смерть после убийств ведёт в Лимбо", () =>
            {
                var (run, fuel, kills) = MakeRun();
                kills.RegisterKill();
                fuel.Modify(-95f);          // почти потух
                run.Tick(10f);

                TestRunner.True(run.InLimbo, "должна быть фаза Лимбо");
                TestRunner.Eq(LimboChoice.Pending, run.LimboAnswer);
                TestRunner.False(run.IsFinished, "это ещё не концовка");
            });

            TestRunner.Run("в Лимбо нельзя убивать", () =>
            {
                var (run, fuel, kills) = MakeRun();
                kills.RegisterKill();
                fuel.Modify(-95f);
                run.Tick(10f);
                TestRunner.True(run.InLimbo);

                int before = kills.Kills;
                TestRunner.False(run.RegisterKill(), "убийство в Лимбо запрещено");
                TestRunner.Eq(before, kills.Kills);
            });

            // --- Лимбо: НЕТ → Рай через искупление ---
            TestRunner.Run("отказ в Лимбо ведёт в Рай через искупление", () =>
            {
                var (run, fuel, kills) = MakeRun();
                kills.RegisterKill();
                fuel.Modify(-95f);
                run.Tick(10f);
                TestRunner.True(run.InLimbo);

                TestRunner.True(run.AnswerLimbo(LimboChoice.Refuse));
                TestRunner.Eq(EndingKind.HeavenRedeemed, run.Ending);
                TestRunner.True(run.IsFinished);
            });

            // --- Лимбо: ДА → вторая попытка ---
            TestRunner.Run("согласие в Лимбо возвращает в бой", () =>
            {
                var (run, fuel, kills) = MakeRun();
                kills.RegisterKill();
                fuel.Modify(-95f);
                run.Tick(10f);

                TestRunner.True(run.AnswerLimbo(LimboChoice.Continue));
                TestRunner.True(run.IsPlaying, "снова играем");
                TestRunner.False(run.IsFinished);
                TestRunner.Near(100f, fuel.Current, 0.01f, "топливо восстановлено");
                TestRunner.Eq(1, run.LimboReturns);
            });

            TestRunner.Run("прокачка и убийства сохраняются после возврата", () =>
            {
                var (run, fuel, kills) = MakeRun();
                for (int i = 0; i < 5; i++) kills.RegisterKill();
                fuel.Modify(-95f);
                run.Tick(10f);
                run.AnswerLimbo(LimboChoice.Continue);
                TestRunner.Eq(5, kills.Kills, "убийства сохранены");
            });

            // --- Вторая смерть после возврата → Ад, цепи ---
            TestRunner.Run("вторая смерть после Лимбо ведёт в Ад (цепи)", () =>
            {
                var (run, fuel, kills) = MakeRun();
                kills.RegisterKill();
                fuel.Modify(-95f);
                run.Tick(10f);
                run.AnswerLimbo(LimboChoice.Continue);
                TestRunner.True(run.IsPlaying);

                fuel.Modify(-200f);     // сгорел снова
                run.Tick(1f);

                TestRunner.Eq(EndingKind.HellChained, run.Ending);
                TestRunner.True(run.IsFinished);
            });

            TestRunner.Run("третьей попытки не даётся", () =>
            {
                var (run, fuel, kills) = MakeRun();
                kills.RegisterKill();
                fuel.Modify(-95f);
                run.Tick(10f);
                run.AnswerLimbo(LimboChoice.Continue);
                fuel.Modify(-200f);
                run.Tick(1f);

                TestRunner.True(run.IsFinished);
                TestRunner.False(run.InLimbo, "второго Лимбо нет");
            });

            // --- Победа: выполнил квоту → Ад, покоритель ---
            TestRunner.Run("выполнение квоты ведёт в Ад (покоритель)", () =>
            {
                var fuel = new FuelSystem(new FuelConfig { BurnPerSecond = 0f });
                var kills = new KillTracker(new QuotaConfig { Population = 10, RequiredFraction = 0.5f });
                var run = new RunDirector(new RunDirectorConfig(), fuel, kills);

                for (int i = 0; i < 4; i++)
                {
                    TestRunner.True(run.RegisterKill());
                    TestRunner.False(run.IsFinished, $"после {i + 1} из 5 ещё играем");
                }
                run.RegisterKill();     // 5-е — квота
                TestRunner.Eq(EndingKind.HellConqueror, run.Ending);
            });

            TestRunner.Run("после концовки убийства не считаются", () =>
            {
                var fuel = new FuelSystem(new FuelConfig { BurnPerSecond = 0f });
                var kills = new KillTracker(new QuotaConfig { Population = 2, RequiredFraction = 0.5f });
                var run = new RunDirector(new RunDirectorConfig(), fuel, kills);
                run.RegisterKill();
                TestRunner.True(run.IsFinished);

                int before = kills.Kills;
                TestRunner.False(run.RegisterKill(), "забег закончен");
                TestRunner.Eq(before, kills.Kills);
            });

            TestRunner.Run("ответ в Лимбо вне Лимбо невозможен", () =>
            {
                var (run, _, _) = MakeRun();
                TestRunner.False(run.AnswerLimbo(LimboChoice.Continue), "мы ещё не в Лимбо");
            });
        }

        static (RunDirector, FuelSystem, KillTracker) MakeRun()
        {
            var fuel = new FuelSystem(new FuelConfig { BurnPerSecond = 1f });
            var kills = new KillTracker(new QuotaConfig { Population = 100, RequiredFraction = 0.9f });
            var run = new RunDirector(new RunDirectorConfig(), fuel, kills);
            return (run, fuel, kills);
        }

        // ================================================================ КАРТОЧКИ

        static void CardTests()
        {
            TestRunner.Section("Карточки прокачки");

            TestRunner.Run("всегда выдаётся ровно три карточки", () =>
            {
                var deck = new CardDeck(MakeCards(2), seed: 1);
                var drawn = deck.Draw(3);
                TestRunner.Eq(3, drawn.Count, "слотов всегда три");
            });

            TestRunner.Run("при нехватке добивается «не брать»", () =>
            {
                var deck = new CardDeck(MakeCards(1), seed: 2);
                var drawn = deck.Draw(3);
                TestRunner.Eq(3, drawn.Count);
                int skips = 0;
                foreach (var c in drawn)
                    if (c.Id == CardDeck.SkipCardId) skips++;
                TestRunner.Eq(2, skips, "два пустых слота");
            });

            TestRunner.Run("взятая карточка не выпадает снова", () =>
            {
                var deck = new CardDeck(MakeCards(3), seed: 3);
                var first = deck.Draw(3);
                string takenId = null;
                foreach (var c in first)
                    if (c.Id != CardDeck.SkipCardId) { takenId = c.Id; break; }

                TestRunner.True(deck.Take(takenId), "берём карточку");
                var second = deck.Draw(3);
                foreach (var c in second)
                    TestRunner.True(c.Id != takenId, "взятая не должна выпасть");
            });

            TestRunner.Run("нельзя взять одну карточку дважды", () =>
            {
                var deck = new CardDeck(MakeCards(3), seed: 4);
                TestRunner.True(deck.Take("c0"));
                TestRunner.False(deck.Take("c0"), "повторно нельзя");
            });

            TestRunner.Run("отказ «не брать» всегда разрешён", () =>
            {
                var deck = new CardDeck(MakeCards(1), seed: 5);
                TestRunner.True(deck.Take(CardDeck.SkipCardId));
                TestRunner.Eq(1, deck.AvailableCount, "отказ не тратит пул");
            });

            TestRunner.Run("статы складываются", () =>
            {
                var stats = new PlayerStats();
                stats.Add(new UpgradeCard("a", "A", Branch.Heat, "", new CardEffect { DamageBonus = 0.2f }));
                stats.Add(new UpgradeCard("b", "B", Branch.Heat, "", new CardEffect { DamageBonus = 0.3f }));
                TestRunner.Near(0.5f, stats.DamageBonus);
            });

            TestRunner.Run("множители перемножаются", () =>
            {
                var stats = new PlayerStats();
                stats.Add(new UpgradeCard("a", "A", Branch.Ash, "", new CardEffect { MoveSpeedMultiplier = 1.2f }));
                stats.Add(new UpgradeCard("b", "B", Branch.Ash, "", new CardEffect { MoveSpeedMultiplier = 1.5f }));
                TestRunner.Near(1.8f, stats.MoveSpeedMultiplier);
            });

            TestRunner.Run("«Молчание» глушит огонь", () =>
            {
                var stats = new PlayerStats();
                TestRunner.False(stats.FireSilenced);
                stats.Add(new UpgradeCard("mute", "Молчание", Branch.Ash, "", new CardEffect { SilencesFire = true }));
                TestRunner.True(stats.FireSilenced);
            });

            TestRunner.Run("сопротивление воде не выше 100%", () =>
            {
                var stats = new PlayerStats();
                stats.Add(new UpgradeCard("w1", "W1", Branch.Ember, "", new CardEffect { WaterResistance = 0.7f }));
                stats.Add(new UpgradeCard("w2", "W2", Branch.Ember, "", new CardEffect { WaterResistance = 0.7f }));
                TestRunner.Near(1f, stats.WaterResistance);
            });

            TestRunner.Run("отказ не попадает в статы", () =>
            {
                var stats = new PlayerStats();
                stats.Add(new UpgradeCard(CardDeck.SkipCardId, "Не брать", Branch.Ash, "", new CardEffect()));
                TestRunner.Eq(0, stats.Cards.Count);
            });

            TestRunner.Run("карточка без id отвергается", () =>
            {
                TestRunner.Throws<ArgumentException>(() =>
                    new UpgradeCard("", "X", Branch.Heat, "", new CardEffect()));
            });
        }

        static UpgradeCard[] MakeCards(int n)
        {
            var cards = new UpgradeCard[n];
            for (int i = 0; i < n; i++)
                cards[i] = new UpgradeCard($"c{i}", $"Card {i}", Branch.Heat, "", new CardEffect());
            return cards;
        }

        // ================================================================ СПОСОБНОСТИ

        static void AbilityTests()
        {
            TestRunner.Section("Способности");

            TestRunner.Run("каталог знает все уровни", () =>
            {
                TestRunner.True(AbilityCatalog.ForLevel(5).Count >= 3, "на 5 уровне минимум 3");
                TestRunner.True(AbilityCatalog.ForLevel(10).Count > AbilityCatalog.ForLevel(5).Count);
                TestRunner.True(AbilityCatalog.ForLevel(15).Count > AbilityCatalog.ForLevel(10).Count);
            });

            TestRunner.Run("пассивки не выдаются как активные", () =>
            {
                foreach (var a in AbilityCatalog.ForLevel(15))
                    TestRunner.False(a.IsPassive, $"пассивка {a.Id} не должна быть в списке активных");
            });

            TestRunner.Run("способность начинается готовой", () =>
            {
                var slot = new AbilitySlot(AbilityCatalog.Get(AbilityId.Trail));
                TestRunner.True(slot.IsReady);
                TestRunner.Near(0f, slot.CooldownFraction);
            });

            TestRunner.Run("применение запускает откат", () =>
            {
                var slot = new AbilitySlot(AbilityCatalog.Get(AbilityId.Trail));
                TestRunner.True(slot.TryUse());
                TestRunner.False(slot.IsReady, "сразу после применения не готова");
                TestRunner.False(slot.TryUse(), "повторно нельзя");
            });

            TestRunner.Run("откат истекает", () =>
            {
                var slot = new AbilitySlot(AbilityCatalog.Get(AbilityId.Trail));
                slot.TryUse();
                slot.Tick(10f);
                TestRunner.True(slot.IsReady);
            });

            TestRunner.Run("набор способностей не дублирует", () =>
            {
                var loadout = new AbilityLoadout();
                TestRunner.True(loadout.Unlock(AbilityId.Trail));
                TestRunner.False(loadout.Unlock(AbilityId.Trail), "повторно нельзя");
                TestRunner.Eq(1, loadout.Count);
            });

            TestRunner.Run("тик набора двигает все откаты", () =>
            {
                var loadout = new AbilityLoadout();
                loadout.Unlock(AbilityId.Trail);
                loadout.Unlock(AbilityId.Spit);
                loadout.Get(AbilityId.Trail).TryUse();
                loadout.Tick(10f);
                TestRunner.True(loadout.Get(AbilityId.Trail).IsReady);
            });
        }

        // ================================================================ УКЛОНЕНИЕ

        static void DodgeTests()
        {
            TestRunner.Section("Уклонение (Ghostrunner)");

            TestRunner.Run("обычное уклонение работает", () =>
            {
                var d = new DodgeSystem(new DodgeConfig());
                TestRunner.Eq(DodgeResult.Normal, d.TryDodge(0f));
                TestRunner.True(d.IsDodging);
            });

            TestRunner.Run("на откате уклоняться нельзя", () =>
            {
                var d = new DodgeSystem(new DodgeConfig { Charges = 1 });
                d.TryDodge(0f);
                TestRunner.Eq(DodgeResult.Failed, d.TryDodge(0f));
                TestRunner.True(d.IsOnCooldown);
            });

            TestRunner.Run("идеальное уклонение в окне", () =>
            {
                var d = new DodgeSystem(new DodgeConfig { PerfectWindow = 0.12f });
                TestRunner.Eq(DodgeResult.Perfect, d.TryDodge(0.08f));
                TestRunner.True(d.IsPerfectActive);
                TestRunner.Less(d.TimeScale, 1f, "время должно замедлиться");
            });

            TestRunner.Run("слишком раннее — не идеальное", () =>
            {
                var d = new DodgeSystem(new DodgeConfig { PerfectWindow = 0.12f });
                TestRunner.Eq(DodgeResult.Normal, d.TryDodge(0.5f));
                TestRunner.False(d.IsPerfectActive);
            });

            TestRunner.Run("замедление заканчивается", () =>
            {
                var d = new DodgeSystem(new DodgeConfig { PerfectDuration = 0.3f });
                d.TryDodge(0.05f);
                d.Tick(0.5f);
                TestRunner.False(d.IsPerfectActive);
                TestRunner.Near(1f, d.TimeScale);
            });

            TestRunner.Run("откат восстанавливается", () =>
            {
                var d = new DodgeSystem(new DodgeConfig { Cooldown = 0.75f, Charges = 1 });
                d.TryDodge(0f);
                TestRunner.Eq(DodgeResult.Failed, d.TryDodge(0f));
                d.Tick(0.8f);
                TestRunner.Eq(DodgeResult.Normal, d.TryDodge(0f));
            });

            TestRunner.Run("множитель отката ускоряет восстановление", () =>
            {
                var d = new DodgeSystem(new DodgeConfig { Cooldown = 1f, Charges = 1 });
                d.TryDodge(0f);
                d.Tick(0.5f, cooldownMultiplier: 2f);
                TestRunner.Eq(DodgeResult.Normal, d.TryDodge(0f), "должно восстановиться вдвое быстрее");
            });

            TestRunner.Run("сброс после Лимбо", () =>
            {
                var d = new DodgeSystem(new DodgeConfig());
                d.TryDodge(0f);
                d.Reset();
                TestRunner.Eq(DodgeResult.Normal, d.TryDodge(0f));
            });
        }

        // ================================================================ ИНТЕГРАЦИЯ

        static void IntegrationTests()
        {
            TestRunner.Section("Связка механик");

            TestRunner.Run("полный цикл: убийства кормят огонь", () =>
            {
                var fuel = new FuelSystem(new FuelConfig { BurnPerSecond = 5f, KillRefuel = 20f });
                var kills = new KillTracker(new QuotaConfig { Population = 50, RequiredFraction = 0.9f });
                var run = new RunDirector(new RunDirectorConfig(), fuel, kills);
                var decay = new DecaySystem(new DecayConfig());

                // 10 секунд без убийств
                for (int i = 0; i < 10; i++) run.Tick(1f);
                TestRunner.Near(50f, fuel.Current, 0.5f, "топливо просело");

                // Убийство возвращает топливо
                run.RegisterKill();
                TestRunner.Near(70f, fuel.Current, 0.5f);

                // Стадия тления зависит от доли убийств
                TestRunner.Eq(DecayStage.Ash, decay.StageFor(kills.KillFraction));
            });

            TestRunner.Run("забег до победы через квоту", () =>
            {
                var fuel = new FuelSystem(new FuelConfig { BurnPerSecond = 1f, KillRefuel = 20f });
                var kills = new KillTracker(new QuotaConfig { Population = 12, RequiredFraction = 0.5f });
                var run = new RunDirector(new RunDirectorConfig(), fuel, kills);

                int guard = 0;
                while (!run.IsFinished && guard++ < 1000)
                {
                    run.Tick(0.5f);
                    if (!run.IsPlaying) break;
                    run.RegisterKill();
                }

                TestRunner.Eq(EndingKind.HellConqueror, run.Ending);
                TestRunner.False(fuel.IsDead, "не должен сгореть при постоянных убийствах");
            });

            TestRunner.Run("голод убивает без убийств", () =>
            {
                var fuel = new FuelSystem(new FuelConfig { BurnPerSecond = 5f });
                var kills = new KillTracker(new QuotaConfig { Population = 50, RequiredFraction = 0.9f });
                var run = new RunDirector(new RunDirectorConfig(), fuel, kills);

                int guard = 0;
                while (!run.IsFinished && guard++ < 1000)
                    run.Tick(0.5f);

                TestRunner.Eq(EndingKind.HeavenPure, run.Ending);
            });

            TestRunner.Run("стадии тления следуют за прогрессом забега", () =>
            {
                var kills = new KillTracker(new QuotaConfig { Population = 40, RequiredFraction = 1f });
                var decay = new DecaySystem(new DecayConfig());

                var seen = new System.Collections.Generic.HashSet<DecayStage>();
                for (int i = 0; i < 40; i++)
                {
                    kills.RegisterKill();
                    seen.Add(decay.StageFor(kills.KillFraction));
                }

                TestRunner.Eq(5, seen.Count, "за забег должны встретиться все 5 стадий");
            });
        }

        // ================================================================ ИНВАРИАНТЫ

        static void InvariantTests()
        {
            TestRunner.Section("Инварианты (не должны нарушаться никогда)");

            TestRunner.Run("топливо всегда в [0, max]", () =>
            {
                var rng = new Random(42);
                for (int trial = 0; trial < 200; trial++)
                {
                    var f = new FuelSystem(new FuelConfig { BurnPerSecond = 3f, KillRefuel = 15f });
                    for (int step = 0; step < 100; step++)
                    {
                        int action = rng.Next(3);
                        if (action == 0) f.Tick((float)rng.NextDouble());
                        else if (action == 1) f.RefuelFromKill();
                        else f.Modify((float)(rng.NextDouble() * 40 - 20));

                        TestRunner.True(f.Current >= 0f, $"топливо ушло в минус: {f.Current}");
                        TestRunner.True(f.Current <= f.Max + 0.001f, $"топливо превысило максимум: {f.Current}");
                    }
                }
            });

            TestRunner.Run("убийств не больше населения", () =>
            {
                var rng = new Random(7);
                for (int trial = 0; trial < 100; trial++)
                {
                    var k = new KillTracker(new QuotaConfig { Population = rng.Next(1, 30), RequiredFraction = 0.6f });
                    for (int i = 0; i < 60; i++)
                        k.RegisterKill();
                    TestRunner.True(k.Kills <= k.Population, $"убито {k.Kills} из {k.Population}");
                }
            });

            TestRunner.Run("концовка назначается один раз", () =>
            {
                var fuel = new FuelSystem(new FuelConfig { BurnPerSecond = 100f });
                var kills = new KillTracker(new QuotaConfig { Population = 10, RequiredFraction = 0.9f });
                var run = new RunDirector(new RunDirectorConfig(), fuel, kills);

                TestRunner.False(run.IsFinished);
                run.Tick(2f);
                TestRunner.True(run.IsFinished);
                var first = run.Ending;

                run.Tick(100f);
                run.RegisterKill();
                TestRunner.Eq(first, run.Ending, "концовка не должна меняться");
            });

            TestRunner.Run("фаза Лимбо не переходит в игру без ответа", () =>
            {
                var fuel = new FuelSystem(new FuelConfig { BurnPerSecond = 100f });
                var kills = new KillTracker(new QuotaConfig { Population = 10, RequiredFraction = 0.9f });
                var run = new RunDirector(new RunDirectorConfig(), fuel, kills);
                run.RegisterKill();
                run.Tick(2f);

                TestRunner.True(run.InLimbo || run.IsFinished);
                if (run.InLimbo)
                {
                    for (int i = 0; i < 100; i++)
                        run.Tick(1f);
                    TestRunner.True(run.InLimbo, "время в Лимбо не должно само вернуть в бой");
                }
            });

            TestRunner.Run("доля убийств всегда 0..1", () =>
            {
                var rng = new Random(99);
                for (int trial = 0; trial < 100; trial++)
                {
                    var k = new KillTracker(new QuotaConfig { Population = rng.Next(1, 50), RequiredFraction = 0.5f });
                    for (int i = 0; i < 60; i++)
                    {
                        k.RegisterKill();
                        TestRunner.True(k.KillFraction >= 0f && k.KillFraction <= 1f,
                            $"доля вне диапазона: {k.KillFraction}");
                    }
                }
            });

            TestRunner.Run("стадия тления монотонна по убийствам", () =>
            {
                var decay = new DecaySystem(new DecayConfig());
                int prev = -1;
                for (int i = 0; i <= 100; i++)
                {
                    int stage = (int)decay.StageFor(i / 100f);
                    TestRunner.True(stage >= prev, $"стадия уменьшилась на {i}%");
                    prev = stage;
                }
            });
        }
    }
}

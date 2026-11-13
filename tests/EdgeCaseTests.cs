using System;
using Calabash.Core.Combat;
using Calabash.Core.Fuel;
using Calabash.Core.Endings;

namespace Calabash.Tests
{
    /// <summary>
    /// Тесты на «неприятные» сценарии: граничные значения, порядок вызовов,
    /// одновременные события. Именно здесь обычно живут баги.
    /// </summary>
    public static class EdgeCaseTests
    {
        public static void Run()
        {
            TestRunner.Section("Граничные случаи и порядок событий");

            TestRunner.Run("смерть от внешнего урона в тике", () =>
            {
                var fuel = new FuelSystem(new FuelConfig { BurnPerSecond = 0.1f });
                var kills = new KillTracker(new QuotaConfig { Population = 100, RequiredFraction = 0.9f });
                var run = new RunDirector(new RunDirectorConfig(), fuel, kills);

                kills.RegisterKill();
                run.RegisterKill();       // регистрируем через директора
                fuel.Modify(-200f);       // «вода» добила

                run.Tick(0.016f);
                TestRunner.True(run.InLimbo || run.IsFinished,
                    "смерть от внешнего урона должна разрешиться в тике");
            });

            TestRunner.Run("нулевой тик ничего не ломает", () =>
            {
                var fuel = new FuelSystem(new FuelConfig());
                TestRunner.False(fuel.Tick(0f));
                TestRunner.Near(100f, fuel.Current);
                TestRunner.False(fuel.Tick(-5f), "отрицательное время игнорируется");
                TestRunner.Near(100f, fuel.Current);
            });

            TestRunner.Run("огромный тик не переполняет", () =>
            {
                var fuel = new FuelSystem(new FuelConfig { BurnPerSecond = 1f });
                fuel.Tick(1e9f);
                TestRunner.Near(0f, fuel.Current);
                TestRunner.True(fuel.IsDead);
            });

            TestRunner.Run("убийство ровно на границе квоты", () =>
            {
                var kills = new KillTracker(new QuotaConfig { Population = 100, RequiredFraction = 0.5f });
                for (int i = 0; i < 49; i++) kills.RegisterKill();
                TestRunner.False(kills.QuotaMet);
                kills.RegisterKill();
                TestRunner.True(kills.QuotaMet, "50-е убийство закрывает квоту");
            });

            TestRunner.Run("квота 100% требует всех", () =>
            {
                var kills = new KillTracker(new QuotaConfig { Population = 7, RequiredFraction = 1f });
                TestRunner.Eq(7, kills.RequiredKills);
                for (int i = 0; i < 6; i++) kills.RegisterKill();
                TestRunner.False(kills.QuotaMet);
                kills.RegisterKill();
                TestRunner.True(kills.QuotaMet);
                TestRunner.True(kills.AllKilled);
            });

            TestRunner.Run("квота 0% выполняется сразу", () =>
            {
                var kills = new KillTracker(new QuotaConfig { Population = 10, RequiredFraction = 0f });
                TestRunner.Eq(0, kills.RequiredKills);
                TestRunner.True(kills.QuotaMet, "нулевая квота уже выполнена");
            });

            TestRunner.Run("смерть без убийств даёт Рай, даже если квота не выполнена", () =>
            {
                var fuel = new FuelSystem(new FuelConfig { BurnPerSecond = 50f });
                var kills = new KillTracker(new QuotaConfig { Population = 100, RequiredFraction = 0.99f });
                var run = new RunDirector(new RunDirectorConfig(), fuel, kills);
                run.Tick(10f);
                TestRunner.Eq(EndingKind.HeavenPure, run.Ending);
            });

            TestRunner.Run("возврат из Лимбо не сбрасывает квоту", () =>
            {
                var fuel = new FuelSystem(new FuelConfig { BurnPerSecond = 1f });
                var kills = new KillTracker(new QuotaConfig { Population = 20, RequiredFraction = 0.9f });
                var run = new RunDirector(new RunDirectorConfig(), fuel, kills);

                for (int i = 0; i < 10; i++) run.RegisterKill();
                fuel.Modify(-200f);
                run.Tick(0.1f);
                TestRunner.True(run.InLimbo);

                run.AnswerLimbo(LimboChoice.Continue);
                TestRunner.Eq(10, kills.Kills, "убийства сохранены");
                TestRunner.False(kills.QuotaMet, "квота ещё не выполнена");
            });

            TestRunner.Run("ответ в Лимбо дважды не проходит", () =>
            {
                var fuel = new FuelSystem(new FuelConfig { BurnPerSecond = 1f });
                var kills = new KillTracker(new QuotaConfig { Population = 50, RequiredFraction = 0.9f });
                var run = new RunDirector(new RunDirectorConfig(), fuel, kills);

                run.RegisterKill();
                fuel.Modify(-200f);
                run.Tick(0.1f);
                TestRunner.True(run.InLimbo);

                TestRunner.True(run.AnswerLimbo(LimboChoice.Continue));
                TestRunner.False(run.AnswerLimbo(LimboChoice.Refuse), "второй ответ уже вне Лимбо");
            });

            TestRunner.Run("запрет второй попытки сразу даёт цепи", () =>
            {
                var fuel = new FuelSystem(new FuelConfig { BurnPerSecond = 1f });
                var kills = new KillTracker(new QuotaConfig { Population = 50, RequiredFraction = 0.9f });
                var run = new RunDirector(new RunDirectorConfig { AllowSecondChance = false }, fuel, kills);

                run.RegisterKill();
                fuel.Modify(-200f);
                run.Tick(0.1f);
                TestRunner.True(run.InLimbo);

                run.AnswerLimbo(LimboChoice.Continue);
                TestRunner.Eq(EndingKind.HellChained, run.Ending);
            });

            TestRunner.Run("топливо на нуле но не dead не бывает", () =>
            {
                var f = new FuelSystem(new FuelConfig());
                f.Modify(-100f);
                TestRunner.True(f.IsDead);
                TestRunner.Eq(FuelState.Extinguished, f.State);
                TestRunner.Near(0f, f.Current);
            });

            TestRunner.Run("частичное оживление", () =>
            {
                var f = new FuelSystem(new FuelConfig { HungryAt = 0.4f });
                f.Modify(-100f);
                f.Revive(0.5f);
                TestRunner.Near(50f, f.Current);
                TestRunner.False(f.IsDead);
                // 50% выше порога голода 40% — состояние ещё Normal
                TestRunner.Eq(FuelState.Normal, f.State, "50% — выше порога голода");

                f.Revive(0.3f);
                TestRunner.Eq(FuelState.Hungry, f.State, "30% — ниже порога голода");
            });

            TestRunner.Run("оживление с нулевой долей = мертв", () =>
            {
                var f = new FuelSystem(new FuelConfig());
                f.Revive(0f);
                TestRunner.True(f.IsDead);
            });

            TestRunner.Run("много убийств подряд не ломают статы", () =>
            {
                var fuel = new FuelSystem(new FuelConfig { BurnPerSecond = 0.1f, KillRefuel = 5f });
                var kills = new KillTracker(new QuotaConfig { Population = 1000, RequiredFraction = 1f });
                var run = new RunDirector(new RunDirectorConfig(), fuel, kills);

                for (int i = 0; i < 500; i++)
                    run.RegisterKill();

                TestRunner.Eq(500, kills.Kills);
                TestRunner.Near(100f, fuel.Current, 0.01f, "топливо на максимуме");
            });
        }
    }
}

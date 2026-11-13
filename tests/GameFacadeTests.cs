using System;
using Calabash.Core.Endings;
using Calabash.Core.Fuel;
using Calabash.Core.Decay;
using Calabash.Core.Movement;
using Calabash.Core.Progression;
using Calabash.Unity;

namespace Calabash.Tests
{
    /// <summary>Тесты фасада CalabashGame — то, что реально будет звать Unity.</summary>
    public static class GameFacadeTests
    {
        public static void Run()
        {
            TestRunner.Section("Фасад игры (то, что зовёт Unity)");

            TestRunner.Run("забег стартует в игре", () =>
            {
                var g = new CalabashGame();
                g.StartRun(48, 0.85f);
                TestRunner.True(g.IsPlaying);
                TestRunner.False(g.IsFinished);
                TestRunner.Eq(0, g.Kills);
                TestRunner.Near(100f, g.Fuel);
            });

            TestRunner.Run("нужная квота посчитана", () =>
            {
                var g = new CalabashGame();
                g.StartRun(48, 0.85f);
                TestRunner.Eq(41, g.RequiredKills);
            });

            TestRunner.Run("убийство возвращает топливо и растит счётчик", () =>
            {
                var g = new CalabashGame();
                g.StartRun(50, 0.9f);
                g.Tick(5f);
                float before = g.Fuel;
                TestRunner.True(g.KillEnemy());
                TestRunner.Eq(1, g.Kills);
                TestRunner.Greater(g.Fuel, before, "топливо должно подрасти");
            });

            TestRunner.Run("событие убийства срабатывает", () =>
            {
                var g = new CalabashGame();
                g.StartRun();
                int reported = -1;
                g.KillRegistered += n => reported = n;
                g.KillEnemy();
                TestRunner.Eq(1, reported);
            });

            TestRunner.Run("АФК ведёт в Рай и сообщает событием", () =>
            {
                var g = new CalabashGame();
                g.StartRun(50, 0.9f);
                EndingKind reported = EndingKind.None;
                g.RunFinished += e => reported = e;

                for (int i = 0; i < 600 && !g.IsFinished; i++)
                    g.Tick(0.1f);

                TestRunner.True(g.IsFinished);
                TestRunner.Eq(EndingKind.HeavenPure, g.Ending);
                TestRunner.Eq(EndingKind.HeavenPure, reported, "событие должно дойти");
            });

            TestRunner.Run("Лимбо сообщается событием", () =>
            {
                var g = new CalabashGame();
                g.StartRun(100, 0.9f);
                g.KillEnemy();

                bool entered = false;
                g.PhaseChanged += p => { if (p == RunPhase.Limbo) entered = true; };

                for (int i = 0; i < 600 && !g.InLimbo && !g.IsFinished; i++)
                    g.Tick(0.1f);

                TestRunner.True(g.InLimbo, "должны попасть в Лимбо");
                TestRunner.True(entered, "событие фазы должно сработать");
            });

            TestRunner.Run("смена состояния топлива сообщается", () =>
            {
                var g = new CalabashGame();
                g.StartRun(100, 0.9f);
                bool hungry = false;
                g.FuelStateChanged += s => { if (s == FuelState.Hungry) hungry = true; };

                for (int i = 0; i < 100 && !g.IsFinished && !hungry; i++)
                    g.Tick(0.5f);

                TestRunner.True(hungry, "переход в голод должен сообщиться");
            });

            TestRunner.Run("стадия тления растёт с убийствами", () =>
            {
                var g = new CalabashGame();
                g.StartRun(20, 0.95f);
                TestRunner.Eq(DecayStage.Ash, g.Stage);

                for (int i = 0; i < 12; i++) g.KillEnemy();
                TestRunner.True(g.Stage > DecayStage.Ash, $"стадия должна вырасти, сейчас {g.Stage}");
            });

            TestRunner.Run("вода наносит урон", () =>
            {
                var g = new CalabashGame();
                g.StartRun();
                float before = g.Fuel;
                g.ApplyWaterDamage(30f);
                TestRunner.Near(before - 30f, g.Fuel, 0.01f);
            });

            TestRunner.Run("сопротивление воде снижает урон", () =>
            {
                // Проверяем математику напрямую, без случайной колоды:
                // тест не должен зависеть от того, что выпало.
                var g = new CalabashGame();
                g.StartRun();

                g.Stats.Add(new UpgradeCard("test_water", "Тест", Branch.Ember,
                    "", new CardEffect { WaterResistance = 0.5f }));

                TestRunner.Near(0.5f, g.Stats.WaterResistance, 0.001f);

                float before = g.Fuel;
                g.ApplyWaterDamage(30f);
                TestRunner.Near(before - 15f, g.Fuel, 0.01f, "половина урона");
            });

            TestRunner.Run("карточки выдаются по три", () =>
            {
                var g = new CalabashGame();
                g.StartRun();
                var cards = g.DrawCards();
                TestRunner.Eq(3, cards.Count);
            });

            TestRunner.Run("нельзя взять карточку не из набора", () =>
            {
                var g = new CalabashGame();
                g.StartRun();
                g.DrawCards();
                TestRunner.False(g.TakeCard("не_существует"));
            });

            TestRunner.Run("взятая карточка меняет статы", () =>
            {
                var g = new CalabashGame();
                g.StartRun();
                var cards = g.DrawCards();
                string id = null;
                foreach (var c in cards)
                    if (c.Id != CardDeck.SkipCardId) { id = c.Id; break; }

                TestRunner.True(g.TakeCard(id), "карточка из набора должна взяться");
                TestRunner.Greater(g.Stats.Cards.Count, 0f);
            });

            TestRunner.Run("способность тратит топливо", () =>
            {
                var g = new CalabashGame();
                g.StartRun();
                TestRunner.True(g.UnlockAbility(Calabash.Core.Abilities.AbilityId.Trail));
                float before = g.Fuel;
                TestRunner.True(g.UseAbility(Calabash.Core.Abilities.AbilityId.Trail));
                TestRunner.Less(g.Fuel, before, "топливо должно упасть");
            });

            TestRunner.Run("на откате способность не применяется", () =>
            {
                var g = new CalabashGame();
                g.StartRun();
                g.UnlockAbility(Calabash.Core.Abilities.AbilityId.Trail);
                g.UseAbility(Calabash.Core.Abilities.AbilityId.Trail);
                TestRunner.False(g.UseAbility(Calabash.Core.Abilities.AbilityId.Trail));
            });

            TestRunner.Run("уклонение работает через фасад", () =>
            {
                var g = new CalabashGame();
                g.StartRun();
                TestRunner.Eq(DodgeResult.Normal, g.TryDodge(0f));
            });

            TestRunner.Run("ответ Лимбо через фасад", () =>
            {
                var g = new CalabashGame();
                g.StartRun(100, 0.9f);
                g.KillEnemy();
                for (int i = 0; i < 600 && !g.InLimbo && !g.IsFinished; i++)
                    g.Tick(0.1f);

                TestRunner.True(g.InLimbo);
                TestRunner.True(g.AnswerLimbo(LimboChoice.Refuse));
                TestRunner.Eq(EndingKind.HeavenRedeemed, g.Ending);
            });

            TestRunner.Run("тик до старта не падает", () =>
            {
                var g = new CalabashGame();
                var ev = g.Tick(1f);   // StartRun не вызывали
                TestRunner.False(ev.StateChanged);
            });

            TestRunner.Run("килл до старта возвращает false", () =>
            {
                var g = new CalabashGame();
                TestRunner.False(g.KillEnemy());
            });

            TestRunner.Run("после концовки убийства не идут", () =>
            {
                var g = new CalabashGame();
                g.StartRun(50, 0.9f);
                for (int i = 0; i < 600 && !g.IsFinished; i++)
                    g.Tick(0.1f);
                TestRunner.True(g.IsFinished);
                TestRunner.False(g.KillEnemy());
            });

            TestRunner.Run("фасад не тянет UnityEngine", () =>
            {
                // Проверяем косвенно: если бы тянул, тесты бы не собрались.
                var asm = typeof(CalabashGame).Assembly;
                foreach (var r in asm.GetReferencedAssemblies())
                {
                    TestRunner.True(
                        !r.Name.StartsWith("UnityEngine"),
                        $"зависимость от {r.Name} — логика перестанет быть тестируемой");
                }
            });
        }
    }
}

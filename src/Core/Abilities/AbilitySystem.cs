using System;
using System.Collections.Generic;

namespace Calabash.Core.Abilities
{
    /// <summary>Каталог способностей по уровням (5 / 10 / 15).</summary>
    public enum AbilityId
    {
        None = 0,
        /// <summary>Ур.5: слайд оставляет горящую полосу.</summary>
        Trail = 1,
        /// <summary>Ур.5: сжечь руку, швырнуть горящие ошмётки.</summary>
        EmberToss = 2,
        /// <summary>Ур.5: короткий конус огня изо рта.</summary>
        Spit = 3,
        /// <summary>Ур.10: воткнуть руку в землю, взрыв по площади.</summary>
        Pyre = 10,
        /// <summary>Ур.10: пассивный урон вблизи.</summary>
        Coals = 11,
        /// <summary>Ур.10: дэш сквозь врага поджигает изнутри.</summary>
        BurnDash = 12,
        /// <summary>Ур.15: вырвать головню и бить ею.</summary>
        Log = 20,
        /// <summary>Ур.15: стоять, излучая жар.</summary>
        Furnace = 21,
        /// <summary>Ур.15: стать дымом и пройти сквозь.</summary>
        Smoke = 22,
    }

    public sealed class AbilityDef
    {
        public AbilityDef(AbilityId id, string title, int unlockLevel, float cooldown,
                          float fuelCost, float damage, float radius, bool isPassive = false)
        {
            Id = id;
            Title = title;
            UnlockLevel = unlockLevel;
            Cooldown = cooldown;
            FuelCost = fuelCost;
            Damage = damage;
            Radius = radius;
            IsPassive = isPassive;
        }

        public AbilityId Id { get; }
        public string Title { get; }
        public int UnlockLevel { get; }
        public float Cooldown { get; }
        public float FuelCost { get; }
        public float Damage { get; }
        public float Radius { get; }
        public bool IsPassive { get; }
    }

    /// <summary>Стандартный каталог. Один источник истины для UI и для логики.</summary>
    public static class AbilityCatalog
    {
        public static readonly IReadOnlyList<AbilityDef> All = new[]
        {
            new AbilityDef(AbilityId.Trail,     "След",     5,  4f,  6f,  18f, 1.2f),
            new AbilityDef(AbilityId.EmberToss, "Ошмётки",  5,  6f,  12f, 34f, 2.0f),
            new AbilityDef(AbilityId.Spit,      "Плевок",   5,  3f,  5f,  22f, 1.6f),
            new AbilityDef(AbilityId.Pyre,      "Кострище", 10, 9f,  20f, 60f, 3.5f),
            new AbilityDef(AbilityId.Coals,     "Угли",     10, 0f,  0f,  4f,  2.2f, isPassive: true),
            new AbilityDef(AbilityId.BurnDash,  "Рывок-ожог", 10, 7f, 15f, 45f, 1.8f),
            new AbilityDef(AbilityId.Log,       "Головня",  15, 12f, 28f, 80f, 2.4f),
            new AbilityDef(AbilityId.Furnace,   "Печь",     15, 14f, 30f, 35f, 4.0f),
            new AbilityDef(AbilityId.Smoke,     "Дым",      15, 10f, 22f, 0f,  0f),
        };

        public static AbilityDef Get(AbilityId id)
        {
            foreach (var a in All)
                if (a.Id == id)
                    return a;
            return null;
        }

        /// <summary>Способности, доступные на данном уровне персонажа.</summary>
        public static IReadOnlyList<AbilityDef> ForLevel(int level)
        {
            var list = new List<AbilityDef>();
            foreach (var a in All)
                if (a.UnlockLevel <= level && !a.IsPassive)
                    list.Add(a);
            return list;
        }
    }

    /// <summary>Слот способности: откат, готовность, применение.</summary>
    public sealed class AbilitySlot
    {
        readonly AbilityDef _def;

        public AbilitySlot(AbilityDef def)
        {
            _def = def ?? throw new ArgumentNullException(nameof(def));
            RemainingCooldown = 0f;
        }

        public AbilityDef Def => _def;
        public float RemainingCooldown { get; private set; }
        public bool IsReady => RemainingCooldown <= 0f;

        /// <summary>Доля отката 0..1 (для UI-дуги на карточке/иконке).</summary>
        public float CooldownFraction
            => _def.Cooldown <= 0f ? 0f : RemainingCooldown / _def.Cooldown;

        public void Tick(float deltaSeconds, float cooldownMultiplier = 1f)
        {
            if (deltaSeconds <= 0f)
                return;
            // cooldownMultiplier — множитель СКОРОСТИ восстановления:
            // 2.0 = откат идёт вдвое быстрее, 0.5 = вдвое медленнее.
            RemainingCooldown -= deltaSeconds * Math.Max(0.01f, cooldownMultiplier);
            if (RemainingCooldown < 0f)
                RemainingCooldown = 0f;
        }

        /// <summary>Пытается применить. Возвращает false, если не готов.</summary>
        public bool TryUse()
        {
            if (!IsReady)
                return false;
            RemainingCooldown = _def.Cooldown;
            return true;
        }

        /// <summary>Мгновенный сброс отката (для тестов).</summary>
        public void ResetCooldown() => RemainingCooldown = 0f;
    }

    /// <summary>Набор способностей героя.</summary>
    public sealed class AbilityLoadout
    {
        readonly Dictionary<AbilityId, AbilitySlot> _slots = new Dictionary<AbilityId, AbilitySlot>();

        public int Count => _slots.Count;

        public bool Unlock(AbilityId id)
        {
            if (id == AbilityId.None)
                return false;
            if (_slots.ContainsKey(id))
                return false;

            var def = AbilityCatalog.Get(id);
            if (def == null)
                return false;

            _slots[id] = new AbilitySlot(def);
            return true;
        }

        public bool Has(AbilityId id) => _slots.ContainsKey(id);

        public AbilitySlot Get(AbilityId id)
            => _slots.TryGetValue(id, out var s) ? s : null;

        public IReadOnlyCollection<AbilitySlot> Slots => _slots.Values;

        public void Tick(float deltaSeconds, float cooldownMultiplier = 1f)
        {
            foreach (var s in _slots.Values)
                s.Tick(deltaSeconds, cooldownMultiplier);
        }
    }
}

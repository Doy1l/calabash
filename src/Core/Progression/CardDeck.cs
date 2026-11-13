using System;
using System.Collections.Generic;

namespace Calabash.Core.Progression
{
    /// <summary>Ветка прокачки. В UI задаёт цвет карточки.</summary>
    public enum Branch
    {
        /// <summary>Жар: больше урона, ярче свечение, быстрее горение.</summary>
        Heat = 0,
        /// <summary>Пепел: скорость, мувмент, уклонение.</summary>
        Ash = 1,
        /// <summary>Головня: стойкость, площадь, контроль.</summary>
        Ember = 2,
    }

    /// <summary>Что именно делает карточка. Все модификаторы аддитивные.</summary>
    [Serializable]
    public sealed class CardEffect
    {
        public float DamageBonus;
        public float BurnRateMultiplier = 1f;
        public float MoveSpeedMultiplier = 1f;
        public float MaxFuelBonus;
        public float KillRefuelBonus;
        public float WaterResistance;
        public float DodgeCooldownMultiplier = 1f;
        public float AreaBonus;
        public int ExtraProjectiles;
        public bool SilencesFire;
    }

    /// <summary>Карточка способности или пассивки.</summary>
    public sealed class UpgradeCard
    {
        public UpgradeCard(string id, string title, Branch branch, string description,
                           CardEffect effect, bool isAbility = false)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("У карточки должен быть id.", nameof(id));

            Id = id;
            Title = title ?? id;
            Branch = branch;
            Description = description ?? string.Empty;
            Effect = effect ?? new CardEffect();
            IsAbility = isAbility;
        }

        public string Id { get; }
        public string Title { get; }
        public Branch Branch { get; }
        public string Description { get; }
        public CardEffect Effect { get; }
        public bool IsAbility { get; }
    }

    /// <summary>
    /// Колода забега: выдаёт по три карточки на выбор, не повторяет взятые.
    /// Ровно три всегда — если доступно меньше, добивает пустой «не брать».
    /// </summary>
    public sealed class CardDeck
    {
        /// <summary>Идентификатор карточки-отказа. Всегда доступна.</summary>
        public const string SkipCardId = "skip";

        readonly List<UpgradeCard> _pool;
        readonly List<string> _taken = new List<string>();
        readonly Random _rng;

        public CardDeck(IEnumerable<UpgradeCard> pool, int seed = 0)
        {
            _pool = new List<UpgradeCard>(pool ?? throw new ArgumentNullException(nameof(pool)));
            _rng = seed == 0 ? new Random() : new Random(seed);
        }

        public IReadOnlyList<string> TakenIds => _taken;

        /// <summary>Сколько карточек реально доступно (не считая «не брать»).</summary>
        public int AvailableCount
        {
            get
            {
                int n = 0;
                foreach (var c in _pool)
                    if (!_taken.Contains(c.Id))
                        n++;
                return n;
            }
        }

        /// <summary>
        /// Вытягивает до трёх карточек. Третьим слотом при нехватке идёт «не брать».
        /// Возвращает ровно три элемента.
        /// </summary>
        public IReadOnlyList<UpgradeCard> Draw(int count = 3)
        {
            if (count <= 0)
                throw new ArgumentOutOfRangeException(nameof(count));

            var available = new List<UpgradeCard>();
            foreach (var c in _pool)
                if (!_taken.Contains(c.Id))
                    available.Add(c);

            // Перемешиваем (Фишер-Йетс)
            for (int i = available.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (available[i], available[j]) = (available[j], available[i]);
            }

            var result = new List<UpgradeCard>(count);
            for (int i = 0; i < count && i < available.Count; i++)
                result.Add(available[i]);

            // Добиваем «не брать», чтобы слотов всегда было ровно count
            while (result.Count < count)
                result.Add(MakeSkipCard());

            return result;
        }

        /// <summary>Берёт карточку. Возвращает false, если её нельзя взять.</summary>
        public bool Take(string cardId)
        {
            if (string.IsNullOrEmpty(cardId))
                return false;

            if (cardId == SkipCardId)
                return true; // отказ всегда разрешён

            bool found = false;
            foreach (var c in _pool)
            {
                if (c.Id == cardId)
                {
                    found = true;
                    break;
                }
            }

            if (!found || _taken.Contains(cardId))
                return false;

            _taken.Add(cardId);
            return true;
        }

        public bool HasTaken(string cardId) => _taken.Contains(cardId);

        static UpgradeCard MakeSkipCard()
            => new UpgradeCard(SkipCardId, "Не брать", Branch.Ash,
                               "Оставить как есть.", new CardEffect());
    }

    /// <summary>
    /// Сводные характеристики героя. Складывает все взятые карточки.
    /// Единственное место, где модификаторы превращаются в числа.
    /// </summary>
    public sealed class PlayerStats
    {
        readonly List<UpgradeCard> _cards = new List<UpgradeCard>();

        public IReadOnlyList<UpgradeCard> Cards => _cards;

        public float DamageBonus { get; private set; }
        public float BurnRateMultiplier { get; private set; } = 1f;
        public float MoveSpeedMultiplier { get; private set; } = 1f;
        public float MaxFuelBonus { get; private set; }
        public float KillRefuelBonus { get; private set; }
        public float WaterResistance { get; private set; }
        public float DodgeCooldownMultiplier { get; private set; } = 1f;
        public float AreaBonus { get; private set; }
        public int ExtraProjectiles { get; private set; }
        public bool FireSilenced { get; private set; }

        /// <summary>Добавляет карточку и пересчитывает всё с нуля.</summary>
        public void Add(UpgradeCard card)
        {
            if (card == null) throw new ArgumentNullException(nameof(card));
            if (card.Id == CardDeck.SkipCardId) return;

            _cards.Add(card);
            Recalculate();
        }

        void Recalculate()
        {
            DamageBonus = 0f;
            BurnRateMultiplier = 1f;
            MoveSpeedMultiplier = 1f;
            MaxFuelBonus = 0f;
            KillRefuelBonus = 0f;
            WaterResistance = 0f;
            DodgeCooldownMultiplier = 1f;
            AreaBonus = 0f;
            ExtraProjectiles = 0;
            FireSilenced = false;

            foreach (var c in _cards)
            {
                var e = c.Effect;
                DamageBonus += e.DamageBonus;
                BurnRateMultiplier *= e.BurnRateMultiplier;
                MoveSpeedMultiplier *= e.MoveSpeedMultiplier;
                MaxFuelBonus += e.MaxFuelBonus;
                KillRefuelBonus += e.KillRefuelBonus;
                WaterResistance += e.WaterResistance;
                DodgeCooldownMultiplier *= e.DodgeCooldownMultiplier;
                AreaBonus += e.AreaBonus;
                ExtraProjectiles += e.ExtraProjectiles;
                if (e.SilencesFire) FireSilenced = true;
            }

            // Сопротивление воде не может превышать полную невосприимчивость
            if (WaterResistance > 1f) WaterResistance = 1f;
        }
    }
}

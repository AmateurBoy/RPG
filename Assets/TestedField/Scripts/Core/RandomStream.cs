using System.Collections.Generic;

namespace TestedField
{
    /// <summary>
    /// Детерминированный генератор — порт FRandomStream из UE (тот же LCG и те же формулы).
    /// Один seed даёт одинаковую последовательность на хосте и у клиентов, поэтому карта забега
    /// собирается локально у каждого (см. docs/generation.md).
    /// </summary>
    public sealed class RandomStream
    {
        uint seed;

        public RandomStream(int initialSeed)
        {
            seed = unchecked((uint)initialSeed);
        }

        public uint CurrentSeed => seed;

        void MutateSeed()
        {
            seed = unchecked(seed * 196314165u + 907633515u);
        }

        /// <summary>[0, 1)</summary>
        public float GetFraction()
        {
            MutateSeed();
            uint bits = 0x3F800000u | (seed >> 9);
            return System.BitConverter.Int32BitsToSingle(unchecked((int)bits)) - 1f;
        }

        /// <summary>[0, max) — как FRandomStream::RandHelper.</summary>
        public int RandHelper(int max)
        {
            if (max <= 0)
                return 0;
            int value = (int)(GetFraction() * max);
            if (value < 0) value = 0;
            if (value > max - 1) value = max - 1;
            return value;
        }

        /// <summary>[min, max] включительно — как FRandomStream::RandRange.</summary>
        public int RandRange(int min, int max)
        {
            int range = max - min + 1;
            return min + RandHelper(range);
        }

        public float FRandRange(float min, float max)
        {
            return min + (max - min) * GetFraction();
        }

        public bool Chance(float probability)
        {
            return GetFraction() < probability;
        }

        /// <summary>Индекс по весам; веса ≤ 0 не выпадают. -1, если выбрать нечего.</summary>
        public int PickWeighted(IReadOnlyList<float> weights)
        {
            float total = 0f;
            for (int i = 0; i < weights.Count; i++)
                if (weights[i] > 0f)
                    total += weights[i];
            if (total <= 0f)
                return -1;

            float roll = GetFraction() * total;
            for (int i = 0; i < weights.Count; i++)
            {
                if (weights[i] <= 0f)
                    continue;
                if (roll < weights[i])
                    return i;
                roll -= weights[i];
            }

            for (int i = weights.Count - 1; i >= 0; i--)
                if (weights[i] > 0f)
                    return i;
            return -1;
        }

        /// <summary>Перемешивание Фишера — Йетса потоком (выбор слотов без повторов).</summary>
        public void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = RandHelper(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}

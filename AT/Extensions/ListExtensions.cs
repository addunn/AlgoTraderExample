using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AT
{
    /// <summary>
    /// Helper methods for the lists.
    /// </summary>
    public static class ListExtensions
    {

        public static IEnumerable<TSource> DistinctBy<TSource, TKey>
        (this IEnumerable<TSource> source, Func<TSource, TKey> keySelector)
        {
            HashSet<TKey> seenKeys = new HashSet<TKey>();
            foreach (TSource element in source)
            {
                if (seenKeys.Add(keySelector(element)))
                {
                    yield return element;
                }
            }
        }

        public static List<T> ShiftLeft<T>(this List<T> list, int shiftBy)
        {
            if (list.Count <= shiftBy)
            {
                return list;
            }

            var result = list.GetRange(shiftBy, list.Count - shiftBy);
            result.AddRange(list.GetRange(0, shiftBy));
            return result;
        }

        public static List<T> ShiftRight<T>(this List<T> list, int shiftBy)
        {
            if (list.Count <= shiftBy)
            {
                return list;
            }

            var result = list.GetRange(list.Count - shiftBy, shiftBy);
            result.AddRange(list.GetRange(0, list.Count - shiftBy));
            return result;
        }
        public static void Shuffle<T>(this IList<T> list, int amount = 1)
        {
            Random rnd = new Random();

            for (int m = 0; m < amount; m++)
            {
                int n = list.Count;
                while (n > 1)
                {
                    n--;
                    int k = rnd.Next(n + 1);
                    T value = list[k];
                    list[k] = list[n];
                    list[n] = value;
                }
            }
        }

        public static List<List<T>> ChunkBy<T>(this List<T> source, int chunkSize)
        {
            return source
                .Select((x, i) => new { Index = i, Value = x })
                .GroupBy(x => x.Index / chunkSize)
                .Select(x => x.Select(v => v.Value).ToList())
                .ToList();
        }
    }
}

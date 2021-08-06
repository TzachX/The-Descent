using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LvlGen.Scripts.Helpers
{
    public static class ExtensionMethods
    {
        public static T PickOne<T>(this IEnumerable<T> col)
        {
            var enumerable = col as T[] ?? col.ToArray();
            //Debug.Log(enumerable.Count());
            return enumerable[RandomService.GetRandom(0, enumerable.Count())];
        }

        public static bool IsEmpty<T>(this IEnumerable<T> col) => !col.Any();
    }
}
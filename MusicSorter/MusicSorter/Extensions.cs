using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MusicSorter
{
    public static class Extensions
    {
        public static List<List<T>> ChunkBy<T>(this List<T> myList, int slices)
        {
            var list = new List<List<T>>();
            for(int i = 0; i < myList.Count(); i += slices)
            {
                list.Add(myList.GetRange(i, Math.Min(slices, myList.Count() - i)));
            }
            return list;
        }
    }
}

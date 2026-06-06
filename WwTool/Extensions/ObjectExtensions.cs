using System;
using System.Collections.Generic;
using System.Text;

namespace WwTool.Extensions
{
    public static class ObjectExtensions
    {
        public static void CopyTo<T>(this T source, T target)
        {
            foreach (var prop in typeof(T).GetProperties())
            {
                if (!prop.CanRead || !prop.CanWrite)
                    continue;

                prop.SetValue(target, prop.GetValue(source));
            }
        }
    }
}

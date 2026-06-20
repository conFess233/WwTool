using System;

namespace WwTool.Extensions
{
    public static class ObjectExtensions
    {
        public static void CopyTo<T>(this T source, T target) where T : class
        {
            if (source == null || target == null) return;

            foreach (var prop in typeof(T).GetProperties())
            {
                if (prop.CanRead && prop.CanWrite)
                {
                    try
                    {
                        prop.SetValue(target, prop.GetValue(source));
                    }
                    catch
                    {
                        // 忽略属性复制时的个别反射异常
                    }
                }
            }
        }
    }
}

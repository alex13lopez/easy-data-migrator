using System;

namespace EasyDataMigrator.Modules
{
    public static class Utilities
    {
        public static void ForEach(this string[] list, Action<string> action)
        {
            foreach (string element in list)
            {
                action.Invoke(element);
            }
        }
    }
}

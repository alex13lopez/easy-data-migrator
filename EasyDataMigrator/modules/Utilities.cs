using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

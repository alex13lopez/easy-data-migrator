using System;
using System.Text.RegularExpressions;

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
        public static bool ValidateFileName(string fileName)
        {
            // File names can only contain numbers, letters, dashes, underscore and have a maximum length of 200 characters.
            string pattern = @"^([a-zA-Z0-9\-_]){1,200}$";
            return stringIsValid(fileName, pattern);
        }

        public static bool ValidateFullPath(string fullPath)
        {
            // File paths can only contain numbers, letters, dashes, underscores, slashes, backslashes, colons and have a maximum length of 255 characters.
            string pattern = @"^([a-zA-Z0-9\-_.:\\/ ]){1,255}$";
            return stringIsValid(fullPath, pattern);
        }

        public static bool stringIsValid(string toValidate, string validationPattern) => new Regex(validationPattern).IsMatch(toValidate);
    }
}

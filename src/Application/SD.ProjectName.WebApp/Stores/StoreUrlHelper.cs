using System;

namespace SD.ProjectName.WebApp.Stores
{
    public static class StoreUrlHelper
    {
        public static string ToSlug(string storeName)
        {
            if (string.IsNullOrWhiteSpace(storeName))
            {
                return string.Empty;
            }

            Span<char> buffer = stackalloc char[storeName.Length];
            var length = 0;

            foreach (var ch in storeName.Trim())
            {
                if (char.IsLetterOrDigit(ch))
                {
                    buffer[length++] = char.ToLowerInvariant(ch);
                    continue;
                }

                if (char.IsWhiteSpace(ch) || ch == '-' || ch == '_')
                {
                    if (length == 0 || buffer[length - 1] == '-')
                    {
                        continue;
                    }

                    buffer[length++] = '-';
                }
            }

            while (length > 0 && buffer[length - 1] == '-')
            {
                length--;
            }

            return length == 0 ? string.Empty : new string(buffer[..length]);
        }
    }
}

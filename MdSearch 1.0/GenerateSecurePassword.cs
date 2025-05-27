using System;
using System.Linq;

namespace MdSearch_1._0
{
    public static class PasswordGenerator
    {
        public static string GenerateSecurePassword(int length = 12)
        {
            const string lowerChars = "abcdefghijklmnopqrstuvwxyz";
            const string upperChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            const string digitChars = "0123456789";
            const string allChars = lowerChars + upperChars + digitChars;

            var random = new Random();
            var password = new char[length];

            password[0] = upperChars[random.Next(upperChars.Length)];
            password[1] = lowerChars[random.Next(lowerChars.Length)];
            password[2] = digitChars[random.Next(digitChars.Length)];

            for (int i = 3; i < length; i++)
            {
                password[i] = allChars[random.Next(allChars.Length)];
            }

            // Перемешивание символов
            return new string(password.OrderBy(c => Guid.NewGuid()).ToArray());
        }
    }
}
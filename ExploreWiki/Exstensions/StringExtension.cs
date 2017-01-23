using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;

namespace ExploreWiki.Helpers
{
    /// <summary>
    /// String normalization state machine.
    /// </summary>
    public static class StringExtension
    {

        enum StringNormalizerState
        {
            NormalChar,
            FirstPercentage,
            FirstSpecialChar,
            SecondPercentage,
            SecondSpecialChar,
        }


        /// <summary>
        /// Normalize the input string.
        /// In wiki dump we store info in "url" like format with %20 as space, '%' escaping for unicode etc.
        /// For now normalization is done here.
        /// TODO: This should be really normalized in database itself.
        /// </summary>
        /// <param name="originalString">String to normalize.</param>
        /// <returns>Normalized string.</returns>
        internal static string NormalizeString(this string originalString)
        {
            List<char> normalizedChars = new List<char>();
            StringNormalizerState state = StringNormalizerState.NormalChar;

            List<char> specialBytes = new List<char>();
            int specialCharsCounter = 0;

            foreach (char currentChar in originalString)
            {
                switch (state)
                {
                    case StringNormalizerState.NormalChar:
                        if (currentChar == '%')
                        {
                            state = StringNormalizerState.FirstPercentage;
                        }
                        else
                        {
                            normalizedChars.Add(currentChar);
                        }
                        break;
                    case StringNormalizerState.FirstPercentage:
                        specialBytes.Add(currentChar);
                        specialCharsCounter++;

                        if (specialCharsCounter == 2)
                        {
                            state = StringNormalizerState.SecondPercentage;
                            specialCharsCounter = 0;
                        }

                        break;
                    case StringNormalizerState.SecondPercentage:
                        if (currentChar == '%')
                        {
                            state = StringNormalizerState.SecondSpecialChar;
                        }
                        else
                        {
                            // Something is strange, just return the original.
                            return originalString;
                        }
                        break;
                    case StringNormalizerState.SecondSpecialChar:
                        specialBytes.Add(currentChar);
                        specialCharsCounter++;

                        if (specialCharsCounter == 2)
                        {
                            state = StringNormalizerState.NormalChar;

                            // TODO: This is really not performant but will do for now.
                            byte byte1 = Convert.ToByte(new string(specialBytes.Take(2).ToArray()), 16);
                            byte byte2 = Convert.ToByte(new string(specialBytes.Skip(2).ToArray()), 16);

                            normalizedChars.AddRange(Encoding.UTF8.GetChars(new byte[] { byte1, byte2 }));

                            specialBytes.Clear();
                            specialCharsCounter = 0;
                        }

                        break;
                }
            }

            return (new string(normalizedChars.ToArray())).Replace("_", " ");
        }

        /// <summary>
        /// Put string into 'database' format.
        /// </summary>
        /// <param name="originalString"></param>
        /// <returns>Denormalized version of input string.</returns>
        internal static string DenormalizeString(this string originalString)
        {
            // TODO: when we do normalization in db this shouldn't be here.
            List<char> denormalizedChars = new List<char>();
            foreach (char currentChar in originalString)
            {
                if ((int)currentChar < 256)
                {
                    denormalizedChars.Add(currentChar);
                }
                else
                {
                    // break it.
                    denormalizedChars.Add('%');
                    denormalizedChars.AddRange(
                        BitConverter.ToString(Encoding.UTF8.GetBytes(new char[] { currentChar })).Replace('-', '%').ToArray()
                    );
                }
            }

            return (new string(denormalizedChars.ToArray())).Replace(" ", "_");
        }
    }
}
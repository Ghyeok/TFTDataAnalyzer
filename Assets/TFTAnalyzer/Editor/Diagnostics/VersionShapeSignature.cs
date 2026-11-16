using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace TFTAnalyzer.Editor.Diagnostics
{
    /// <summary>De-identified shape of one version field; never retains the original text.</summary>
    internal sealed class VersionShape
    {
        public string Type { get; }
        public string Signature { get; }
        public bool Truncated { get; }
        public bool HasVersionPrefix { get; }
        public string LeadingGroups { get; }

        internal VersionShape(string type, string signature, bool truncated, bool hasVersionPrefix, string leadingGroups)
        {
            Type = type; Signature = signature; Truncated = truncated; HasVersionPrefix = hasVersionPrefix; LeadingGroups = leadingGroups;
        }

        /// <summary>The combined type and signature key used for distinct-shape counting.</summary>
        public string Key => Type + "|" + Signature;
    }

    /// <summary>Converts game_version/data_version values into the approved non-identifying signature form.</summary>
    internal static class VersionShapeSignature
    {
        public const int MaxSignatureLength = 64;
        public const string NoGroups = "none";
        private const string VersionPrefix = "Version ";
        // Copy of MatchAnonymizer.NumericVersion's private contract; parity is pinned by a regression test.
        private static readonly Regex GameVersion = new Regex(
            @"\A(?:Version )?([0-9]{1,5}\.[0-9]{1,5}\.[0-9]{1,10}(?:\.[0-9]{1,10})?)(?:\s[^\r\n]*)?\z",
            RegexOptions.CultureInvariant);
        private static readonly Regex SignaturePattern = new Regex(
            @"\A(?:[a9_.()\[\]<>/:\-?]|\{x:(?:1-5|6-8|9-16|17-32|33-64|65\+)\})*\z", RegexOptions.CultureInvariant);
        private static readonly Regex GroupsPattern = new Regex(
            @"\A(?:none|(?:[0-9]{1,3}|#(?:[1-9]|[1-5][0-9]|6[0-4]|65\+))(?:\.(?:[0-9]{1,3}|#(?:[1-9]|[1-5][0-9]|6[0-4]|65\+)))?)\z",
            RegexOptions.CultureInvariant);
        private const string Punctuation = ".()[]<>/:-";

        /// <summary>Describes a JSON value: type first, then the category signature of its text (strings and integers only).</summary>
        public static VersionShape Describe(string type, string text)
        {
            switch (type)
            {
                case "string":
                case "integer":
                    break;
                case "null":
                case "missing":
                case "other":
                    return new VersionShape(type, "", false, false, NoGroups);
                default:
                    throw new ArgumentException("Unsupported version value type.", nameof(type));
            }
            text = text ?? "";
            var classes = Classify(text, out var tokens);
            var signature = Fold(classes, out var truncated);
            var prefix = type == "string" && text.Length > VersionPrefix.Length &&
                         text.StartsWith(VersionPrefix, StringComparison.Ordinal);
            return new VersionShape(type, signature, truncated, prefix, LeadingGroups(tokens));
        }

        /// <summary>Accepts only the signature alphabet and fixed length-bucket tokens.</summary>
        public static bool IsValidSignature(string value)
            => value != null && value.Length <= MaxSignatureLength && SignaturePattern.IsMatch(value);

        /// <summary>Accepts only up to two groups of 1-3 ASCII digits or digit-count markers.</summary>
        public static bool IsValidLeadingGroups(string value) => value != null && GroupsPattern.IsMatch(value);

        /// <summary>Extracts the first two numeric groups exactly as the normal admission path would.</summary>
        public static bool TryExtractRelease(string value, out string release)
        {
            release = null;
            var match = GameVersion.Match(value ?? "");
            if (!match.Success) return false;
            var parts = match.Groups[1].Value.Split('.');
            release = parts[0] + "." + parts[1];
            return true;
        }

        /// <summary>Whether a value satisfies the existing normal-path game_version regex.</summary>
        public static bool MatchesGameVersionContract(string value) => value != null && GameVersion.IsMatch(value);

        /// <summary>Maps each code point to its category character and records alphanumeric tokens.</summary>
        private static List<char> Classify(string text, out List<Token> tokens)
        {
            var classes = new List<char>(text.Length);
            tokens = new List<Token>();
            Token current = null;
            for (var i = 0; i < text.Length; i++)
            {
                char mapped;
                var asciiDigit = false;
                if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    mapped = Map(CharUnicodeInfo.GetUnicodeCategory(text, i), text[i]);
                    i++;
                }
                else
                {
                    mapped = Map(CharUnicodeInfo.GetUnicodeCategory(text[i]), text[i]);
                    asciiDigit = text[i] >= '0' && text[i] <= '9';
                }
                classes.Add(mapped);
                if (mapped == 'a' || mapped == '9')
                {
                    if (current == null) tokens.Add(current = new Token());
                    current.Length++;
                    if (mapped == 'a') current.HasLetter = true;
                    if (!asciiDigit) current.NonAscii = true;
                    // Raw digits are buffered only while short enough to be a release candidate.
                    if (asciiDigit && current.Digits.Length < 4) current.Digits.Append(text[i]);
                }
                else current = null;
            }
            return classes;
        }

        /// <summary>Uses Unicode categories so non-ASCII letters, digits and symbols never survive as text.</summary>
        private static char Map(UnicodeCategory category, char value)
        {
            switch (category)
            {
                case UnicodeCategory.UppercaseLetter:
                case UnicodeCategory.LowercaseLetter:
                case UnicodeCategory.TitlecaseLetter:
                case UnicodeCategory.ModifierLetter:
                case UnicodeCategory.OtherLetter:
                    return 'a';
                case UnicodeCategory.DecimalDigitNumber:
                case UnicodeCategory.LetterNumber:
                case UnicodeCategory.OtherNumber:
                    return '9';
                case UnicodeCategory.SpaceSeparator:
                case UnicodeCategory.LineSeparator:
                case UnicodeCategory.ParagraphSeparator:
                    return '_';
            }
            if (char.IsWhiteSpace(value)) return '_';
            return Punctuation.IndexOf(value) >= 0 ? value : '?';
        }

        /// <summary>Folds hyphen-joined alphanumeric chains and long alphanumeric runs into length buckets.</summary>
        private static string Fold(List<char> classes, out bool truncated)
        {
            var pieces = new List<string>();
            for (var i = 0; i < classes.Count;)
            {
                if (!IsAlphanumeric(classes[i]))
                {
                    pieces.Add(classes[i].ToString());
                    i++;
                    continue;
                }
                var runEnd = RunEnd(classes, i);
                var end = runEnd;
                var runs = 1;
                while (end + 1 < classes.Count && classes[end] == '-' && IsAlphanumeric(classes[end + 1]))
                {
                    end = RunEnd(classes, end + 1);
                    runs++;
                }
                if (runs >= 2) pieces.Add(Bucket(end - i));
                else
                {
                    end = runEnd;
                    var length = end - i;
                    if (length >= 6) pieces.Add(Bucket(length));
                    else
                        for (var k = i; k < end; k++) pieces.Add(classes[k].ToString());
                }
                i = end;
            }
            var builder = new StringBuilder();
            truncated = false;
            foreach (var piece in pieces)
            {
                // Truncate at a token boundary so a partial length bucket can never appear.
                if (builder.Length + piece.Length > MaxSignatureLength)
                {
                    truncated = true;
                    break;
                }
                builder.Append(piece);
            }
            return builder.ToString();
        }

        private static int RunEnd(List<char> classes, int start)
        {
            var end = start;
            while (end < classes.Count && IsAlphanumeric(classes[end])) end++;
            return end;
        }

        private static bool IsAlphanumeric(char value) => value == 'a' || value == '9';

        private static string Bucket(int length)
        {
            if (length <= 5) return "{x:1-5}";
            if (length <= 8) return "{x:6-8}";
            if (length <= 16) return "{x:9-16}";
            if (length <= 32) return "{x:17-32}";
            if (length <= 64) return "{x:33-64}";
            return "{x:65+}";
        }

        /// <summary>Keeps raw values only for the first two all-digit tokens of 1-3 ASCII digits; others become digit counts.</summary>
        private static string LeadingGroups(List<Token> tokens)
        {
            var groups = new List<string>(2);
            foreach (var token in tokens)
            {
                if (token.HasLetter) continue;
                groups.Add(!token.NonAscii && token.Length <= 3
                    ? token.Digits.ToString()
                    : "#" + (token.Length > 64 ? "65+" : token.Length.ToString(CultureInfo.InvariantCulture)));
                if (groups.Count == 2) break;
            }
            foreach (var token in tokens) token.Digits.Clear();
            return groups.Count == 0 ? NoGroups : string.Join(".", groups);
        }

        private sealed class Token
        {
            public int Length;
            public bool HasLetter;
            public bool NonAscii;
            public readonly StringBuilder Digits = new StringBuilder(4);
        }
    }
}

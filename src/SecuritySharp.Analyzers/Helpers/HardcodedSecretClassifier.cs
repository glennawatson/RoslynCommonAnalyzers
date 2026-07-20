// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>
/// Classifies the decoded content of a string literal against a curated, high-precision set of credential
/// shapes for SES1201. The matcher is purely syntactic (no semantic model, no framework probing, no regular
/// expressions): a length screen and a first-character dispatch keep the clean path allocation-free, and a
/// hand-rolled prefix plus character-class scan confirms each shape only after the cheap screen passes. It
/// rejects placeholder shapes -- angle-bracket templates, repeated fill characters, low-entropy bodies, and
/// spelled-out stand-ins -- so a returned kind is almost always a real secret rather than a documentation
/// sample. Every recognised shape maps to a short kind label used as the diagnostic's message argument.
/// </summary>
internal static class HardcodedSecretClassifier
{
    /// <summary>The kind label for an OpenAI-style secret key.</summary>
    internal const string OpenAiApiKey = "OpenAI API key";

    /// <summary>The kind label for an AWS access key id.</summary>
    internal const string AwsAccessKeyId = "AWS access key id";

    /// <summary>The kind label for a GitHub token.</summary>
    internal const string GitHubToken = "GitHub token";

    /// <summary>The kind label for a Slack token.</summary>
    internal const string SlackToken = "Slack token";

    /// <summary>The kind label for a Google API key.</summary>
    internal const string GoogleApiKey = "Google API key";

    /// <summary>The kind label for a PEM private-key block.</summary>
    internal const string PrivateKey = "private key";

    /// <summary>The kind label for an Azure shared-access/account key.</summary>
    internal const string AzureAccessKey = "Azure access key";

    /// <summary>The kind label for a connection-string password.</summary>
    internal const string ConnectionStringPassword = "connection-string password";

    /// <summary>Character class: an ASCII digit.</summary>
    private const int Digit = 1;

    /// <summary>Character class: an upper-case ASCII letter.</summary>
    private const int Upper = 2;

    /// <summary>Character class: a lower-case ASCII letter.</summary>
    private const int Lower = 4;

    /// <summary>Character class: the <c>-</c> separator.</summary>
    private const int Dash = 8;

    /// <summary>Character class: the <c>_</c> separator.</summary>
    private const int Underscore = 16;

    /// <summary>Character class: the base64 <c>+</c> character.</summary>
    private const int PlusSign = 32;

    /// <summary>Character class: the base64 <c>/</c> character.</summary>
    private const int Slash = 64;

    /// <summary>Character class: the base64 <c>=</c> padding character.</summary>
    private const int EqualsSign = 128;

    /// <summary>The base62 mask (digits and ASCII letters).</summary>
    private const int Base62Mask = Digit | Upper | Lower;

    /// <summary>The mask for digits and upper-case ASCII letters.</summary>
    private const int UpperAlphanumericMask = Digit | Upper;

    /// <summary>The url-safe mask (base62 plus <c>-</c> and <c>_</c>).</summary>
    private const int UrlSafeMask = Base62Mask | Dash | Underscore;

    /// <summary>The Slack body mask (base62 plus <c>-</c>).</summary>
    private const int SlackBodyMask = Base62Mask | Dash;

    /// <summary>The base64 mask (base62 plus the base64 and url-safe punctuation).</summary>
    private const int Base64Mask = Base62Mask | Dash | Underscore | PlusSign | Slash | EqualsSign;

    /// <summary>The number of ASCII code points the character-class table covers.</summary>
    private const int AsciiRange = 128;

    /// <summary>The size of the low half of the ASCII range, used to split the entropy bitset.</summary>
    private const int AsciiLowBlockSize = 64;

    /// <summary>The shortest literal any shape can match, used as the first cheap screen.</summary>
    private const int MinCandidateLength = 15;

    /// <summary>The minimum distinct characters a keyed body needs before it is trusted as real, not a placeholder.</summary>
    private const int MinDistinctBodyCharacters = 5;

    /// <summary>The longest run of one repeated character allowed in a keyed body before it reads as a placeholder.</summary>
    private const int MaxIdenticalRun = 5;

    /// <summary>The index at which an OpenAI key body follows its <c>sk-</c> prefix.</summary>
    private const int OpenAiBodyStart = 3;

    /// <summary>The minimum length of an OpenAI key body.</summary>
    private const int OpenAiMinBodyLength = 20;

    /// <summary>The index at which an AWS/Google/GitHub key body follows its four-character prefix.</summary>
    private const int FourCharPrefixBodyStart = 4;

    /// <summary>The length of the trailing portion of an AWS access key id.</summary>
    private const int AwsKeyIdLength = 16;

    /// <summary>The length of the trailing portion of a Google API key.</summary>
    private const int GoogleKeyLength = 35;

    /// <summary>The length of the trailing portion of a GitHub token.</summary>
    private const int GitHubTokenLength = 36;

    /// <summary>The index at which a Slack token body follows its five-character prefix.</summary>
    private const int SlackBodyStart = 5;

    /// <summary>The minimum length of a Slack token body.</summary>
    private const int SlackMinBodyLength = 10;

    /// <summary>The minimum length of an Azure key body.</summary>
    private const int AzureMinKeyLength = 20;

    /// <summary>The minimum length of a connection-string password value.</summary>
    private const int MinPasswordLength = 4;

    /// <summary>The GitHub token prefixes.</summary>
    private static readonly string[] GitHubPrefixes = ["ghp_", "gho_", "ghu_", "ghs_", "ghr_"];

    /// <summary>The Slack token prefixes (type letter plus separator).</summary>
    private static readonly string[] SlackPrefixes = ["xoxb-", "xoxa-", "xoxp-", "xoxr-", "xoxs-"];

    /// <summary>The spelled-out placeholder password values that are never treated as real secrets.</summary>
    private static readonly string[] PlaceholderPasswords =
        ["password", "changeme", "placeholder", "yourpassword", "your_password", "mypassword", "example", "secret"];

    /// <summary>The per-ASCII-character class flags, indexed by code point.</summary>
    private static readonly int[] CharacterClassTable = BuildCharacterClassTable();

    /// <summary>Returns the credential kind a string literal's content matches, or <see langword="null"/> when it is not a recognised secret.</summary>
    /// <param name="value">The decoded literal content.</param>
    /// <returns>A kind label from this class, or <see langword="null"/> when the content is not a recognised secret.</returns>
    internal static string? Classify(string value)
    {
        if (value.Length < MinCandidateLength || IsAngleBracketTemplate(value))
        {
            return null;
        }

        return ClassifyPrefixed(value) ?? ClassifyEmbedded(value);
    }

    /// <summary>Matches the fixed-prefix families by dispatching on the first character.</summary>
    /// <param name="value">The decoded literal content.</param>
    /// <returns>The matched kind label, or <see langword="null"/>.</returns>
    private static string? ClassifyPrefixed(string value)
        => value[0] switch
        {
            's' => IsOpenAiKey(value) ? OpenAiApiKey : null,
            'A' => ClassifyAwsOrGoogle(value),
            'g' => IsGitHubToken(value) ? GitHubToken : null,
            'x' => IsSlackToken(value) ? SlackToken : null,
            _ => null,
        };

    /// <summary>Disambiguates the two families whose keys begin with <c>A</c>.</summary>
    /// <param name="value">The decoded literal content.</param>
    /// <returns>The matched kind label, or <see langword="null"/>.</returns>
    private static string? ClassifyAwsOrGoogle(string value)
    {
        if (IsAwsAccessKeyId(value))
        {
            return AwsAccessKeyId;
        }

        return IsGoogleApiKey(value) ? GoogleApiKey : null;
    }

    /// <summary>Matches the families that may appear anywhere in the literal (PEM blocks and connection strings).</summary>
    /// <param name="value">The decoded literal content.</param>
    /// <returns>The matched kind label, or <see langword="null"/>.</returns>
    private static string? ClassifyEmbedded(string value)
    {
        // A PEM block may be preceded by leading whitespace, so it is matched independent of the first character.
        if (value.IndexOf('-') >= 0 && IsPemPrivateKey(value))
        {
            return PrivateKey;
        }

        // The connection-string families all rely on a '=' assignment; without one there is nothing to match.
        if (value.IndexOf('=') < 0)
        {
            return null;
        }

        if (IsAzureAccessKey(value))
        {
            return AzureAccessKey;
        }

        return IsConnectionStringPassword(value) ? ConnectionStringPassword : null;
    }

    /// <summary>Returns whether the content is an angle-bracket template such as <c>&lt;your-key&gt;</c>.</summary>
    /// <param name="value">The decoded literal content.</param>
    /// <returns><see langword="true"/> when the content is a template.</returns>
    private static bool IsAngleBracketTemplate(string value)
        => value.IndexOf('<') >= 0 && value.IndexOf('>') >= 0;

    /// <summary>Returns whether the content is an OpenAI-style <c>sk-</c> key of at least 20 base62 characters.</summary>
    /// <param name="value">The decoded literal content.</param>
    /// <returns><see langword="true"/> when the content matches.</returns>
    private static bool IsOpenAiKey(string value)
    {
        if (!HasPrefix(value, "sk-"))
        {
            return false;
        }

        var run = CountClassRun(value, OpenAiBodyStart, Base62Mask);
        return run >= OpenAiMinBodyLength && IsHighEntropyBody(value, OpenAiBodyStart, OpenAiBodyStart + run);
    }

    /// <summary>Returns whether the content is an <c>AKIA</c> AWS access key id (16 trailing upper-case alphanumerics).</summary>
    /// <param name="value">The decoded literal content.</param>
    /// <returns><see langword="true"/> when the content matches.</returns>
    private static bool IsAwsAccessKeyId(string value)
    {
        if (!HasPrefix(value, "AKIA"))
        {
            return false;
        }

        var run = CountClassRun(value, FourCharPrefixBodyStart, UpperAlphanumericMask);
        return run >= AwsKeyIdLength && IsHighEntropyBody(value, FourCharPrefixBodyStart, FourCharPrefixBodyStart + run);
    }

    /// <summary>Returns whether the content is an <c>AIza</c> Google API key (35 trailing url-safe characters).</summary>
    /// <param name="value">The decoded literal content.</param>
    /// <returns><see langword="true"/> when the content matches.</returns>
    private static bool IsGoogleApiKey(string value)
    {
        if (!HasPrefix(value, "AIza"))
        {
            return false;
        }

        var run = CountClassRun(value, FourCharPrefixBodyStart, UrlSafeMask);
        return run >= GoogleKeyLength && IsHighEntropyBody(value, FourCharPrefixBodyStart, FourCharPrefixBodyStart + run);
    }

    /// <summary>Returns whether the content is a GitHub token (<c>ghp_</c>/<c>gho_</c>/<c>ghu_</c>/<c>ghs_</c>/<c>ghr_</c> + 36 base62).</summary>
    /// <param name="value">The decoded literal content.</param>
    /// <returns><see langword="true"/> when the content matches.</returns>
    private static bool IsGitHubToken(string value)
    {
        if (!HasAnyPrefix(value, GitHubPrefixes))
        {
            return false;
        }

        var run = CountClassRun(value, FourCharPrefixBodyStart, Base62Mask);
        return run >= GitHubTokenLength && IsHighEntropyBody(value, FourCharPrefixBodyStart, FourCharPrefixBodyStart + run);
    }

    /// <summary>Returns whether the content is a Slack token (<c>xox</c> + type + <c>-</c> + a token body).</summary>
    /// <param name="value">The decoded literal content.</param>
    /// <returns><see langword="true"/> when the content matches.</returns>
    private static bool IsSlackToken(string value)
    {
        if (!HasAnyPrefix(value, SlackPrefixes))
        {
            return false;
        }

        var run = CountClassRun(value, SlackBodyStart, SlackBodyMask);
        return run >= SlackMinBodyLength && IsHighEntropyBody(value, SlackBodyStart, SlackBodyStart + run);
    }

    /// <summary>Returns whether the content contains a PEM private-key header/footer pair.</summary>
    /// <param name="value">The decoded literal content.</param>
    /// <returns><see langword="true"/> when the content matches.</returns>
    private static bool IsPemPrivateKey(string value)
    {
        var begin = value.IndexOf("-----BEGIN", StringComparison.Ordinal);
        if (begin < 0)
        {
            return false;
        }

        return value.IndexOf("PRIVATE KEY-----", StringComparison.Ordinal) > begin;
    }

    /// <summary>Returns whether the content holds an Azure <c>SharedAccessKey=</c> or <c>AccountKey=</c> base64 body.</summary>
    /// <param name="value">The decoded literal content.</param>
    /// <returns><see langword="true"/> when the content matches.</returns>
    private static bool IsAzureAccessKey(string value)
        => HasBase64Assignment(value, "SharedAccessKey=") || HasBase64Assignment(value, "AccountKey=");

    /// <summary>Returns whether an assignment marker is followed by a base64-shaped body of at least 20 characters.</summary>
    /// <param name="value">The decoded literal content.</param>
    /// <param name="marker">The <c>Key=</c> marker to locate.</param>
    /// <returns><see langword="true"/> when the marker is followed by a high-entropy base64 body.</returns>
    private static bool HasBase64Assignment(string value, string marker)
    {
        var marked = value.IndexOf(marker, StringComparison.Ordinal);
        if (marked < 0)
        {
            return false;
        }

        var start = marked + marker.Length;
        var run = CountClassRun(value, start, Base64Mask);
        return run >= AzureMinKeyLength && IsHighEntropyBody(value, start, start + run);
    }

    /// <summary>Returns whether the content is a connection string that carries a non-placeholder password value.</summary>
    /// <param name="value">The decoded literal content.</param>
    /// <returns><see langword="true"/> when the content matches.</returns>
    private static bool IsConnectionStringPassword(string value)
    {
        if (!HasConnectionStringMarker(value))
        {
            return false;
        }

        var marker = "Password=";
        var marked = value.IndexOf(marker, StringComparison.Ordinal);
        if (marked < 0)
        {
            marker = "Pwd=";
            marked = value.IndexOf(marker, StringComparison.Ordinal);
        }

        if (marked < 0)
        {
            return false;
        }

        var start = marked + marker.Length;
        var end = start;
        while (end < value.Length && value[end] != ';')
        {
            end++;
        }

        return end - start >= MinPasswordLength && !IsPlaceholderPassword(value, start, end);
    }

    /// <summary>Returns whether the content carries a connection-string endpoint marker.</summary>
    /// <param name="value">The decoded literal content.</param>
    /// <returns><see langword="true"/> when a recognised endpoint marker is present.</returns>
    private static bool HasConnectionStringMarker(string value)
        => value.IndexOf("Server=", StringComparison.Ordinal) >= 0
            || value.IndexOf("Data Source=", StringComparison.Ordinal) >= 0
            || value.IndexOf("Initial Catalog=", StringComparison.Ordinal) >= 0
            || value.IndexOf("Host=", StringComparison.Ordinal) >= 0;

    /// <summary>Returns whether a password span is a placeholder rather than a real secret.</summary>
    /// <param name="value">The decoded literal content.</param>
    /// <param name="start">The inclusive start of the password span.</param>
    /// <param name="end">The exclusive end of the password span.</param>
    /// <returns><see langword="true"/> when the span reads as a placeholder.</returns>
    private static bool IsPlaceholderPassword(string value, int start, int end)
        => IsSingleRepeatedCharacter(value, start, end) || IsPlaceholderWord(value, start, end);

    /// <summary>Returns whether a span is one character repeated for its whole length.</summary>
    /// <param name="value">The decoded literal content.</param>
    /// <param name="start">The inclusive start of the span.</param>
    /// <param name="end">The exclusive end of the span.</param>
    /// <returns><see langword="true"/> when every character in the span is identical.</returns>
    private static bool IsSingleRepeatedCharacter(string value, int start, int end)
    {
        for (var i = start + 1; i < end; i++)
        {
            if (value[i] != value[start])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns whether a span equals one of the spelled-out placeholder password words.</summary>
    /// <param name="value">The decoded literal content.</param>
    /// <param name="start">The inclusive start of the span.</param>
    /// <param name="end">The exclusive end of the span.</param>
    /// <returns><see langword="true"/> when the span is a placeholder word.</returns>
    private static bool IsPlaceholderWord(string value, int start, int end)
    {
        for (var i = 0; i < PlaceholderPasswords.Length; i++)
        {
            if (RegionEqualsIgnoreCase(value, start, end, PlaceholderPasswords[i]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a keyed body has enough distinct characters and no long identical run to be a real secret.</summary>
    /// <param name="value">The decoded literal content.</param>
    /// <param name="start">The inclusive start of the body span.</param>
    /// <param name="end">The exclusive end of the body span.</param>
    /// <returns><see langword="true"/> when the body reads as a high-entropy secret rather than a placeholder.</returns>
    private static bool IsHighEntropyBody(string value, int start, int end)
        => !HasLongIdenticalRun(value, start, end) && CountDistinctAsciiCharacters(value, start, end) >= MinDistinctBodyCharacters;

    /// <summary>Returns whether a span contains a run of one repeated character longer than the allowed maximum.</summary>
    /// <param name="value">The decoded literal content.</param>
    /// <param name="start">The inclusive start of the span.</param>
    /// <param name="end">The exclusive end of the span.</param>
    /// <returns><see langword="true"/> when a run exceeds <see cref="MaxIdenticalRun"/>.</returns>
    private static bool HasLongIdenticalRun(string value, int start, int end)
    {
        var runLength = 1;
        for (var i = start + 1; i < end; i++)
        {
            if (value[i] == value[i - 1])
            {
                runLength++;
                if (runLength > MaxIdenticalRun)
                {
                    return true;
                }
            }
            else
            {
                runLength = 1;
            }
        }

        return false;
    }

    /// <summary>Counts the distinct characters in a span. Every character in a keyed body is an in-class ASCII code point.</summary>
    /// <param name="value">The decoded literal content.</param>
    /// <param name="start">The inclusive start of the span.</param>
    /// <param name="end">The exclusive end of the span.</param>
    /// <returns>The number of distinct characters.</returns>
    private static int CountDistinctAsciiCharacters(string value, int start, int end)
    {
        ulong low = 0;
        ulong high = 0;
        var distinct = 0;
        for (var i = start; i < end; i++)
        {
            int c = value[i];
            if (c < AsciiLowBlockSize)
            {
                var bit = 1UL << c;
                if ((low & bit) == 0)
                {
                    low |= bit;
                    distinct++;
                }
            }
            else
            {
                var bit = 1UL << (c - AsciiLowBlockSize);
                if ((high & bit) == 0)
                {
                    high |= bit;
                    distinct++;
                }
            }
        }

        return distinct;
    }

    /// <summary>Returns whether a span equals a lower-case placeholder word, comparing ASCII case-insensitively.</summary>
    /// <param name="value">The decoded literal content.</param>
    /// <param name="start">The inclusive start of the span.</param>
    /// <param name="end">The exclusive end of the span.</param>
    /// <param name="word">The lower-case word to compare against.</param>
    /// <returns><see langword="true"/> when the span equals the word.</returns>
    private static bool RegionEqualsIgnoreCase(string value, int start, int end, string word)
    {
        if (end - start != word.Length)
        {
            return false;
        }

        for (var i = 0; i < word.Length; i++)
        {
            if (ToLowerAscii(value[start + i]) != word[i])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns whether the value starts with any of the supplied prefixes, comparing ordinally.</summary>
    /// <param name="value">The decoded literal content.</param>
    /// <param name="prefixes">The prefixes to test.</param>
    /// <returns><see langword="true"/> when the value starts with one of the prefixes.</returns>
    private static bool HasAnyPrefix(string value, string[] prefixes)
    {
        for (var i = 0; i < prefixes.Length; i++)
        {
            if (HasPrefix(value, prefixes[i]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether the value starts with a prefix, comparing ordinally.</summary>
    /// <param name="value">The decoded literal content. Callers pass a value at least <see cref="MinCandidateLength"/> long, which exceeds every prefix.</param>
    /// <param name="prefix">The prefix to test.</param>
    /// <returns><see langword="true"/> when the value starts with the prefix.</returns>
    private static bool HasPrefix(string value, string prefix)
    {
        for (var i = 0; i < prefix.Length; i++)
        {
            if (value[i] != prefix[i])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Counts the run of characters in one class starting at an index.</summary>
    /// <param name="value">The decoded literal content.</param>
    /// <param name="start">The index to start counting from.</param>
    /// <param name="classMask">The class mask the run must stay within.</param>
    /// <returns>The number of consecutive in-class characters.</returns>
    private static int CountClassRun(string value, int start, int classMask)
    {
        var i = start;
        while (i < value.Length && IsInClass(value[i], classMask))
        {
            i++;
        }

        return i - start;
    }

    /// <summary>Returns whether a character belongs to the class described by a mask.</summary>
    /// <param name="c">The character to test.</param>
    /// <param name="classMask">The class mask to test against.</param>
    /// <returns><see langword="true"/> when the character is in the class.</returns>
    private static bool IsInClass(char c, int classMask)
        => c < AsciiRange && (CharacterClassTable[c] & classMask) != 0;

    /// <summary>Lower-cases an ASCII letter, leaving every other character untouched.</summary>
    /// <param name="c">The character to fold.</param>
    /// <returns>The lower-cased character.</returns>
    private static char ToLowerAscii(char c)
        => c is >= 'A' and <= 'Z' ? (char)(c + ('a' - 'A')) : c;

    /// <summary>Builds the per-character class-flag table for the ASCII range.</summary>
    /// <returns>The table, indexed by code point.</returns>
    private static int[] BuildCharacterClassTable()
    {
        var table = new int[AsciiRange];
        for (var c = '0'; c <= '9'; c++)
        {
            table[c] = Digit;
        }

        for (var c = 'A'; c <= 'Z'; c++)
        {
            table[c] = Upper;
        }

        for (var c = 'a'; c <= 'z'; c++)
        {
            table[c] = Lower;
        }

        table['-'] = Dash;
        table['_'] = Underscore;
        table['+'] = PlusSign;
        table['/'] = Slash;
        table['='] = EqualsSign;
        return table;
    }
}

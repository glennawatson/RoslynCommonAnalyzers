// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>Evaluates permissive sources within their effective CSP directives.</summary>
internal static class ContentSecurityPolicyParser
{
    /// <summary>The opening and closing quotes around a nonce or hash source.</summary>
    private const int QuoteCount = 2;

    /// <summary>The maximum padding accepted by CSP's base64-value grammar.</summary>
    private const int MaximumPadding = 2;

    /// <summary>Independent permissions intersected across a comma-separated policy list.</summary>
    internal enum Permission
    {
        /// <summary>Inline script elements.</summary>
        ScriptInline = 0,
        /// <summary>Script loading from arbitrary network hosts.</summary>
        ScriptHosts = 1,
        /// <summary>Inline script attributes.</summary>
        ScriptAttributes = 2,
        /// <summary>JavaScript string evaluation.</summary>
        ScriptEval = 3,
        /// <summary>Inline style elements.</summary>
        StyleInline = 4,
        /// <summary>Stylesheet loading from arbitrary network hosts.</summary>
        StyleHosts = 5,
        /// <summary>Inline style attributes.</summary>
        StyleAttributes = 6,
        /// <summary>Embedded object loading from arbitrary network hosts.</summary>
        ObjectHosts = 7,
        /// <summary>Base URLs from arbitrary origins.</summary>
        BaseHosts = 8,
        /// <summary>The number of independent permissions.</summary>
        Count = 9,
    }

    /// <summary>The source lists whose effective permissions are checked.</summary>
    private enum Directive
    {
        /// <summary>An unrecognized directive.</summary>
        Unknown = -1,
        /// <summary>The fallback for unspecified fetch directives.</summary>
        Default = 0,
        /// <summary>Script loading and execution.</summary>
        Script = 1,
        /// <summary>Stylesheet loading and inline styles.</summary>
        Style = 2,
        /// <summary>Embedded objects.</summary>
        Object = 3,
        /// <summary>Document base URLs.</summary>
        Base = 4,
        /// <summary>Script elements.</summary>
        ScriptElement = 5,
        /// <summary>Inline script attributes.</summary>
        ScriptAttribute = 6,
        /// <summary>Style elements and stylesheets.</summary>
        StyleElement = 7,
        /// <summary>Inline style attributes.</summary>
        StyleAttribute = 8,
        /// <summary>The number of tracked directives.</summary>
        Count = 9,
    }

    /// <summary>Source-list facts needed to evaluate effective permissions.</summary>
    [Flags]
    private enum Sources
    {
        /// <summary>No recognized sources.</summary>
        None = 0,
        /// <summary>The directive exists, including an empty source list.</summary>
        Present = 1 << 0,
        /// <summary>The unsafe-inline keyword.</summary>
        Inline = 1 << 1,
        /// <summary>The unsafe-eval keyword.</summary>
        Eval = 1 << 2,
        /// <summary>A standalone wildcard source.</summary>
        Wildcard = 1 << 3,
        /// <summary>A syntactically valid nonce or hash source.</summary>
        NonceOrHash = 1 << 4,
        /// <summary>The strict-dynamic keyword.</summary>
        StrictDynamic = 1 << 5,
    }

    /// <summary>Checks whether the first directive identifies a standalone policy literal.</summary>
    /// <param name="text">The decoded literal.</param>
    /// <returns>Whether the first token is a tracked directive name.</returns>
    internal static bool BeginsWithDirective(string text)
    {
        var remaining = text.AsSpan();
        var separator = remaining.IndexOf(';');
        if (separator >= 0)
        {
            remaining = remaining.Slice(0, separator);
        }

        return GetDirective(ReadToken(ref remaining)) != Directive.Unknown;
    }

    /// <summary>Finds a permissive effective restriction in a serialized policy.</summary>
    /// <param name="text">The decoded policy literal.</param>
    /// <returns>The first reportable permission, or null for a clean policy.</returns>
    internal static ContentSecurityPolicyViolation? FindViolation(string text)
    {
        if (text.IndexOf(',') >= 0)
        {
            return FindPolicyListViolation(text.AsSpan());
        }

        Span<Sources> policy = stackalloc Sources[(int)Directive.Count];
        ReadPolicy(text.AsSpan(), policy);
        var objects = EffectiveDirective(policy, Directive.Object);
        return FindScriptViolation(policy)
            ?? FindStyleViolation(policy)
            ?? WildcardViolation(policy[(int)objects], objects, Permission.ObjectHosts)
            ?? WildcardViolation(policy[(int)Directive.Base], Directive.Base, Permission.BaseHosts);
    }

    /// <summary>Selects complete diagnostic wording without allocating an intermediate string.</summary>
    /// <param name="permission">The capability to describe.</param>
    /// <param name="reportOnly">Whether the policy is unenforced.</param>
    /// <returns>The diagnostic's permission phrase.</returns>
    internal static string DescribePermission(Permission permission, bool reportOnly) =>
        reportOnly ? ReportOnlyDescription(permission) : EnforcedDescription(permission);

    /// <summary>Combines restrictions from policies that the browser enforces together.</summary>
    /// <param name="text">The serialized policy list.</param>
    /// <returns>A permission left unrestricted by every policy, or null.</returns>
    private static ContentSecurityPolicyViolation? FindPolicyListViolation(ReadOnlySpan<char> text)
    {
        Span<Sources> policy = stackalloc Sources[(int)Directive.Count];
        Span<Directive> origins = stackalloc Directive[(int)Permission.Count];
        var restricted = 0;
        var explicitPermissions = 0;
        while (!text.IsEmpty)
        {
            var separator = text.IndexOf(',');
            var part = separator < 0 ? text : text.Slice(0, separator);
            text = separator < 0 ? default : text.Slice(separator + 1);
            ReadPolicy(part, policy);
            IntersectPermissions(policy, origins, ref restricted, ref explicitPermissions);
            if (restricted == (1 << (int)Permission.Count) - 1)
            {
                return null;
            }
        }

        var candidates = explicitPermissions & ~restricted;
        for (var index = 0; index < (int)Permission.Count; index++)
        {
            if ((candidates & (1 << index)) != 0)
            {
                return new(Name(origins[index]), (Permission)index);
            }
        }

        return null;
    }

    /// <summary>Records restrictions and explicit permissions without treating an omitted directive as a warning.</summary>
    /// <param name="policy">One parsed policy.</param>
    /// <param name="origins">The directive supplying each explicit permission.</param>
    /// <param name="restricted">Permissions blocked by any policy.</param>
    /// <param name="explicitPermissions">Permissions explicitly allowed by a policy.</param>
    private static void IntersectPermissions(ReadOnlySpan<Sources> policy, Span<Directive> origins, ref int restricted, ref int explicitPermissions)
    {
        for (var index = 0; index < (int)Permission.Count; index++)
        {
            var permission = (Permission)index;
            var directive = PermissionDirective(policy, permission);
            var sources = policy[(int)directive];
            if ((sources & Sources.Present) == 0)
            {
                continue;
            }

            if (AllowsPermission(sources, permission))
            {
                explicitPermissions |= 1 << index;
                origins[index] = directive;
            }
            else
            {
                restricted |= 1 << index;
            }
        }
    }

    /// <summary>Locates the effective source list for one independently controlled permission.</summary>
    /// <param name="policy">The parsed source lists.</param>
    /// <param name="permission">The capability to evaluate.</param>
    /// <returns>The directive that governs the capability.</returns>
    private static Directive PermissionDirective(ReadOnlySpan<Sources> policy, Permission permission)
    {
        var script = EffectiveDirective(policy, Directive.Script);
        var style = EffectiveDirective(policy, Directive.Style);
        return permission switch
        {
            Permission.ScriptInline or Permission.ScriptHosts => EffectiveDirective(policy, Directive.ScriptElement, script),
            Permission.ScriptAttributes => EffectiveDirective(policy, Directive.ScriptAttribute, script),
            Permission.ScriptEval => script,
            Permission.StyleInline or Permission.StyleHosts => EffectiveDirective(policy, Directive.StyleElement, style),
            Permission.StyleAttributes => EffectiveDirective(policy, Directive.StyleAttribute, style),
            Permission.ObjectHosts => EffectiveDirective(policy, Directive.Object),
            _ => Directive.Base,
        };
    }

    /// <summary>Checks the relevant source semantics for one capability.</summary>
    /// <param name="sources">The effective source list.</param>
    /// <param name="permission">The capability to evaluate.</param>
    /// <returns>Whether this source list grants the unrestricted capability.</returns>
    private static bool AllowsPermission(Sources sources, Permission permission) => permission switch
    {
        Permission.ScriptInline or Permission.ScriptAttributes => (sources & (Sources.Inline | Sources.NonceOrHash | Sources.StrictDynamic)) == Sources.Inline,
        Permission.ScriptHosts => (sources & (Sources.Wildcard | Sources.StrictDynamic)) == Sources.Wildcard,
        Permission.ScriptEval => (sources & Sources.Eval) != 0,
        Permission.StyleInline or Permission.StyleAttributes => (sources & (Sources.Inline | Sources.NonceOrHash)) == Sources.Inline,
        _ => (sources & Sources.Wildcard) != 0,
    };

    /// <summary>Describes a source list's permission without claiming other resource restrictions are disabled.</summary>
    /// <param name="permission">The capability to describe.</param>
    /// <returns>The diagnostic's permission phrase.</returns>
    private static string EnforcedDescription(Permission permission) => permission switch
    {
        Permission.ScriptInline => "allows unrestricted inline scripts",
        Permission.ScriptHosts => "allows scripts from arbitrary network hosts",
        Permission.ScriptAttributes => "allows unrestricted inline script attributes",
        Permission.ScriptEval => "allows JavaScript evaluation from strings",
        Permission.StyleInline => "allows unrestricted inline styles",
        Permission.StyleHosts => "allows styles from arbitrary network hosts",
        Permission.StyleAttributes => "allows unrestricted inline style attributes",
        Permission.ObjectHosts => "allows embedded objects from arbitrary network hosts",
        _ => "allows base URLs from arbitrary origins",
    };

    /// <summary>Describes hypothetical permissions in a report-only policy.</summary>
    /// <param name="permission">The capability to describe.</param>
    /// <returns>The diagnostic's report-only phrase.</returns>
    private static string ReportOnlyDescription(Permission permission) => permission switch
    {
        Permission.ScriptInline => "would allow unrestricted inline scripts if enforced (report-only policy)",
        Permission.ScriptHosts => "would allow scripts from arbitrary network hosts if enforced (report-only policy)",
        Permission.ScriptAttributes => "would allow unrestricted inline script attributes if enforced (report-only policy)",
        Permission.ScriptEval => "would allow JavaScript evaluation from strings if enforced (report-only policy)",
        Permission.StyleInline => "would allow unrestricted inline styles if enforced (report-only policy)",
        Permission.StyleHosts => "would allow styles from arbitrary network hosts if enforced (report-only policy)",
        Permission.StyleAttributes => "would allow unrestricted inline style attributes if enforced (report-only policy)",
        Permission.ObjectHosts => "would allow embedded objects from arbitrary network hosts if enforced (report-only policy)",
        _ => "would allow base URLs from arbitrary origins if enforced (report-only policy)",
    };

    /// <summary>Reads one policy into fixed source-list storage.</summary>
    /// <param name="text">The serialized policy.</param>
    /// <param name="policy">The destination, cleared before reading.</param>
    private static void ReadPolicy(ReadOnlySpan<char> text, Span<Sources> policy)
    {
        policy.Clear();
        while (!text.IsEmpty)
        {
            var separator = text.IndexOf(';');
            var directive = separator < 0 ? text : text.Slice(0, separator);
            text = separator < 0 ? default : text.Slice(separator + 1);
            ReadDirective(directive, policy);
        }
    }

    /// <summary>Checks script elements, attributes and evaluation against their individual fallbacks.</summary>
    /// <param name="policy">The source lists in the policy.</param>
    /// <returns>The first effective script permission, or null.</returns>
    private static ContentSecurityPolicyViolation? FindScriptViolation(ReadOnlySpan<Sources> policy)
    {
        var script = EffectiveDirective(policy, Directive.Script);
        var elements = EffectiveDirective(policy, Directive.ScriptElement, script);
        var attributes = EffectiveDirective(policy, Directive.ScriptAttribute, script);
        return ScriptViolation(policy[(int)elements], elements)
            ?? InlineViolation(policy[(int)attributes], attributes, Sources.NonceOrHash | Sources.StrictDynamic, Permission.ScriptAttributes)
            ?? ((policy[(int)script] & Sources.Eval) != 0 ? new(Name(script), Permission.ScriptEval) : null);
    }

    /// <summary>Checks style elements and attributes against their individual fallbacks.</summary>
    /// <param name="policy">The source lists in the policy.</param>
    /// <returns>The first effective style permission, or null.</returns>
    private static ContentSecurityPolicyViolation? FindStyleViolation(ReadOnlySpan<Sources> policy)
    {
        var style = EffectiveDirective(policy, Directive.Style);
        var elements = EffectiveDirective(policy, Directive.StyleElement, style);
        var attributes = EffectiveDirective(policy, Directive.StyleAttribute, style);
        return InlineViolation(policy[(int)elements], elements, Sources.NonceOrHash, Permission.StyleInline)
            ?? WildcardViolation(policy[(int)elements], elements, Permission.StyleHosts)
            ?? InlineViolation(policy[(int)attributes], attributes, Sources.NonceOrHash, Permission.StyleAttributes);
    }

    /// <summary>Stores the first source list for a recognized directive.</summary>
    /// <param name="text">One semicolon-delimited directive.</param>
    /// <param name="policy">The facts collected for the policy.</param>
    private static void ReadDirective(ReadOnlySpan<char> text, Span<Sources> policy)
    {
        var directive = GetDirective(ReadToken(ref text));
        if (directive == Directive.Unknown || (policy[(int)directive] & Sources.Present) != 0)
        {
            return;
        }

        var sources = Sources.Present;
        while (!text.IsEmpty)
        {
            sources |= ClassifySource(ReadToken(ref text));
        }

        policy[(int)directive] = sources;
    }

    /// <summary>Resolves a fetch directive's source list through default-src when it is absent.</summary>
    /// <param name="policy">The parsed source lists.</param>
    /// <param name="directive">The resource-specific directive.</param>
    /// <param name="fallback">The directive used when the requested directive is absent.</param>
    /// <returns>The directive supplying the effective source list.</returns>
    private static Directive EffectiveDirective(ReadOnlySpan<Sources> policy, Directive directive, Directive fallback = Directive.Default) =>
        (policy[(int)directive] & Sources.Present) != 0 ? directive : fallback;

    /// <summary>Checks script permissions, including nonce, hash and strict-dynamic precedence.</summary>
    /// <param name="sources">The effective script source list.</param>
    /// <param name="directive">The directive supplying that list.</param>
    /// <returns>A script permission, or null.</returns>
    private static ContentSecurityPolicyViolation? ScriptViolation(Sources sources, Directive directive)
    {
        if ((sources & (Sources.Inline | Sources.NonceOrHash | Sources.StrictDynamic)) == Sources.Inline)
        {
            return new(Name(directive), Permission.ScriptInline);
        }

        return (sources & Sources.StrictDynamic) == 0
            ? WildcardViolation(sources, directive, Permission.ScriptHosts)
            : null;
    }

    /// <summary>Checks unrestricted inline behavior after applying the relevant overriding keywords.</summary>
    /// <param name="sources">The effective source list.</param>
    /// <param name="directive">The directive supplying that list.</param>
    /// <param name="overrides">The facts that make unsafe-inline ineffective for this resource.</param>
    /// <param name="capability">The inline capability to describe.</param>
    /// <returns>An unrestricted inline permission, or null.</returns>
    private static ContentSecurityPolicyViolation? InlineViolation(Sources sources, Directive directive, Sources overrides, Permission capability) =>
        (sources & (Sources.Inline | overrides)) == Sources.Inline ? new(Name(directive), capability) : null;

    /// <summary>Checks whether a source list permits arbitrary network hosts.</summary>
    /// <param name="sources">The source list.</param>
    /// <param name="directive">The directive supplying it.</param>
    /// <param name="capability">The resource permission to describe.</param>
    /// <returns>The wildcard permission, or null.</returns>
    private static ContentSecurityPolicyViolation? WildcardViolation(Sources sources, Directive directive, Permission capability) =>
        (sources & Sources.Wildcard) != 0 ? new(Name(directive), capability) : null;

    /// <summary>Recognizes complete source tokens without inspecting text inside URLs.</summary>
    /// <param name="source">One whitespace-delimited source token.</param>
    /// <returns>The relevant facts represented by the token.</returns>
    private static Sources ClassifySource(ReadOnlySpan<char> source)
    {
        if (source.Equals("'unsafe-inline'".AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            return Sources.Inline;
        }

        if (source.Equals("'unsafe-eval'".AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            return Sources.Eval;
        }

        if (source.Equals("'strict-dynamic'".AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            return Sources.StrictDynamic;
        }

        if (source.Length == 1 && source[0] == '*')
        {
            return Sources.Wildcard;
        }

        return IsNonceOrHash(source) ? Sources.NonceOrHash : Sources.None;
    }

    /// <summary>Validates the nonce-source and hash-source grammar used to override unsafe-inline.</summary>
    /// <param name="source">The complete source token.</param>
    /// <returns>Whether it contains a recognized prefix and a nonempty base64 value.</returns>
    private static bool IsNonceOrHash(ReadOnlySpan<char> source)
    {
        if (source.Length < 8 || source[0] != '\'' || source[source.Length - 1] != '\'')
        {
            return false;
        }

        source = source.Slice(1, source.Length - QuoteCount);
        var prefix = SourcePrefixLength(source);
        return prefix != 0 && IsBase64Value(source.Slice(prefix));
    }

    /// <summary>Recognizes the nonce and supported digest prefixes.</summary>
    /// <param name="source">The source token without its quotes.</param>
    /// <returns>The prefix length, or zero for an unrecognized source.</returns>
    private static int SourcePrefixLength(ReadOnlySpan<char> source)
    {
        if (source.StartsWith("nonce-".AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            return "nonce-".Length;
        }

        return source.StartsWith("sha256-".AsSpan(), StringComparison.OrdinalIgnoreCase)
            || source.StartsWith("sha384-".AsSpan(), StringComparison.OrdinalIgnoreCase)
            || source.StartsWith("sha512-".AsSpan(), StringComparison.OrdinalIgnoreCase)
            ? "sha256-".Length
            : 0;
    }

    /// <summary>Checks CSP's base64-value grammar, including URL-safe characters and optional padding.</summary>
    /// <param name="value">The nonce or digest value.</param>
    /// <returns>Whether the value has data characters followed by at most two padding characters.</returns>
    private static bool IsBase64Value(ReadOnlySpan<char> value)
    {
        var padding = 0;
        foreach (var character in value)
        {
            if (character == '=')
            {
                padding++;
                if (padding > MaximumPadding)
                {
                    return false;
                }
            }
            else if (padding != 0 || !IsBase64Character(character))
            {
                return false;
            }
        }

        return value.Length > padding;
    }

    /// <summary>Recognizes an ASCII base64 or base64url data character.</summary>
    /// <param name="character">The character to classify.</param>
    /// <returns>Whether the character is allowed before padding.</returns>
    private static bool IsBase64Character(char character) =>
        character is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '+' or '/' or '-' or '_';

    /// <summary>Reads one token using CSP's ASCII whitespace rules.</summary>
    /// <param name="text">The remaining directive text, advanced past the returned token.</param>
    /// <returns>The next token, or an empty span at the end.</returns>
    private static ReadOnlySpan<char> ReadToken(ref ReadOnlySpan<char> text)
    {
        var start = 0;
        while (start < text.Length && IsWhitespace(text[start]))
        {
            start++;
        }

        var end = start;
        while (end < text.Length && !IsWhitespace(text[end]))
        {
            end++;
        }

        var token = text.Slice(start, end - start);
        text = text.Slice(end);
        return token;
    }

    /// <summary>Recognizes CSP's ASCII whitespace separators.</summary>
    /// <param name="character">The character to classify.</param>
    /// <returns>Whether the character separates directive names and sources.</returns>
    private static bool IsWhitespace(char character) => character is ' ' or '\t' or '\n' or '\r' or '\f';

    /// <summary>Resolves a complete directive name without allocating a normalized string.</summary>
    /// <param name="name">The directive token.</param>
    /// <returns>The tracked directive, or Unknown.</returns>
    private static Directive GetDirective(ReadOnlySpan<char> name)
    {
        if (name.Equals("default-src".AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            return Directive.Default;
        }

        if (name.Equals("script-src".AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            return Directive.Script;
        }

        if (name.Equals("style-src".AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            return Directive.Style;
        }

        if (name.Equals("object-src".AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            return Directive.Object;
        }

        return name.Equals("base-uri".AsSpan(), StringComparison.OrdinalIgnoreCase) ? Directive.Base : GetSubdirective(name);
    }

    /// <summary>Recognizes the element and attribute overrides for scripts and styles.</summary>
    /// <param name="name">The complete directive name.</param>
    /// <returns>The subdirective, or Unknown.</returns>
    private static Directive GetSubdirective(ReadOnlySpan<char> name)
    {
        if (name.Equals("script-src-elem".AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            return Directive.ScriptElement;
        }

        if (name.Equals("script-src-attr".AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            return Directive.ScriptAttribute;
        }

        if (name.Equals("style-src-elem".AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            return Directive.StyleElement;
        }

        return name.Equals("style-src-attr".AsSpan(), StringComparison.OrdinalIgnoreCase) ? Directive.StyleAttribute : Directive.Unknown;
    }

    /// <summary>Gets the canonical directive name used in a diagnostic.</summary>
    /// <param name="directive">The effective directive.</param>
    /// <returns>The canonical lowercase name.</returns>
    private static string Name(Directive directive) => directive switch
    {
        Directive.Script => "script-src",
        Directive.Style => "style-src",
        Directive.Object => "object-src",
        Directive.Base => "base-uri",
        Directive.ScriptElement => "script-src-elem",
        Directive.ScriptAttribute => "script-src-attr",
        Directive.StyleElement => "style-src-elem",
        Directive.StyleAttribute => "style-src-attr",
        _ => "default-src",
    };
}

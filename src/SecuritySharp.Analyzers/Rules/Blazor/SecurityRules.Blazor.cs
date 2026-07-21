// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>The SES17xx Blazor descriptors.</summary>
internal static partial class SecurityRules
{
    /// <summary>SES1701 -- a non-constant value is rendered as raw HTML, bypassing output encoding.</summary>
    public static readonly DiagnosticDescriptor RawHtmlFromNonConstant = Create(
        "SES1701",
        "Raw HTML is rendered from a non-constant value",
        RawHtmlFromNonConstantMessage,
        Blazor,
        RawHtmlFromNonConstantDescription);

    /// <summary>SES1702 -- a JavaScript interop call targets a script-evaluation primitive.</summary>
    public static readonly DiagnosticDescriptor JsInteropScriptEvaluation = Create(
        "SES1702",
        "A JavaScript interop call targets a script-evaluation primitive",
        JsInteropScriptEvaluationMessage,
        Blazor,
        JsInteropScriptEvaluationDescription);

    /// <summary>The SES1701 rule message.</summary>
    private const string RawHtmlFromNonConstantMessage =
        "'{0}' emits a non-constant value as raw HTML with no encoding, so an attacker-controlled value becomes live markup and "
        + "can inject script (cross-site scripting); encode the value, wrap it in a sanitizer, or render it as text instead";

    /// <summary>The SES1701 rule description.</summary>
    private const string RawHtmlFromNonConstantDescription =
        "Blazor HTML-encodes every value it renders, which is what stops an attacker-supplied string from being interpreted as "
        + "markup. Constructing a 'MarkupString' (via its constructor or the explicit string conversion) or calling "
        + "'RenderTreeBuilder.AddMarkupContent' opts out of that encoding: the value is written to the page verbatim as raw HTML. "
        + "When the value is a compile-time constant it is developer-authored markup and is safe, so it is not reported. When the "
        + "value is non-constant it can carry request data, a database field, or any other untrusted input, and a '<script>' or an "
        + "event-handler attribute inside it will execute in the victim's session -- reflected or stored cross-site scripting. The "
        + "rule reports the non-constant value passed to any of these raw-HTML sinks. A value wrapped in a call to a method named in "
        + "the 'securitysharp.SES1701.sanitizers' option (a comma-separated allow-list) is treated as already-encoded and is not "
        + "reported. Prefer rendering the value as ordinary text so Blazor encodes it; only build a 'MarkupString' from markup you "
        + "control or from output that a trusted sanitizer has produced. The rule is gated on "
        + "'Microsoft.AspNetCore.Components.MarkupString' resolving, so a non-Blazor project pays nothing.";

    /// <summary>The SES1702 rule message.</summary>
    private const string JsInteropScriptEvaluationMessage =
        "This interop call invokes '{0}', a JavaScript primitive that evaluates arbitrary source, turning interop into a "
        + "script-injection channel; call a dedicated JavaScript function that performs the work instead of passing code to '{0}'";

    /// <summary>The SES1702 rule description.</summary>
    private const string JsInteropScriptEvaluationDescription =
        "An 'IJSRuntime' or 'IJSObjectReference' interop call names the JavaScript function to run by its identifier string. When "
        + "that identifier is a primitive that evaluates source -- 'eval', 'Function', 'document.write', or 'document.writeln' -- "
        + "every argument the interop call forwards becomes executable script, so any untrusted value in those arguments runs in the "
        + "browser: a script-injection channel opened straight through the interop boundary. 'setTimeout' and 'setInterval' are the "
        + "same hazard, but only when their first argument is a string body that the browser compiles and runs; passed a real "
        + "function reference they are safe, so the rule reports them only when the argument after the identifier is a string. The "
        + "rule reports the identifier argument when it is one of these constant eval-class values. Expose a named JavaScript "
        + "function that does the specific work and invoke that by name, passing data as ordinary arguments, so no argument is ever "
        + "evaluated as code. The rule is gated on 'Microsoft.JSInterop.IJSRuntime' resolving, so a non-Blazor project pays nothing.";
}

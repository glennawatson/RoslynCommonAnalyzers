// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>The SES1309 descriptor.</summary>
internal static partial class SecurityRules
{
    /// <summary>SES1309 — an XSLT stylesheet is loaded with settings that enable embedded script.</summary>
    public static readonly DiagnosticDescriptor XsltScriptExecution = Create(
        "SES1309",
        "Do not load an XSLT stylesheet with script execution enabled",
        "The 'XsltSettings' passed to 'XslCompiledTransform.Load' enable script; a stylesheet can then run embedded code in the host process",
        Injection,
        XsltScriptExecutionDescription);

    /// <summary>The SES1309 rule description.</summary>
    private const string XsltScriptExecutionDescription =
        "'XslCompiledTransform.Load' compiles the stylesheet it is given, and when the 'XsltSettings' handed to it enable "
        + "script -- an object initializer that sets 'EnableScript = true', the 'new XsltSettings(enableDocumentFunction, "
        + "enableScript)' constructor with 'enableScript' true, or the static 'XsltSettings.TrustedXslt' that turns on both "
        + "the document() function and script -- an embedded script block in the stylesheet is compiled and executed in the "
        + "host process with the host's privileges. A stylesheet that comes from or is influenced by untrusted input then "
        + "becomes arbitrary code execution. Load stylesheets with script disabled ('XsltSettings.Default', or leaving "
        + "'EnableScript' false), and never enable script for a stylesheet you do not fully control.";
}

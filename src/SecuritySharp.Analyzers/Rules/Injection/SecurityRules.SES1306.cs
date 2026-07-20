// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>The SES1306 descriptor.</summary>
internal static partial class SecurityRules
{
    /// <summary>SES1306 — a scripting-API call compiles and runs a non-constant C# source string.</summary>
    public static readonly DiagnosticDescriptor DynamicScriptCompilation = Create(
        "SES1306",
        "Do not compile or execute non-constant C# via the scripting API",
        "The C# source passed to 'CSharpScript.{0}' is not a compile-time constant; compiling and running data-derived source is arbitrary code execution",
        Injection,
        DynamicScriptCompilationDescription);

    /// <summary>The SES1306 rule description.</summary>
    private const string DynamicScriptCompilationDescription =
        "'CSharpScript.EvaluateAsync', 'RunAsync', and 'Create' compile the string they are given and execute it in the "
        + "host process with the host's privileges. When that string is a compile-time constant it is a trusted template the "
        + "author wrote; when any part of it comes from runtime data -- a request field, a file, a database value, a "
        + "concatenation or interpolation that folds in a variable -- an attacker who controls that data controls the code "
        + "that runs, which is remote code execution. The code channel must stay a constant, trusted script; never build it "
        + "from untrusted input. If dynamic behaviour is required, drive it through a fixed set of vetted operations selected "
        + "by the data rather than by compiling the data as source.";
}

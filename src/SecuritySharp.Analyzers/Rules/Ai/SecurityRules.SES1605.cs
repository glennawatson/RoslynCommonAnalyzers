// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>The SES1605 descriptor.</summary>
internal static partial class SecurityRules
{
    /// <summary>SES1605 — AI instrumentation is told to capture raw prompts and responses as telemetry.</summary>
    public static readonly DiagnosticDescriptor SensitiveAiTelemetry = new(
        "SES1605",
        "AI instrumentation must not enable sensitive-data capture",
        "'{0}.EnableSensitiveData' is set to true; this ships raw prompts and model responses -- which routinely carry secrets and PII -- verbatim to your telemetry backend",
        Ai,
        DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: SensitiveAiTelemetryDescription,
        helpLinkUri: "https://github.com/glennawatson/RoslynCommonAnalyzers/blob/main/docs/rules/SES1605.md");

    /// <summary>The SES1605 rule description.</summary>
    private const string SensitiveAiTelemetryDescription =
        "OpenTelemetry instrumentation for generative-AI clients captures operation metadata -- token counts, model names, "
        + "durations -- but keeps raw inputs and outputs out of telemetry by default, because a chat prompt or a model response "
        + "routinely carries credentials, access tokens, personal data, and other secrets. Setting 'EnableSensitiveData' to true "
        + "turns that raw content back on: every prompt and completion, and every function-call argument and result, is written "
        + "verbatim to whatever backend receives the traces -- application logs, an APM service, a third-party collector -- which "
        + "widens the blast radius of that store and often crosses a data-residency or compliance boundary. This rule reports an "
        + "'EnableSensitiveData = true' assignment (a plain statement or an object-initializer member) whose target is the "
        + "'EnableSensitiveData' property of a 'Microsoft.Extensions.AI' OpenTelemetry instrumentation client -- the chat, "
        + "embedding, realtime, speech, image, and hosted-file variants. The value is read locally from the right-hand side, so "
        + "only a compile-time 'true' is flagged; a false, or a value computed at runtime, is left alone. Capturing raw content "
        + "is a deliberate diagnostics choice that is defensible while debugging in a controlled environment, so this is reported "
        + "at Info: keep it off in production, or gate it behind a development-only configuration.";
}

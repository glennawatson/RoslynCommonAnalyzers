// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>The SES1602 descriptor.</summary>
internal static partial class SecurityRules
{
    /// <summary>SES1602 — text read from an AI model response flows directly into a code/command/query sink.</summary>
    public static readonly DiagnosticDescriptor ModelOutputToDangerousSink = Create(
        "SES1602",
        "Do not route AI model output into a process, file, or raw SQL sink",
        "Text read from the AI model response flows directly into {0}; treat model output as untrusted data and never let it choose the program, path, or query that runs",
        Ai,
        ModelOutputToDangerousSinkDescription);

    /// <summary>The SES1602 rule description.</summary>
    private const string ModelOutputToDangerousSinkDescription =
        "A language model's response is attacker-influenced data: a prompt-injection payload hidden in a document, a tool "
        + "result, or a user turn can make the model emit any string the attacker chooses. When that string is used verbatim "
        + "to launch a process, name a program or its arguments, build a file-system path, or form a raw SQL command, the "
        + "model -- and whoever steered it -- decides what code, file, or query the host executes. That turns prompt injection "
        + "into remote code execution, arbitrary file access, or SQL injection. The rule reports the model text ('ChatResponse.Text' "
        + "or 'ChatMessage.Text', inline or through a single immediately-preceding local) where it reaches such a sink: "
        + "'Process.Start', a 'ProcessStartInfo.FileName'/'Arguments' assignment, a 'System.IO.File' path, or a raw-SQL "
        + "'FromSqlRaw'/'ExecuteSqlRaw' command. Never execute or act on model output directly. Constrain the model to a fixed, "
        + "vetted set of operations, validate its output against an allow-list, and pass any dynamic value as a parameter -- "
        + "process 'ArgumentList' entries or SQL command parameters -- never as part of the executed text.";
}

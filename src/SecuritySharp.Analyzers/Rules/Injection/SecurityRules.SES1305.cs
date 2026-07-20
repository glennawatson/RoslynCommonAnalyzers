// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>The SES1305 descriptor.</summary>
internal static partial class SecurityRules
{
    /// <summary>SES1305 — an uploaded file name is used to build a storage path.</summary>
    public static readonly DiagnosticDescriptor UploadFilenameInPath = Create(
        "SES1305",
        "Do not build a storage path from an uploaded file name",
        UploadFilenameInPathMessage,
        Injection,
        UploadFilenameInPathDescription);

    /// <summary>The SES1305 rule message.</summary>
    private const string UploadFilenameInPathMessage =
        "The uploaded file name '{0}' is attacker-controlled and may contain '..' segments or a rooted path; using it to build a "
        + "storage path enables path traversal -- reduce it with Path.GetFileName or use a server-generated name";

    /// <summary>The SES1305 rule description.</summary>
    private const string UploadFilenameInPathDescription =
        "The 'FileName' of an uploaded file is set entirely by the client; a request can send '..\\..\\appsettings.json' or a "
        + "rooted path like '/etc/passwd'. Feeding that value straight into a filesystem sink -- 'Path.Combine', a '+' path "
        + "concatenation, or a file-creating call such as 'File.Create', 'File.OpenWrite', 'File.WriteAllBytes', 'File.Copy', or "
        + "'new FileStream(...)' -- lets the request escape the intended upload directory and read or overwrite arbitrary files. "
        + "Strip the directory portion with 'Path.GetFileName' before combining, or store the upload under a server-generated "
        + "name (for example a GUID) and keep the original name only as metadata.";
}

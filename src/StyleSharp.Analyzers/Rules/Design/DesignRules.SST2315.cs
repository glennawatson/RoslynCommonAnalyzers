// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2315 descriptor.</summary>
internal static partial class DesignRules
{
    /// <summary>SST2315 — a type creates and keeps a disposable but does not advertise disposal.</summary>
    public static readonly DiagnosticDescriptor OwnsDisposableField = Create(
        "SST2315",
        "A type that owns a disposable should be disposable",
        "'{0}' creates and owns a disposable through '{1}' but is not IDisposable, so nothing releases it",
        OwnsDisposableFieldDescription);

    /// <summary>The OwnsDisposableField rule description.</summary>
    private const string OwnsDisposableFieldDescription =
        "A type that builds a disposable and holds onto it has taken ownership of a resource — a handle, a socket, an event "
        + "subscription — and the only place that resource can be released is a disposal method the type does not have. Because "
        + "the type is not IDisposable, no owner, DI container, or 'using' ever cleans it up, and the resource leaks for the life "
        + "of the process. This reports the ownership shapes a plain 'new' on a field does not cover: a member assigned from a "
        + "static factory (File.OpenRead, a Create call), an auto-property initialized with 'new', and a collection the type fills "
        + "with newly created disposables. A member assigned from a constructor parameter is left alone — that is an injected "
        + "dependency the caller owns and must not be disposed here. Implement IDisposable (or IAsyncDisposable) and release the "
        + "owned members; for a collection, dispose each element.";
}

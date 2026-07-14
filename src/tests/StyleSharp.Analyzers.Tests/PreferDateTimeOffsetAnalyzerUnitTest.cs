// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using VerifyDateTimeOffset = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2016PreferDateTimeOffsetAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2016 (expose DateTimeOffset rather than DateTime).</summary>
public class PreferDateTimeOffsetAnalyzerUnitTest
{
    /// <summary>Verifies every externally visible declaration of a DateTime is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EveryExternallyVisibleDateTimeDeclarationIsReportedAsync()
        => await VerifyNet80Async(
            """
            using System;

            public class C
            {
                public {|SST2016:DateTime|} Field;

                protected {|SST2016:DateTime|} ProtectedField;

                public {|SST2016:DateTime|} Property { get; set; }

                public {|SST2016:DateTime|} Returns() => default;

                public void Takes({|SST2016:DateTime|} when) => GC.KeepAlive(when.ToString());

                public C({|SST2016:DateTime|} when) => Field = when;
            }
            """);

    /// <summary>Verifies a nullable DateTime is reported: the offset is just as absent.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NullableDateTimeIsReportedAsync()
        => await VerifyNet80Async(
            """
            using System;

            public class C
            {
                public {|SST2016:DateTime?|} Deleted { get; set; }
            }
            """);

    /// <summary>Verifies a fully qualified DateTime is reported, and reported once.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task QualifiedDateTimeIsReportedAsync()
        => await VerifyNet80Async(
            """
            public class C
            {
                public {|SST2016:System.DateTime|} Stamped { get; set; }
            }
            """);

    /// <summary>Verifies a positional record parameter is reported: the generated property takes its type.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RecordPositionalParameterIsReportedAsync()
        => await VerifyNet80Async(
            """
            using System;

            public record Booking({|SST2016:DateTime|} Start, string Name);
            """);

    /// <summary>Verifies a delegate's return type and parameters are reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DelegateSignatureIsReportedAsync()
        => await VerifyNet80Async(
            """
            using System;

            public delegate {|SST2016:DateTime|} Clock({|SST2016:DateTime|} offsetFrom);
            """);

    /// <summary>Verifies a clock read is left to SST2010: this rule looks at declared types, never at expressions.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ClockReadsAreNotReportedAsync()
        => await VerifyNet80Async(
            """
            using System;

            public class C
            {
                public DateTimeOffset Read() => DateTimeOffset.UtcNow;

                public int Hour()
                {
                    var now = DateTime.Now;
                    var utc = DateTime.UtcNow;
                    var today = DateTime.Today;
                    return now.Hour + utc.Hour + today.Hour;
                }
            }
            """);

    /// <summary>Verifies a local, and a member nobody outside the assembly can see, are left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LocalsAndInvisibleMembersAreNotReportedAsync()
        => await VerifyNet80Async(
            """
            using System;

            internal class Internal
            {
                public DateTime Visible { get; set; }
            }

            public class C
            {
                private DateTime _started;

                internal DateTime Internal { get; set; }

                private DateTime Hidden(DateTime when) => when;

                public int Length()
                {
                    DateTime local = default;
                    static DateTime Nested(DateTime when) => when;
                    return Hidden(Nested(local)).Millisecond + _started.Millisecond;
                }
            }
            """);

    /// <summary>Verifies the declaration that owns the type is reported, and the members restating it are not.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OverrideAndInterfaceImplementationAreNotReportedAsync()
        => await VerifyNet80Async(
            """
            using System;

            public interface IStamped
            {
                {|SST2016:DateTime|} Stamp { get; }
            }

            public abstract class Base
            {
                public abstract {|SST2016:DateTime|} Created { get; }
            }

            public class C : Base, IStamped
            {
                public override DateTime Created => default;

                public DateTime Stamp => default;
            }
            """);

    /// <summary>Verifies a signature already written in DateTimeOffset is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DateTimeOffsetSignatureIsCleanAsync()
        => await VerifyNet80Async(
            """
            using System;

            public class C
            {
                public DateTimeOffset Created { get; set; }

                public DateTimeOffset Shift(DateTimeOffset when, TimeSpan by) => when + by;
            }
            """);

    /// <summary>Verifies the rule stays silent when the compilation has no DateTimeOffset to move to.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// Every shipping reference set has a DateTimeOffset, so the absent case is built by hand: a compilation
    /// whose only framework types are the ones written into it.
    /// </remarks>
    [Test]
    public async Task NoDiagnosticWithoutDateTimeOffsetAsync()
    {
        var diagnostics = await AnalyzeWithoutFrameworkReferencesAsync(withDateTimeOffset: false);

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Verifies the same source is reported the moment DateTimeOffset exists.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>The control for the test above: it proves the silence there is the gate, not a failure to bind DateTime.</remarks>
    [Test]
    public async Task TheSameSourceIsReportedOnceDateTimeOffsetExistsAsync()
    {
        var diagnostics = await AnalyzeWithoutFrameworkReferencesAsync(withDateTimeOffset: true);

        await Assert.That(diagnostics.Length).IsEqualTo(1);
        await Assert.That(diagnostics[0].Id).IsEqualTo("SST2016");
    }

    /// <summary>Runs the analyzer against the .NET 8 reference assemblies.</summary>
    /// <param name="source">The source to analyze.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet80Async(string source)
    {
        var test = new VerifyDateTimeOffset.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = source,
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs the analyzer over a compilation that references no framework at all.</summary>
    /// <param name="withDateTimeOffset">Whether the hand-written framework declares a DateTimeOffset.</param>
    /// <returns>The diagnostics the analyzer reported.</returns>
    private static Task<ImmutableArray<Diagnostic>> AnalyzeWithoutFrameworkReferencesAsync(bool withDateTimeOffset)
    {
        var dateTimeOffset = withDateTimeOffset ? "public struct DateTimeOffset { }" : string.Empty;
        var source = $$"""
                       namespace System
                       {
                           public class Object { }
                           public class ValueType { }
                           public class String { }
                           public struct Void { }
                           public struct Boolean { }
                           public struct Int32 { }
                           public class Attribute { }
                           public struct DateTime { }
                           {{dateTimeOffset}}
                       }

                       public class Booking
                       {
                           public System.DateTime Start;
                       }
                       """;

        var compilation = CSharpCompilation.Create(
            assemblyName: "PreferDateTimeOffsetAnalyzerUnitTest",
            syntaxTrees: [CSharpSyntaxTree.ParseText(source)],
            references: []);
        var withAnalyzers = compilation.WithAnalyzers([new Sst2016PreferDateTimeOffsetAnalyzer()]);

        return withAnalyzers.GetAnalyzerDiagnosticsAsync(CancellationToken.None);
    }
}

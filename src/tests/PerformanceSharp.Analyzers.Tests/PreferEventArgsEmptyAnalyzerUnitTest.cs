// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1022PreferEventArgsEmptyAnalyzer,
    PerformanceSharp.Analyzers.Psh1022PreferEventArgsEmptyCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1022PreferEventArgsEmptyAnalyzer"/> (PSH1022 use EventArgs.Empty).</summary>
public class PreferEventArgsEmptyAnalyzerUnitTest
{
    /// <summary>Verifies a parameterless allocation raised as event args is reported and replaced.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NewEventArgsIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public event EventHandler Changed;

                                  public void Raise() => Changed?.Invoke(this, {|PSH1022:new EventArgs()|});
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public event EventHandler Changed;

                                       public void Raise() => Changed?.Invoke(this, EventArgs.Empty);
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies the target-typed <c>new()</c> form is reported and replaced with the simple name.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TargetTypedAllocationIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public EventArgs Make() => {|PSH1022:new()|};
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public EventArgs Make() => EventArgs.Empty;
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a fully qualified allocation keeps the qualification the author wrote.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task QualifiedAllocationKeepsQualificationAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public System.EventArgs Make() => {|PSH1022:new System.EventArgs()|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public System.EventArgs Make() => System.EventArgs.Empty;
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a derived EventArgs is never reported: it is a different type that may carry state.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DerivedEventArgsIsCleanAsync()
        => await VerifyCleanAsync(
            """
            using System;

            public sealed class RenamedEventArgs : EventArgs
            {
            }

            public class C
            {
                public EventArgs Make() => new RenamedEventArgs();
            }
            """);

    /// <summary>Verifies a construction with an object initializer is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InitializerAllocationIsCleanAsync()
        => await VerifyCleanAsync(
            """
            using System;

            public class C
            {
                public EventArgs Make() => new EventArgs() { };
            }
            """);

    /// <summary>Verifies another type's parameterless allocation is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OtherTypeAllocationIsCleanAsync()
        => await VerifyCleanAsync(
            """
            public class C
            {
                public object Make() => new object();
            }
            """);

    /// <summary>Runs a verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="fixedSource">The expected fixed source.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source, string fixedSource)
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
            FixedCode = fixedSource
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a no-diagnostic verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source expected to produce no diagnostics.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyCleanAsync(string source) => await VerifyAsync(source, source);
}

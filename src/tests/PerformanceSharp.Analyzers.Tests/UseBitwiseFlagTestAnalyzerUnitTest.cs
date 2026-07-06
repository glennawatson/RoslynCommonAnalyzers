// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using AnalyzerVerify = PerformanceSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    PerformanceSharp.Analyzers.Psh1016UseBitwiseFlagTestAnalyzer>;
using Verify = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1016UseBitwiseFlagTestAnalyzer,
    PerformanceSharp.Analyzers.Psh1016UseBitwiseFlagTestCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1016UseBitwiseFlagTestAnalyzer"/> (PSH1016 bitwise flag test).</summary>
public class UseBitwiseFlagTestAnalyzerUnitTest
{
    /// <summary>Verifies a HasFlag call is flagged and rewritten to a bitwise test.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task HasFlagIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System;

                              [Flags]
                              public enum MyFlags
                              {
                                  None = 0,
                                  A = 1,
                                  B = 2,
                              }

                              public class C
                              {
                                  public bool M(MyFlags flags) => {|PSH1016:flags.HasFlag(MyFlags.A)|};
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   [Flags]
                                   public enum MyFlags
                                   {
                                       None = 0,
                                       A = 1,
                                       B = 2,
                                   }

                                   public class C
                                   {
                                       public bool M(MyFlags flags) => (flags & MyFlags.A) == MyFlags.A;
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a negated HasFlag call folds the negation into a <c>!=</c> test.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NegatedHasFlagIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System;

                              [Flags]
                              public enum MyFlags
                              {
                                  None = 0,
                                  A = 1,
                                  B = 2,
                              }

                              public class C
                              {
                                  public bool M(MyFlags flags) => !{|PSH1016:flags.HasFlag(MyFlags.A)|};
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   [Flags]
                                   public enum MyFlags
                                   {
                                       None = 0,
                                       A = 1,
                                       B = 2,
                                   }

                                   public class C
                                   {
                                       public bool M(MyFlags flags) => (flags & MyFlags.A) != MyFlags.A;
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a combined flag argument is parenthesized on both sides of the rewrite.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CompositeFlagArgumentIsParenthesizedAsync()
    {
        const string Source = """
                              using System;

                              [Flags]
                              public enum MyFlags
                              {
                                  None = 0,
                                  A = 1,
                                  B = 2,
                              }

                              public class C
                              {
                                  public bool M(MyFlags flags) => {|PSH1016:flags.HasFlag(MyFlags.A | MyFlags.B)|};
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   [Flags]
                                   public enum MyFlags
                                   {
                                       None = 0,
                                       A = 1,
                                       B = 2,
                                   }

                                   public class C
                                   {
                                       public bool M(MyFlags flags) => (flags & (MyFlags.A | MyFlags.B)) == (MyFlags.A | MyFlags.B);
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a method-call argument is reported but gets no fix, since the call cannot be repeated safely.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MethodCallArgumentIsReportedWithoutFixAsync()
    {
        const string Source = """
                              using System;

                              [Flags]
                              public enum MyFlags
                              {
                                  None = 0,
                                  A = 1,
                              }

                              public class C
                              {
                                  public bool M(MyFlags flags) => {|PSH1016:flags.HasFlag(Next())|};

                                  private static MyFlags Next() => MyFlags.A;
                              }
                              """;

        var test = new AnalyzerVerify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = Source,
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a user-defined HasFlag method on a non-enum type stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UserDefinedHasFlagIsCleanAsync()
        => await VerifyAsync(
            """
            public struct Mask
            {
                public bool HasFlag(Mask other) => true;
            }

            public class C
            {
                public bool M(Mask m, Mask n) => m.HasFlag(n);
            }
            """);

    /// <summary>Verifies HasFlag on a receiver typed as the abstract <c>System.Enum</c> stays clean; the bitwise rewrite could not compile there.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EnumTypedReceiverIsCleanAsync()
        => await VerifyAsync(
            """
            using System;

            [Flags]
            public enum MyFlags
            {
                None = 0,
                A = 1,
            }

            public class C
            {
                public bool M(Enum value, MyFlags flag) => value.HasFlag(flag);
            }
            """);

    /// <summary>Verifies a HasFlag call nested inside an argument list composes into the surrounding expression.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task HasFlagInsideArgumentIsFixedAsync()
    {
        const string Source = """
                              using System;

                              [Flags]
                              public enum MyFlags
                              {
                                  None = 0,
                                  A = 1,
                                  B = 2,
                              }

                              public class C
                              {
                                  public string M(MyFlags flags) => Format({|PSH1016:flags.HasFlag(MyFlags.A)|});

                                  private static string Format(bool value) => value.ToString();
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   [Flags]
                                   public enum MyFlags
                                   {
                                       None = 0,
                                       A = 1,
                                       B = 2,
                                   }

                                   public class C
                                   {
                                       public string M(MyFlags flags) => Format((flags & MyFlags.A) == MyFlags.A);

                                       private static string Format(bool value) => value.ToString();
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Runs a verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The test source.</param>
    /// <param name="fixedSource">The expected fixed source, when a fix should apply.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source, string? fixedSource = null)
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
        };
        if (fixedSource is not null)
        {
            test.FixedCode = fixedSource;
        }

        await test.RunAsync(CancellationToken.None);
    }
}

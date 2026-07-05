// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyPrimaryConstructorStorage = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2241PrimaryConstructorStorageAnalyzer,
    StyleSharp.Analyzers.Sst2241PrimaryConstructorStorageCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="Sst2241PrimaryConstructorStorageAnalyzer"/>.</summary>
public class PrimaryConstructorStorageAnalyzerUnitTest
{
    /// <summary>Verifies a constructor that only stores its parameters is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task StorageOnlyConstructorIsReportedAsync()
    {
        var test = new VerifyPrimaryConstructorStorage.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = """
                       public sealed class C
                       {
                           private readonly int _value;

                           public {|SST2241:C|}(int value)
                           {
                               _value = value;
                           }
                       }
                       """,
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a storage-only constructor is converted to primary-constructor storage.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task StorageOnlyConstructorIsConvertedAsync()
    {
        const string Source = """
            public sealed class C
            {
                private readonly int _value;

                public {|SST2241:C|}(int value)
                {
                    _value = value;
                }
            }
            """;
        const string Fixed = """
            public sealed class C(int value)
            {
                private readonly int _value = value;
            }
            """;

        await VerifyCodeFixAsync(Source, Fixed);
    }

    /// <summary>Verifies constructor parameter docs move to the primary-constructor type docs.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ConstructorParameterDocsMoveToTypeAsync()
    {
        const string Source = """
            /// <summary>Stores a value.</summary>
            public sealed class C
            {
                private readonly string _value;

                /// <summary>Initializes a new instance of the <see cref="C"/> class.</summary>
                /// <param name="value">The value to store.</param>
                public {|SST2241:C|}(string value)
                {
                    _value = value;
                }
            }
            """;
        const string Fixed = """
            /// <summary>Stores a value.</summary>
            /// <param name="value">The value to store.</param>
            public sealed class C(string value)
            {
                private readonly string _value = value;
            }
            """;

        await VerifyCodeFixAsync(Source, Fixed);
    }

    /// <summary>Verifies base-constructor arguments are preserved on the primary-constructor base type.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task BaseConstructorArgumentsAreCarriedOverAsync()
    {
        const string Source = """
            public abstract class B
            {
                protected B(int value)
                {
                }
            }

            public sealed class C : B
            {
                private readonly int _value;

                public {|SST2241:C|}(int value)
                    : base(value)
                {
                    _value = value;
                }
            }
            """;
        const string Fixed = """
            public abstract class B
            {
                protected B(int value)
                {
                }
            }

            public sealed class C(int value) : B(value)
            {
                private readonly int _value = value;
            }
            """;

        await VerifyCodeFixAsync(Source, Fixed);
    }

    /// <summary>Verifies constructors with extra work are clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ConstructorWithExtraWorkIsCleanAsync()
    {
        var test = new VerifyPrimaryConstructorStorage.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = """
                       public sealed class C
                       {
                           private readonly int _value;

                           public C(int value)
                           {
                               _value = value;
                               System.Console.WriteLine(value);
                           }
                       }
                       """,
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies constructors that validate parameters are clean because the body cannot be moved to initializers.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ConstructorWithValidationIsCleanAsync()
    {
        var test = new VerifyPrimaryConstructorStorage.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = """
                       public sealed class C
                       {
                           private readonly string _value;

                           public C(string value)
                           {
                               System.ArgumentNullException.ThrowIfNull(value);
                               _value = value;
                           }
                       }
                       """,
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a storage-only constructor is clean when the type has another constructor.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task StorageOnlyConstructorOnMultiConstructorTypeIsCleanAsync()
    {
        var test = new VerifyPrimaryConstructorStorage.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = """
                       #nullable enable

                       public sealed class Bag
                       {
                           private readonly object? _a;
                           private readonly object? _b;

                           public Bag()
                           {
                           }

                           public Bag(object a, object b)
                           {
                               _a = a;
                               _b = b;
                           }
                       }
                       """,
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs the code-fix verifier with modern reference assemblies.</summary>
    /// <param name="source">The source code to analyze and fix.</param>
    /// <param name="fixedSource">The expected source after the fix.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private static async Task VerifyCodeFixAsync(string source, string fixedSource)
    {
        var test = new VerifyPrimaryConstructorStorage.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = source,
            FixedCode = fixedSource
        };

        await test.RunAsync(CancellationToken.None);
    }
}

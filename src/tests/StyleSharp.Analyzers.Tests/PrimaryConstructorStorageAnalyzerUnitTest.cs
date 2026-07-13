// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;
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

    /// <summary>Verifies generic types keep tight type-parameter spacing and a spaced base-list colon.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task GenericTypeWithBaseListKeepsExpectedSpacingAsync()
    {
        const string Source = """
            public interface I<T>
            {
            }

            public sealed class C<T> : I<T>
            {
                private readonly T _value;

                public {|SST2241:C|}(T value)
                {
                    _value = value;
                }
            }
            """;
        const string Fixed = """
            public interface I<T>
            {
            }

            public sealed class C<T>(T value) : I<T>
            {
                private readonly T _value = value;
            }
            """;

        await VerifyCodeFixAsync(Source, Fixed);
    }

    /// <summary>Verifies the code fix skips a type when promoted parameters would be shadowed by body declarations.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task CodeFixSkipsBodyScopeParameterNameCollisionsAsync()
    {
        const string Source = """
            #nullable enable

            public sealed class C
            {
                private object? _owner;

                public {|SST2241:C|}(object owner)
                {
                    _owner = owner;
                }

                public void Dispose()
                {
                    var owner = System.Threading.Interlocked.Exchange(ref _owner, null);
                }
            }
            """;

        await VerifyCodeFixAsync(Source, Source);
    }

    /// <summary>Verifies constructor parameter docs moved onto a nested type keep the nested indentation.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task NestedConstructorParameterDocsKeepTypeIndentationAsync()
    {
        const string Source = """
            public sealed class Outer
            {
                /// <summary>Projection subscription.</summary>
                private sealed class Subscription
                {
                    private readonly object _parent;
                    private readonly object _observer;

                    /// <summary>Initializes a new instance of the <see cref="Subscription"/> class.</summary>
                    /// <param name="parent">Parent projection.</param>
                    /// <param name="observer">Observer to remove.</param>
                    public {|SST2241:Subscription|}(object parent, object observer)
                    {
                        _parent = parent;
                        _observer = observer;
                    }
                }
            }
            """;
        const string Fixed = """
            public sealed class Outer
            {
                /// <summary>Projection subscription.</summary>
                /// <param name="parent">Parent projection.</param>
                /// <param name="observer">Observer to remove.</param>
                private sealed class Subscription(object parent, object observer)
                {
                    private readonly object _parent = parent;
                    private readonly object _observer = observer;
                }
            }
            """;

        await VerifyCodeFixAsync(Source, Fixed);
    }

    /// <summary>Verifies Fix All converts nested storage-only types in one document.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task FixAllConvertsNestedStorageOnlyTypesAsync()
    {
        const string Source = """
            public sealed class Outer
            {
                private readonly int _value;

                public {|SST2241:Outer|}(int value)
                {
                    _value = value;
                }

                private sealed class Inner
                {
                    private readonly string _name;

                    public {|SST2241:Inner|}(string name)
                    {
                        _name = name;
                    }
                }
            }
            """;
        const string Fixed = """
            public sealed class Outer(int value)
            {
                private readonly int _value = value;

                private sealed class Inner(string name)
                {
                    private readonly string _name = name;
                }
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

    /// <summary>Verifies the rule stays silent below C# 12, where primary constructors on classes do not exist.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SilentBelowCSharp12Async()
    {
        const string Source = """
                              public sealed class C
                              {
                                  private readonly int _value;

                                  public C(int value)
                                  {
                                      _value = value;
                                  }
                              }
                              """;
        var test = new VerifyPrimaryConstructorStorage.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = Source,
            FixedCode = Source
        };
        test.SolutionTransforms.Add(static (solution, projectId) =>
        {
            var parseOptions = (CSharpParseOptions)solution.GetProject(projectId)!.ParseOptions!;
            return solution.WithProjectParseOptions(projectId, parseOptions.WithLanguageVersion(LanguageVersion.CSharp11));
        });
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a constructor a primary constructor would have to widen is not reported.</summary>
    /// <remarks>
    /// A primary constructor is <c>public</c> on a concrete class — the language does not let it be
    /// anything else. Rewriting a non-public constructor into one therefore adds a new way to construct
    /// the type from outside, which is an API change rather than a formatting one.
    /// </remarks>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ConstructorThatWouldBeWidenedIsNotReportedAsync()
    {
        const string Source = """
                              internal sealed class C
                              {
                                  private readonly int _value;

                                  internal C(int value)
                                  {
                                      _value = value;
                                  }

                                  public int Value => _value;
                              }
                              """;
        await VerifyCodeFixAsync(Source, Source);
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

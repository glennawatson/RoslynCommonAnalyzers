// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using VerifyStaticGeneric = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.TypeDesignAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1431 (static member of a generic type ignoring its type parameters).</summary>
public class StaticMemberInGenericTypeAnalyzerUnitTest
{
    /// <summary>Verifies a static method on a generic type that ignores the type parameter is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task StaticMemberIgnoringTypeParameterReportedAsync() =>
        VerifyStaticGeneric.VerifyAnalyzerAsync(
            """
            public class Cache<T>
            {
                private int _hits;

                public static void {|SST1431:Clear|}()
                {
                }

                public static T Create() => default;
            }
            """);

    /// <summary>Verifies a static member that reaches a nested type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// A type nested in a generic type is a different type for every instantiation, so naming one uses the
    /// enclosing type parameters as surely as writing them out.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task StaticMemberReachingANestedTypeIsCleanAsync() =>
        VerifyStaticGeneric.VerifyAnalyzerAsync(
            """
            public class Cache<T>
            {
                private int _hits;

                public int Hits => _hits;

                public static bool Reset()
                {
                    var state = new Entry();
                    return state.Used;
                }

                public sealed class Entry
                {
                    public bool Used { get; set; }
                }
            }
            """);

    /// <summary>Verifies a nested type declared in another part of the type still counts.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>The name alone cannot show it, so the symbol decides rather than the syntax in this file.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NestedTypeFromAnotherPartIsCleanAsync() =>
        VerifyStaticGeneric.VerifyAnalyzerAsync(
            """
            public partial class Cache<T>
            {
                private int _hits;

                public int Hits => _hits;

                public static bool Reset()
                {
                    var state = new Entry();
                    return state.Used;
                }
            }

            public partial class Cache<T>
            {
                public sealed class Entry
                {
                    public bool Used { get; set; }
                }
            }
            """);

    /// <summary>Verifies static members that use the type parameter and a private static helper are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task TypeParameterUsersAreCleanAsync() =>
        VerifyStaticGeneric.VerifyAnalyzerAsync(
            """
            public class Cache<T>
            {
                private int _hits;

                public static T Default { get; set; }

                public static bool Matches(T value) => value is not null;

                private static void Reset()
                {
                }
            }
            """);

    /// <summary>Verifies a field whose initializer references the type parameter is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task InitializerReferencingTypeParameterIsCleanAsync() =>
        VerifyStaticGeneric.VerifyAnalyzerAsync(
            """
            public class Owner<T>
            {
                private int _instance;

                public static readonly string Name = typeof(T).Name;
            }
            """);

    /// <summary>Verifies a field whose initializer mentions the closed self-type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ClosedSelfTypeInInitializerIsCleanAsync() =>
        VerifyStaticGeneric.VerifyAnalyzerAsync(
            """
            public class Owner<TViewModel>
                where TViewModel : class
            {
                private int _instance;

                public static readonly string Key = typeof(Owner<TViewModel>).FullName;
            }
            """);

    /// <summary>Verifies a MAUI <c>BindableProperty</c> registration whose only type-parameter use is the closed self-type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task BindablePropertyRegistrationIsCleanAsync() =>
        VerifyStaticGeneric.VerifyAnalyzerAsync(
            """
            namespace Microsoft.Maui.Controls
            {
                public sealed class BindableProperty
                {
                    private int _instance;

                    public static BindableProperty Create(string name, System.Type returnType, System.Type declaringType) => new();
                }
            }

            namespace App
            {
                using Microsoft.Maui.Controls;

                public class ReactiveView<TViewModel>
                    where TViewModel : class
                {
                    private int _instance;

                    public static readonly BindableProperty ViewModelProperty =
                        BindableProperty.Create("ViewModel", typeof(string), typeof(ReactiveView<TViewModel>));
                }
            }
            """);

    /// <summary>Verifies a forwarding property typed as a property-system registration type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task DependencyPropertyForwardingIsCleanAsync() =>
        VerifyStaticGeneric.VerifyAnalyzerAsync(
            """
            namespace System.Windows
            {
                public class DependencyProperty
                {
                }
            }

            namespace App
            {
                using System.Windows;

                public class ReactiveControl<TViewModel>
                    where TViewModel : class
                {
                    private int _instance;

                    public static DependencyProperty ViewModelProperty => Holder.ViewModelProperty;

                    private static class Holder
                    {
                        public static readonly DependencyProperty ViewModelProperty = new();
                    }
                }
            }
            """);

    /// <summary>Verifies an Avalonia registration whose type argument is the closed self-type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task AvaloniaRegistrationIsCleanAsync() =>
        VerifyStaticGeneric.VerifyAnalyzerAsync(
            """
            namespace Avalonia
            {
                public class AvaloniaProperty
                {
                    private int _instance;

                    public static StyledProperty<TValue> Register<TOwner, TValue>(string name) => new();
                }

                public sealed class StyledProperty<TValue> : AvaloniaProperty
                {
                }
            }

            namespace App
            {
                using Avalonia;

                public class ReactiveControl<T>
                    where T : class
                {
                    private int _instance;

                    public static readonly StyledProperty<string> NameProperty =
                        AvaloniaProperty.Register<ReactiveControl<T>, string>("Name");
                }
            }
            """);

    /// <summary>Verifies a registration type named in the editorconfig owner-type allow-list is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConfiguredOwnerTypeIsCleanAsync()
    {
        var test = new VerifyStaticGeneric.Test
        {
            TestCode = """
                       namespace Custom.Framework
                       {
                           public class PropertyKey
                           {
                           }
                       }

                       namespace App
                       {
                           using Custom.Framework;

                           public class Widget<T>
                           {
                               private int _instance;

                               public static PropertyKey NameKey => Holder.NameKey;

                               private static class Holder
                               {
                                   public static readonly PropertyKey NameKey = new();
                               }
                           }
                       }
                       """,
        };

        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            stylesharp.SST1431.additional_per_owner_types = Custom.Framework.PropertyKey

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies static fields and methods that ignore the type parameter are still reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task StaticMembersIgnoringTypeParameterStillReportedAsync() =>
        VerifyStaticGeneric.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public class Foo<T>
            {
                private int _instance;

                public static readonly Dictionary<string, string> {|SST1431:Map|} = new();

                public static int {|SST1431:Add|}(int a, int b) => a + b;
            }
            """);
}

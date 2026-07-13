// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    PerformanceSharp.Analyzers.Psh1011UseStateOverloadAnalyzer>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1011UseStateOverloadAnalyzer"/> (PSH1011 state overloads).</summary>
public class UseStateOverloadAnalyzerUnitTest
{
    /// <summary>Verifies a capturing ContinueWith lambda is reported; the state overload exists.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CapturingContinueWithLambdaIsReportedAsync()
        => await VerifyAsync(
            """
            using System;
            using System.Threading.Tasks;

            public class C
            {
                public void M(Task task, string label)
                    => task.ContinueWith({|PSH1011:t => Console.WriteLine(label)|});
            }
            """);

    /// <summary>Verifies a capturing UnsafeRegister callback is reported; the data fits the state argument.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CapturingRegisterCallbackIsReportedAsync()
        => await VerifyAsync(
            """
            using System;
            using System.Threading;

            public class C
            {
                public void M(CancellationToken token, IDisposable resource)
                    => token.Register({|PSH1011:() => resource.Dispose()|});
            }
            """);

    /// <summary>Verifies a capture-free lambda stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CaptureFreeLambdaIsCleanAsync()
        => await VerifyAsync(
            """
            using System;
            using System.Threading.Tasks;

            public class C
            {
                public void M(Task task)
                    => task.ContinueWith(static t => Console.WriteLine(t.Status));
            }
            """);

    /// <summary>Verifies a capturing lambda passed to an API with no state overload stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NoStateOverloadIsCleanAsync()
        => await VerifyAsync(
            """
            using System;
            using System.Collections.Generic;

            public class C
            {
                public void M(List<int> values, int threshold)
                    => values.RemoveAll(value => value > threshold);
            }
            """);

    /// <summary>Verifies a recursive scheduler callback is clean when no recursive state overload exists.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RecursiveSchedulerWithoutRecursiveStateOverloadIsCleanAsync()
        => await VerifyAsync(
            """
            using System;

            public interface IScheduler
            {
                IDisposable Schedule(Action<Action> work);

                IDisposable Schedule<TState>(TState state, Action<TState> work);
            }

            public static class Loop
            {
                public static IDisposable Run(IScheduler scheduler, IObserver<int> observer, int value)
                    => scheduler.Schedule(self =>
                    {
                        observer.OnNext(value);
                        self();
                    });
            }
            """);

    /// <summary>Verifies a keyed-DI factory lambda is clean; the sibling overload adds a service type, not state.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task KeyedServiceFactoryIsCleanAsync()
        => await VerifyAsync(
            """
            #nullable enable
            using System;

            public interface IServiceCollection;

            public static class KeyedRegistration
            {
                public static IServiceCollection AddKeyedSingleton<TService>(
                    this IServiceCollection services,
                    object? serviceKey,
                    Func<IServiceProvider, object?, TService> implementationFactory)
                    where TService : class => services;

                public static IServiceCollection AddKeyedSingleton(
                    this IServiceCollection services,
                    Type serviceType,
                    object? serviceKey,
                    Func<IServiceProvider, object?, object> implementationFactory) => services;
            }

            public sealed class Holder(string setting)
            {
                public string Setting { get; } = setting;
            }

            public static class C
            {
                public static void Add(IServiceCollection services, string key, string setting)
                    => services.AddKeyedSingleton(key, (provider, _) => new Holder(setting));
            }
            """);

    /// <summary>Verifies an overload that genuinely adds an object state parameter is still reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AddedObjectStateOverloadIsReportedAsync()
        => await VerifyAsync(
            """
            #nullable enable
            using System;

            public static class Registry
            {
                public static void Run(Action<int> callback) { }

                public static void Run(Action<int, object?> callback, object? state) { }
            }

            public static class C
            {
                public static void M(string label)
                    => Registry.Run({|PSH1011:value => Console.WriteLine(label)|});
            }
            """);

    /// <summary>Runs a verification against the .NET 9 reference assemblies.</summary>
    /// <summary>Verifies a capture-free callback is not reported when a sibling lambda captures.</summary>
    /// <remarks>
    /// A neighbour's capture is not this lambda's. Reading <c>Captured</c> — which folds in what the other
    /// lambdas in the method closed over — charged this callback with its neighbour's state and told it to
    /// move something it never had.
    /// </remarks>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CaptureFreeCallbackBesideACapturingSiblingIsNotReportedAsync()
        => await VerifyAsync(
            """
            using System;
            using System.Threading;

            public class C
            {
                public void M(CancellationToken token, IDisposable resource)
                {
                    Action capturing = () => resource.Dispose();
                    token.Register(static () => Console.WriteLine("done"));
                    capturing();
                }
            }
            """);

    /// <summary>Runs an analyzer verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The test source.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
        };
        await test.RunAsync(CancellationToken.None);
    }
}

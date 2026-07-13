// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Flags a stream read or write made through the array-based async overloads (PSH1314):
/// <c>stream.ReadAsync(buffer, offset, count)</c> should be
/// <c>stream.ReadAsync(buffer.AsMemory(offset, count))</c>. The array overloads allocate state to
/// carry the array, offset and count across the await; the <c>Memory&lt;byte&gt;</c> overloads
/// carry all three in the value itself and return a <c>ValueTask</c>, so a call that completes
/// synchronously — on a buffered stream, most of them — allocates nothing.
/// <para>
/// The memory overloads are .NET Core 2.1+, so they are never assumed: <c>Memory&lt;byte&gt;</c>,
/// <c>ReadOnlyMemory&lt;byte&gt;</c>, and a <c>Stream.ReadAsync</c> that actually takes one are
/// all resolved at compilation start, and the rule registers nothing when the framework has none.
/// The replacement overload is then resolved off the receiver's own type hierarchy, so a stream
/// that does not expose one is not reported.
/// </para>
/// <para>
/// Only a directly awaited call is reported. The array overloads return <c>Task</c> and the memory
/// ones return <c>ValueTask</c>, so rewriting a call whose task is stored in a variable would
/// change that variable's type; awaiting in place is the only shape where the swap is invisible.
/// </para>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1314UseMemoryBasedStreamOverloadsAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The reading member name the syntax gate accepts.</summary>
    internal const string ReadAsyncMethodName = "ReadAsync";

    /// <summary>The writing member name the syntax gate accepts.</summary>
    internal const string WriteAsyncMethodName = "WriteAsync";

    /// <summary>The name of the array extension the code fix rewrites through.</summary>
    internal const string AsMemoryMethodName = "AsMemory";

    /// <summary>The continuation-configuring call that may sit between the invocation and its await.</summary>
    private const string ConfigureAwaitMethodName = "ConfigureAwait";

    /// <summary>The argument count of the array overload without a cancellation token.</summary>
    private const int ArrayArgumentCount = 3;

    /// <summary>The argument count of the array overload with a cancellation token.</summary>
    private const int ArrayWithTokenArgumentCount = 4;

    /// <summary>The index of the count parameter in the array overload.</summary>
    private const int CountParameterIndex = 2;

    /// <summary>The metadata name of the stream type the reported calls are made on.</summary>
    private const string StreamMetadataName = "System.IO.Stream";

    /// <summary>The metadata name of the memory type the reading overload takes.</summary>
    private const string MemoryMetadataName = "System.Memory`1";

    /// <summary>The metadata name of the memory type the writing overload takes.</summary>
    private const string ReadOnlyMemoryMetadataName = "System.ReadOnlyMemory`1";

    /// <summary>The metadata name of the cancellation token type the array overload may take.</summary>
    private const string CancellationTokenMetadataName = "System.Threading.CancellationToken";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ConcurrencyRules.UseMemoryBasedStreamOverloads);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            if (TryCreateGate(start.Compilation) is not { } gate)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeInvocation(nodeContext, gate), SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Returns whether an invocation has the array-based stream call shape, before any binding.</summary>
    /// <param name="invocation">The invocation to inspect.</param>
    /// <returns><see langword="true"/> when the member name and argument count match an array overload.</returns>
    internal static bool IsArrayOverloadShape(InvocationExpressionSyntax invocation)
        => invocation.ArgumentList.Arguments.Count is ArrayArgumentCount or ArrayWithTokenArgumentCount
            && invocation.Expression is MemberAccessExpressionSyntax access
            && access.Name.Identifier.ValueText is ReadAsyncMethodName or WriteAsyncMethodName;

    /// <summary>Returns whether an invocation's value is awaited where it stands.</summary>
    /// <param name="invocation">The invocation to inspect.</param>
    /// <returns><see langword="true"/> when the call is awaited directly, or through a ConfigureAwait.</returns>
    internal static bool IsDirectlyAwaited(InvocationExpressionSyntax invocation)
    {
        if (invocation.Parent is AwaitExpressionSyntax)
        {
            return true;
        }

        return invocation.Parent is MemberAccessExpressionSyntax { Name.Identifier.ValueText: ConfigureAwaitMethodName } access
            && access.Parent is InvocationExpressionSyntax configured
            && configured.Parent is AwaitExpressionSyntax;
    }

    /// <summary>Finds the memory-based overload of a stream call, searching the receiver's own type hierarchy.</summary>
    /// <param name="containingType">The type declaring the array-based overload that was called.</param>
    /// <param name="name">The call's member name.</param>
    /// <param name="memoryType">The memory type the replacement must take first.</param>
    /// <returns>The memory-based overload, or <see langword="null"/> when the stream has none.</returns>
    internal static IMethodSymbol? TryFindMemoryOverload(INamedTypeSymbol containingType, string name, ITypeSymbol memoryType)
    {
        for (var current = containingType; current is not null; current = current.BaseType)
        {
            var members = current.GetMembers(name);
            for (var i = 0; i < members.Length; i++)
            {
                if (members[i] is IMethodSymbol { Parameters.Length: > 0 } candidate
                    && SymbolEqualityComparer.Default.Equals(candidate.Parameters[0].Type, memoryType)
                    && TrailingParametersOptional(candidate.Parameters))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    /// <summary>Resolves the types the rule needs, or nothing when the framework has no memory overloads.</summary>
    /// <param name="compilation">The compilation being analyzed.</param>
    /// <returns>The gate state, or <see langword="null"/> when the rule cannot apply.</returns>
    private static MemoryOverloadGate? TryCreateGate(Compilation compilation)
    {
        var byteType = compilation.GetSpecialType(SpecialType.System_Byte);
        if (compilation.GetTypeByMetadataName(StreamMetadataName) is not { } streamType
            || compilation.GetTypeByMetadataName(MemoryMetadataName) is not { } memory
            || compilation.GetTypeByMetadataName(ReadOnlyMemoryMetadataName) is not { } readOnlyMemory
            || compilation.GetTypeByMetadataName(CancellationTokenMetadataName) is not { } cancellationToken)
        {
            return null;
        }

        var memoryOfByte = memory.Construct(byteType);
        var readOnlyMemoryOfByte = readOnlyMemory.Construct(byteType);
        return TryFindMemoryOverload(streamType, ReadAsyncMethodName, memoryOfByte) is null
            ? null
            : new MemoryOverloadGate(streamType, memoryOfByte, readOnlyMemoryOfByte, cancellationToken);
    }

    /// <summary>Reports PSH1314 for an awaited array-based stream call whose memory overload exists.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="gate">The per-compilation gate state.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, MemoryOverloadGate gate)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (!IsArrayOverloadShape(invocation)
            || !IsDirectlyAwaited(invocation)
            || context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || !IsStreamType(method.ContainingType, gate.StreamType)
            || !TakesArrayOffsetCount(method.Parameters, gate.CancellationTokenType))
        {
            return;
        }

        var memoryType = method.Name == ReadAsyncMethodName ? gate.MemoryOfByte : gate.ReadOnlyMemoryOfByte;
        if (TryFindMemoryOverload(method.ContainingType, method.Name, memoryType) is null)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            ConcurrencyRules.UseMemoryBasedStreamOverloads,
            invocation.SyntaxTree,
            invocation.Span,
            method.Name));
    }

    /// <summary>Returns whether a type is <c>System.IO.Stream</c> or derives from it.</summary>
    /// <param name="type">The type declaring the called overload.</param>
    /// <param name="streamType">The stream type in the current compilation.</param>
    /// <returns><see langword="true"/> when the call really is a stream call.</returns>
    private static bool IsStreamType(INamedTypeSymbol type, INamedTypeSymbol streamType)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, streamType))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a bound method takes the array, offset and count the rule replaces.</summary>
    /// <param name="parameters">The bound method's parameters.</param>
    /// <param name="cancellationTokenType">The cancellation token type in the current compilation.</param>
    /// <returns><see langword="true"/> when the signature is (byte[], int, int) with an optional trailing token.</returns>
    private static bool TakesArrayOffsetCount(ImmutableArray<IParameterSymbol> parameters, INamedTypeSymbol cancellationTokenType)
    {
        if (parameters.Length is not (ArrayArgumentCount or ArrayWithTokenArgumentCount)
            || parameters[0].Type is not IArrayTypeSymbol { ElementType.SpecialType: SpecialType.System_Byte }
            || parameters[1].Type.SpecialType != SpecialType.System_Int32
            || parameters[CountParameterIndex].Type.SpecialType != SpecialType.System_Int32)
        {
            return false;
        }

        return parameters.Length == ArrayArgumentCount
            || SymbolEqualityComparer.Default.Equals(parameters[ArrayArgumentCount].Type, cancellationTokenType);
    }

    /// <summary>Returns whether every parameter after the memory one is optional, so the rewritten call still binds.</summary>
    /// <param name="parameters">The candidate overload's parameters.</param>
    /// <returns><see langword="true"/> when only the memory parameter is required.</returns>
    private static bool TrailingParametersOptional(ImmutableArray<IParameterSymbol> parameters)
    {
        for (var i = 1; i < parameters.Length; i++)
        {
            if (!parameters[i].IsOptional)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The types resolved once per compilation, proving the memory overloads exist before anything is suggested.</summary>
    /// <param name="StreamType">The stream type the reported calls are made on.</param>
    /// <param name="MemoryOfByte">The memory type the reading overload takes.</param>
    /// <param name="ReadOnlyMemoryOfByte">The memory type the writing overload takes.</param>
    /// <param name="CancellationTokenType">The cancellation token type the array overload may take.</param>
    private readonly record struct MemoryOverloadGate(
        INamedTypeSymbol StreamType,
        INamedTypeSymbol MemoryOfByte,
        INamedTypeSymbol ReadOnlyMemoryOfByte,
        INamedTypeSymbol CancellationTokenType);
}

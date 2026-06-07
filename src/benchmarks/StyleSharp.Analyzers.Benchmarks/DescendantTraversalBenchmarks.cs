// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Micro-benchmarks for the remaining descendant-traversal replacements.</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class DescendantTraversalBenchmarks
{
    /// <summary>The file-name benchmark root.</summary>
    private CompilationUnitSyntax _fileNameRoot = null!;

    /// <summary>The file-type/namespace benchmark root.</summary>
    private CompilationUnitSyntax _fileTypeNamespaceRoot = null!;

    /// <summary>The XML element benchmark root.</summary>
    private XmlElementSyntax _xmlSummary = null!;

    /// <summary>The local-shadowing benchmark scope.</summary>
    private BlockSyntax _localScope = null!;

    /// <summary>The position after which locals no longer matter for the shadowing scan.</summary>
    private int _localPosition;

    /// <summary>The type scanned by the lock-field benchmark.</summary>
    private TypeDeclarationSyntax _lockType = null!;

    /// <summary>The semantic model for the lock-field benchmark.</summary>
    private SemanticModel _lockModel = null!;

    /// <summary>The candidate lock field symbol.</summary>
    private IFieldSymbol _lockFieldSymbol = null!;

    /// <summary>The property scanned by the field-keyword benchmark.</summary>
    private PropertyDeclarationSyntax _property = null!;

    /// <summary>The semantic model for the field-keyword benchmark.</summary>
    private SemanticModel _propertyModel = null!;

    /// <summary>The backing field symbol referenced by the benchmark property.</summary>
    private IFieldSymbol _propertyFieldSymbol = null!;

    /// <summary>Builds the benchmark fixtures once.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _fileNameRoot = (CompilationUnitSyntax)BenchmarkCompilationFactory.Parse(
            "namespace Outer.Inner { public class Widget { private sealed class Nested { } } } public delegate void After();").GetRoot();
        _fileTypeNamespaceRoot = (CompilationUnitSyntax)BenchmarkCompilationFactory.Parse(
            "namespace A { internal partial class First<T> { } internal partial class First<T> { } namespace B { internal class Second { } } } namespace C { } internal class Third { } ").GetRoot();

        var xmlRoot = BenchmarkCompilationFactory.Parse(
            """
            /// <summary><see cref="C"/><inheritdoc/></summary>
            public class C { }
            """).GetRoot();
        _xmlSummary = (XmlElementSyntax)xmlRoot.DescendantNodes(descendIntoTrivia: true)
            .First(static node => node is XmlElementSyntax element && element.StartTag.Name.LocalName.ValueText == "summary");

        var localRoot = (CompilationUnitSyntax)BenchmarkCompilationFactory.Parse(
            "class C { void M() { var first = 0; foreach (var item in new[] { 1, 2 }) { } try { } catch (System.Exception error) { } var target = 3; System.Console.WriteLine(target); } }").GetRoot();
        var method = (MethodDeclarationSyntax)localRoot.DescendantNodes().First(static node => node is MethodDeclarationSyntax);
        _localScope = method.Body!;
        _localPosition = _localScope.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .First(static identifier => identifier.Identifier.ValueText == "target")
            .SpanStart;

        var (lockTree, lockCompilation) = BenchmarkCompilationFactory.CreateCompilation(
            "class C { private readonly object _gate = new(); void M() { lock (_gate) { } lock (_gate) { } } }");
        _lockType = (TypeDeclarationSyntax)((CompilationUnitSyntax)lockTree.GetRoot()).Members[0];
        _lockModel = lockCompilation.GetSemanticModel(lockTree);
        _lockFieldSymbol = (IFieldSymbol)_lockModel.GetDeclaredSymbol(
            ((FieldDeclarationSyntax)_lockType.Members[0]).Declaration.Variables[0])!;

        var (propertyTree, propertyCompilation) = BenchmarkCompilationFactory.CreateCompilation(
            "class C { private int _value; public int Value { get => _value; set => _value = value < 0 ? 0 : value; } }");
        var propertyRoot = (CompilationUnitSyntax)propertyTree.GetRoot();
        _property = (PropertyDeclarationSyntax)propertyRoot.DescendantNodes().First(static node => node is PropertyDeclarationSyntax);
        _propertyModel = propertyCompilation.GetSemanticModel(propertyTree);
        _propertyFieldSymbol = (IFieldSymbol)_propertyModel.GetDeclaredSymbol(
            ((FieldDeclarationSyntax)((TypeDeclarationSyntax)propertyRoot.Members[0]).Members[0]).Declaration.Variables[0])!;
    }

    /// <summary>Benchmarks the original first-type scan.</summary>
    /// <returns>Whether the file contains a first eligible type-like declaration.</returns>
    [Benchmark(Baseline = true)]
    public bool FileName_Baseline() => BaselineTryGetFirstTypeIdentifier(_fileNameRoot, out _);

    /// <summary>Benchmarks the helper-driven first-type scan.</summary>
    /// <returns>Whether the file contains a first eligible type-like declaration.</returns>
    [Benchmark]
    public bool FileName_Optimized() => OptimizedTryGetFirstTypeIdentifier(_fileNameRoot, out _);

    /// <summary>Benchmarks the original file-type/namespace scan.</summary>
    /// <returns>The number of excess namespaces or types found.</returns>
    [Benchmark]
    public int FileTypeNamespace_Baseline() => BaselineCountTopLevelDiagnostics(_fileTypeNamespaceRoot);

    /// <summary>Benchmarks the direct-member file-type/namespace scan.</summary>
    /// <returns>The number of excess namespaces or types found.</returns>
    [Benchmark]
    public int FileTypeNamespace_Optimized() => OptimizedCountTopLevelDiagnostics(_fileTypeNamespaceRoot);

    /// <summary>Benchmarks the original XML inheritdoc scan.</summary>
    /// <returns>Whether an inheritdoc descendant exists.</returns>
    [Benchmark]
    public bool XmlDocumentation_Baseline() => BaselineContainsInheritDoc(_xmlSummary);

    /// <summary>Benchmarks the helper-driven XML inheritdoc scan.</summary>
    /// <returns>Whether an inheritdoc descendant exists.</returns>
    [Benchmark]
    public bool XmlDocumentation_Optimized() => OptimizedContainsInheritDoc(_xmlSummary);

    /// <summary>Benchmarks the original earlier-local scan.</summary>
    /// <returns>Whether a matching earlier local exists.</returns>
    [Benchmark]
    public bool EarlierLocal_Baseline() => BaselineHasEarlierLocalNamed(_localScope, _localPosition, "target");

    /// <summary>Benchmarks the helper-driven earlier-local scan.</summary>
    /// <returns>Whether a matching earlier local exists.</returns>
    [Benchmark]
    public bool EarlierLocal_Optimized() => OptimizedHasEarlierLocalNamed(_localScope, _localPosition, "target");

    /// <summary>Benchmarks the original lock-field reference scan.</summary>
    /// <returns>Whether the candidate remains lock-only.</returns>
    [Benchmark]
    public bool PreferLockType_Baseline() => BaselineScanLockFieldReferences(_lockType, _lockModel, _lockFieldSymbol);

    /// <summary>Benchmarks the helper-driven lock-field reference scan.</summary>
    /// <returns>Whether the candidate remains lock-only.</returns>
    [Benchmark]
    public bool PreferLockType_Optimized() => OptimizedScanLockFieldReferences(_lockType, _lockModel, _lockFieldSymbol);

    /// <summary>Benchmarks the original property backing-field collection.</summary>
    /// <returns>The number of references collected for rewriting.</returns>
    [Benchmark]
    public int PreferFieldKeyword_Baseline() => BaselineCollectFieldReferences(_property, _propertyModel, _propertyFieldSymbol);

    /// <summary>Benchmarks the helper-driven property backing-field collection.</summary>
    /// <returns>The number of references collected for rewriting.</returns>
    [Benchmark]
    public int PreferFieldKeyword_Optimized() => OptimizedCollectFieldReferences(_property, _propertyModel, _propertyFieldSymbol);

    /// <summary>Mirrors the original first-type scan.</summary>
    /// <param name="root">The file root.</param>
    /// <param name="identifier">The discovered identifier.</param>
    /// <returns><see langword="true"/> when a type-like declaration is found.</returns>
    private static bool BaselineTryGetFirstTypeIdentifier(SyntaxNode root, out SyntaxToken identifier)
    {
        identifier = default;
        foreach (var node in root.DescendantNodes())
        {
            switch (node)
            {
                case BaseTypeDeclarationSyntax type:
                    {
                        if (ModifierListHelper.Contains(type.Modifiers, SyntaxKind.PartialKeyword))
                        {
                            return false;
                        }

                        identifier = type.Identifier;
                        return true;
                    }

                case DelegateDeclarationSyntax @delegate:
                    {
                        identifier = @delegate.Identifier;
                        return true;
                    }
            }
        }

        return false;
    }

    /// <summary>Mirrors the helper-driven first-type scan.</summary>
    /// <param name="root">The file root.</param>
    /// <param name="identifier">The discovered identifier.</param>
    /// <returns><see langword="true"/> when a type-like declaration is found.</returns>
    private static bool OptimizedTryGetFirstTypeIdentifier(SyntaxNode root, out SyntaxToken identifier)
    {
        var state = (Found: false, Identifier: default(SyntaxToken));
        DescendantTraversalHelper.VisitDescendants<SyntaxNode, (bool Found, SyntaxToken Identifier)>(root, ref state, VisitTypeLikeDeclaration);
        identifier = state.Identifier;
        return state.Found;
    }

    /// <summary>Mirrors the original top-level namespace/type scan.</summary>
    /// <param name="root">The file root.</param>
    /// <returns>The number of excess namespaces or types.</returns>
    private static int BaselineCountTopLevelDiagnostics(CompilationUnitSyntax root)
    {
        var typeKeys = new HashSet<string>(StringComparer.Ordinal);
        var firstTypeSeen = false;
        var firstNamespaceSeen = false;
        var diagnostics = 0;

        foreach (var node in root.DescendantNodes(static descend => descend is CompilationUnitSyntax or BaseNamespaceDeclarationSyntax))
        {
            switch (node)
            {
                case BaseNamespaceDeclarationSyntax:
                    {
                        if (firstNamespaceSeen)
                        {
                            diagnostics++;
                        }

                        firstNamespaceSeen = true;
                        break;
                    }

                case BaseTypeDeclarationSyntax type when typeKeys.Add(TypeKey(type)):
                    {
                        if (firstTypeSeen)
                        {
                            diagnostics++;
                        }

                        firstTypeSeen = true;
                        break;
                    }
            }
        }

        return diagnostics;
    }

    /// <summary>Mirrors the direct-member namespace/type scan.</summary>
    /// <param name="root">The file root.</param>
    /// <returns>The number of excess namespaces or types.</returns>
    private static int OptimizedCountTopLevelDiagnostics(CompilationUnitSyntax root)
    {
        var typeKeys = new HashSet<string>(StringComparer.Ordinal);
        var firstTypeSeen = false;
        var firstNamespaceSeen = false;
        var diagnostics = 0;

        ScanMembers(root.Members, typeKeys, ref firstTypeSeen, ref firstNamespaceSeen, ref diagnostics);
        return diagnostics;
    }

    /// <summary>Mirrors the original XML inheritdoc scan.</summary>
    /// <param name="element">The XML element to inspect.</param>
    /// <returns><see langword="true"/> when an inheritdoc descendant exists.</returns>
    private static bool BaselineContainsInheritDoc(XmlNodeSyntax element)
    {
        foreach (var descendant in element.DescendantNodes())
        {
            if (descendant is XmlNodeSyntax node && GetElementName(node) == "inheritdoc")
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Mirrors the helper-driven XML inheritdoc scan.</summary>
    /// <param name="element">The XML element to inspect.</param>
    /// <returns><see langword="true"/> when an inheritdoc descendant exists.</returns>
    private static bool OptimizedContainsInheritDoc(XmlNodeSyntax element)
    {
        var found = false;
        DescendantTraversalHelper.VisitDescendants<XmlNodeSyntax, bool>(element, ref found, VisitInheritDocNode);
        return found;
    }

    /// <summary>Mirrors the original earlier-local scan.</summary>
    /// <param name="scope">The scope to inspect.</param>
    /// <param name="position">The identifier position.</param>
    /// <param name="name">The identifier name.</param>
    /// <returns><see langword="true"/> when a matching earlier local exists.</returns>
    private static bool BaselineHasEarlierLocalNamed(SyntaxNode scope, int position, string name)
    {
        foreach (var node in scope.DescendantNodes())
        {
            if (node.SpanStart >= position)
            {
                continue;
            }

            switch (node)
            {
                case VariableDeclaratorSyntax variable when variable.Identifier.ValueText == name:
                case SingleVariableDesignationSyntax designation when designation.Identifier.ValueText == name:
                case ForEachStatementSyntax foreachStatement when foreachStatement.Identifier.ValueText == name:
                case CatchDeclarationSyntax catchDeclaration when catchDeclaration.Identifier.ValueText == name:
                    return true;
            }
        }

        return false;
    }

    /// <summary>Mirrors the helper-driven earlier-local scan.</summary>
    /// <param name="scope">The scope to inspect.</param>
    /// <param name="position">The identifier position.</param>
    /// <param name="name">The identifier name.</param>
    /// <returns><see langword="true"/> when a matching earlier local exists.</returns>
    private static bool OptimizedHasEarlierLocalNamed(SyntaxNode scope, int position, string name)
    {
        var state = (Position: position, Name: name, Found: false);
        DescendantTraversalHelper.VisitDescendants<SyntaxNode, (int Position, string Name, bool Found)>(scope, ref state, VisitEarlierLocalCandidate);
        return state.Found;
    }

    /// <summary>Mirrors the original lock-field scan.</summary>
    /// <param name="type">The declaring type.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="fieldSymbol">The candidate field symbol.</param>
    /// <returns><see langword="true"/> when the candidate remains lock-only.</returns>
    private static bool BaselineScanLockFieldReferences(TypeDeclarationSyntax type, SemanticModel model, IFieldSymbol fieldSymbol)
    {
        var candidate = new LockCandidateState(fieldSymbol);
        var candidates = new Dictionary<string, LockCandidateState>(1, StringComparer.Ordinal)
        {
            [fieldSymbol.Name] = candidate
        };

        foreach (var node in type.DescendantNodes())
        {
            if (node is not IdentifierNameSyntax identifier)
            {
                continue;
            }

            if (!candidates.TryGetValue(identifier.Identifier.ValueText, out var current)
                || !SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(identifier).Symbol, current.FieldSymbol))
            {
                continue;
            }

            if (IsLockTarget(identifier))
            {
                current.HasLockUse = true;
                continue;
            }

            current.HasNonLockUse = true;
        }

        return candidate.HasLockUse && !candidate.HasNonLockUse;
    }

    /// <summary>Mirrors the helper-driven lock-field scan.</summary>
    /// <param name="type">The declaring type.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="fieldSymbol">The candidate field symbol.</param>
    /// <returns><see langword="true"/> when the candidate remains lock-only.</returns>
    private static bool OptimizedScanLockFieldReferences(TypeDeclarationSyntax type, SemanticModel model, IFieldSymbol fieldSymbol)
    {
        var candidate = new LockCandidateState(fieldSymbol);
        var candidates = new Dictionary<string, LockCandidateState>(1, StringComparer.Ordinal)
        {
            [fieldSymbol.Name] = candidate
        };
        var state = (Model: model, Candidates: candidates);

        DescendantTraversalHelper.VisitDescendants<IdentifierNameSyntax, (SemanticModel Model, Dictionary<string, LockCandidateState> Candidates)>(
            type,
            ref state,
            VisitLockFieldReference);

        return candidate.HasLockUse && !candidate.HasNonLockUse;
    }

    /// <summary>Mirrors the original property backing-field collection.</summary>
    /// <param name="property">The property to inspect.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="symbol">The backing field symbol.</param>
    /// <returns>The number of references collected.</returns>
    private static int BaselineCollectFieldReferences(PropertyDeclarationSyntax property, SemanticModel model, IFieldSymbol symbol)
    {
        var references = new List<IdentifierNameSyntax>(4);
        foreach (var node in property.DescendantNodes())
        {
            if (node is IdentifierNameSyntax identifier
                && SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(identifier).Symbol, symbol))
            {
                references.Add(identifier);
            }
        }

        return references.Count;
    }

    /// <summary>Mirrors the helper-driven property backing-field collection.</summary>
    /// <param name="property">The property to inspect.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="symbol">The backing field symbol.</param>
    /// <returns>The number of references collected.</returns>
    private static int OptimizedCollectFieldReferences(PropertyDeclarationSyntax property, SemanticModel model, IFieldSymbol symbol)
    {
        var references = new List<IdentifierNameSyntax>(4);
        var state = (Model: model, Symbol: symbol, References: references);

        DescendantTraversalHelper.VisitDescendants<IdentifierNameSyntax, (SemanticModel Model, IFieldSymbol Symbol, List<IdentifierNameSyntax> References)>(
            property,
            ref state,
            CollectFieldReference);

        return references.Count;
    }

    /// <summary>Visits type-like declarations in preorder.</summary>
    /// <param name="node">The visited node.</param>
    /// <param name="state">The discovery state.</param>
    /// <returns><see langword="true"/> to continue scanning.</returns>
    private static bool VisitTypeLikeDeclaration(SyntaxNode node, ref (bool Found, SyntaxToken Identifier) state)
    {
        switch (node)
        {
            case BaseTypeDeclarationSyntax type:
                {
                    if (ModifierListHelper.Contains(type.Modifiers, SyntaxKind.PartialKeyword))
                    {
                        return false;
                    }

                    state = (true, type.Identifier);
                    return false;
                }

            case DelegateDeclarationSyntax @delegate:
                {
                    state = (true, @delegate.Identifier);
                    return false;
                }

            default:
                return true;
        }
    }

    /// <summary>Visits XML nodes until an inheritdoc element is found.</summary>
    /// <param name="node">The visited node.</param>
    /// <param name="found">Whether an inheritdoc element has been found.</param>
    /// <returns><see langword="true"/> to continue scanning.</returns>
    private static bool VisitInheritDocNode(XmlNodeSyntax node, ref bool found)
    {
        if (GetElementName(node) != "inheritdoc")
        {
            return true;
        }

        found = true;
        return false;
    }

    /// <summary>Visits possible local-shadowing declarations until a match is found.</summary>
    /// <param name="node">The visited node.</param>
    /// <param name="state">The search state.</param>
    /// <returns><see langword="true"/> to continue scanning.</returns>
    private static bool VisitEarlierLocalCandidate(SyntaxNode node, ref (int Position, string Name, bool Found) state)
    {
        if (node.SpanStart >= state.Position)
        {
            return true;
        }

        switch (node)
        {
            case VariableDeclaratorSyntax variable when variable.Identifier.ValueText == state.Name:
            case SingleVariableDesignationSyntax designation when designation.Identifier.ValueText == state.Name:
            case ForEachStatementSyntax foreachStatement when foreachStatement.Identifier.ValueText == state.Name:
            case CatchDeclarationSyntax catchDeclaration when catchDeclaration.Identifier.ValueText == state.Name:
                {
                    state = (state.Position, state.Name, true);
                    return false;
                }

            default:
                return true;
        }
    }

    /// <summary>Visits lock-field references and records lock-only usage state.</summary>
    /// <param name="identifier">The visited identifier.</param>
    /// <param name="state">The scan state.</param>
    /// <returns><see langword="true"/> to continue scanning.</returns>
    private static bool VisitLockFieldReference(
        IdentifierNameSyntax identifier,
        ref (SemanticModel Model, Dictionary<string, LockCandidateState> Candidates) state)
    {
        if (!state.Candidates.TryGetValue(identifier.Identifier.ValueText, out var candidate)
            || !SymbolEqualityComparer.Default.Equals(state.Model.GetSymbolInfo(identifier).Symbol, candidate.FieldSymbol))
        {
            return true;
        }

        if (IsLockTarget(identifier))
        {
            candidate.HasLockUse = true;
            return true;
        }

        candidate.HasNonLockUse = true;
        return true;
    }

    /// <summary>Visits property references and records matching backing-field uses.</summary>
    /// <param name="identifier">The visited identifier.</param>
    /// <param name="state">The collection state.</param>
    /// <returns><see langword="true"/> to continue scanning.</returns>
    private static bool CollectFieldReference(
        IdentifierNameSyntax identifier,
        ref (SemanticModel Model, IFieldSymbol Symbol, List<IdentifierNameSyntax> References) state)
    {
        if (!SymbolEqualityComparer.Default.Equals(state.Model.GetSymbolInfo(identifier).Symbol, state.Symbol))
        {
            return true;
        }

        state.References.Add(identifier);
        return true;
    }

    /// <summary>Scans direct members and nested namespaces without descending into type bodies.</summary>
    /// <param name="members">The members to inspect.</param>
    /// <param name="typeKeys">The distinct type keys seen so far.</param>
    /// <param name="firstTypeSeen">Whether the first distinct type has already been seen.</param>
    /// <param name="firstNamespaceSeen">Whether the first namespace has already been seen.</param>
    /// <param name="diagnostics">The count of excess namespaces or types.</param>
    private static void ScanMembers(
        SyntaxList<MemberDeclarationSyntax> members,
        HashSet<string> typeKeys,
        ref bool firstTypeSeen,
        ref bool firstNamespaceSeen,
        ref int diagnostics)
    {
        for (var i = 0; i < members.Count; i++)
        {
            switch (members[i])
            {
                case BaseNamespaceDeclarationSyntax namespaceDeclaration:
                    {
                        if (firstNamespaceSeen)
                        {
                            diagnostics++;
                        }

                        firstNamespaceSeen = true;
                        ScanMembers(namespaceDeclaration.Members, typeKeys, ref firstTypeSeen, ref firstNamespaceSeen, ref diagnostics);
                        break;
                    }

                case BaseTypeDeclarationSyntax type when typeKeys.Add(TypeKey(type)):
                    {
                        if (firstTypeSeen)
                        {
                            diagnostics++;
                        }

                        firstTypeSeen = true;
                        break;
                    }
            }
        }
    }

    /// <summary>Builds the type uniqueness key used by the analyzer.</summary>
    /// <param name="type">The type declaration.</param>
    /// <returns>The name-and-arity key.</returns>
    private static string TypeKey(BaseTypeDeclarationSyntax type)
    {
        var arity = type is TypeDeclarationSyntax declaration ? declaration.TypeParameterList?.Parameters.Count ?? 0 : 0;
        return $"{type.Identifier.ValueText}`{arity}";
    }

    /// <summary>Returns the XML element name for supported XML node kinds.</summary>
    /// <param name="node">The XML node.</param>
    /// <returns>The XML element name, or <see langword="null"/>.</returns>
    private static string? GetElementName(XmlNodeSyntax node)
        => node switch
        {
            XmlElementSyntax element => element.StartTag.Name.LocalName.ValueText,
            XmlEmptyElementSyntax element => element.Name.LocalName.ValueText,
            _ => null
        };

    /// <summary>Returns whether an identifier is the expression locked by a lock statement.</summary>
    /// <param name="identifier">The identifier to inspect.</param>
    /// <returns><see langword="true"/> when the identifier is a lock target.</returns>
    private static bool IsLockTarget(IdentifierNameSyntax identifier)
    {
        ExpressionSyntax expression = identifier;
        if (identifier.Parent is MemberAccessExpressionSyntax access && access.Name == identifier)
        {
            expression = access;
        }

        return expression.Parent is LockStatementSyntax lockStatement && lockStatement.Expression == expression;
    }

    /// <summary>Tracks lock-only usage state for a benchmark candidate field.</summary>
    private sealed class LockCandidateState
    {
        /// <summary>Initializes a new instance of the <see cref="LockCandidateState"/> class.</summary>
        /// <param name="fieldSymbol">The candidate field symbol.</param>
        public LockCandidateState(IFieldSymbol fieldSymbol)
        {
            FieldSymbol = fieldSymbol;
        }

        /// <summary>Gets the candidate field symbol.</summary>
        public IFieldSymbol FieldSymbol { get; }

        /// <summary>Gets or sets a value indicating whether a qualifying lock use was found.</summary>
        public bool HasLockUse { get; set; }

        /// <summary>Gets or sets a value indicating whether a non-lock use was found.</summary>
        public bool HasNonLockUse { get; set; }
    }
}

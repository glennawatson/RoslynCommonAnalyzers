using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;

namespace RoslynCommonAnalyzers
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RCGS0001ParametersMustBeOnUniqueLinesCodeFixProvider)), Shared]
    public class RCGS0001ParametersMustBeOnUniqueLinesCodeFixProvider : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(RCGS0001BaseMethodDeclarationsParametersMustBeOnUniqueLinesAnalyzer.DiagnosticId); }
        }

        public sealed override FixAllProvider GetFixAllProvider()
        {
            // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            // TODO: Replace the following code with your own analysis, generating a CodeAction for each fix to suggest
            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            // Find the type declaration identified by the diagnostic.
            var declaration = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType<SyntaxNode>().First();

            // Register a code action that will invoke the fix.
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: CodeFixResources.RCGS0001CodeFixTitle,
                    createChangedSolution: c => MakeUppercaseAsync(context.Document, declaration, c),
                    equivalenceKey: nameof(CodeFixResources.RCGS0001CodeFixTitle)),
                diagnostic);
        }

        private async Task<Solution> FixSpacingForMethods(Document document, MethodDeclarationSyntax syntax, CancellationToken cancellationToken)
        {
            var newSyntax = syntax.WithParameterList(
                                syntax.ParameterList.WithParameters(
                                    SyntaxFactory.SeparatedList(
                                        syntax.ParameterList.Parameters.Select(
                                            p => p.WithLeadingTrivia(SyntaxFactory.CarriageReturnLineFeed)))));
            var newDocument = document.WithSyntaxRoot(newSyntax);
            return await MakeUniqueLines(newDocument, newSyntax, cancellationToken).ConfigureAwait(false);
        }

        private static T? ConvertNodeIfAble<T, TParam>(T node, Func<T, List<TParam>> converterToList, Func<T, SeparatedSyntaxList<TParam>, T> addParameters)
            where T : SyntaxNode
            where TParam : SyntaxNode
        {
            var list = converterToList(node);

            // Check if all arguments are on the same line as the method call
            if (list.Count > 1 && !list.All(p => p.GetLocation().GetLineSpan().StartLinePosition.Line == node.GetLocation().GetLineSpan().StartLinePosition.Line))
            {
                // Calculate the number of leading spaces of the method call
                var leadingSpaces = GetLeadingSpaces(node) + 4;

                // Create a new ArgumentListSyntax with each argument on its own line
                var newArguments = list.Select(a => a.WithLeadingTrivia(SyntaxFactory.Whitespace(new string(' ', leadingSpaces)))).ToList();
                var newNode = addParameters(node, SyntaxFactory.SeparatedList(newArguments, Enumerable.Repeat(SyntaxFactory.Token(SyntaxKind.CommaToken).WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed), newArguments.Count - 1)));
                ////var newNode = node.WithParameterList(SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(newArguments, Enumerable.Repeat(SyntaxFactory.Token(SyntaxKind.CommaToken).WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed), newArguments.Count - 1))));
                return newNode;
            }

            return null;
        }

        private static int GetLeadingSpaces(SyntaxNode? node)
        {
            if (node is null)
            {
                return 0;
            }

            return node.GetLocation().GetLineSpan().StartLinePosition.Character;
        }

        private async Task<Solution> MakeUniqueLines(Document document, TypeDeclarationSyntax typeDecl, CancellationToken cancellationToken)
        {
            // Compute new uppercase name.
            var identifierToken = typeDecl.Identifier;
            var newName = identifierToken.Text.ToUpperInvariant();

            // Get the symbol representing the type to be renamed.
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            var typeSymbol = semanticModel.GetDeclaredSymbol(typeDecl, cancellationToken);

            // Produce a new solution that has all references to that type renamed, including the declaration.
            var originalSolution = document.Project.Solution;
            var optionSet = originalSolution.Workspace.Options;
            var newSolution = await Renamer.RenameSymbolAsync(document.Project.Solution, typeSymbol, newName, optionSet, cancellationToken).ConfigureAwait(false);

            // Return the new solution with the now-uppercase type name.
            return newSolution;
        }
    }
}

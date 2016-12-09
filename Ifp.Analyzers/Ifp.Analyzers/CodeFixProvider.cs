using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Formatting;

namespace Ifp.Analyzers
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(IfpAnalyzersCodeFixProvider)), Shared]
    public class IfpAnalyzersCodeFixProvider : CodeFixProvider
    {
        private const string title = "Make getter only auto property";

        public IfpAnalyzersCodeFixProvider()
        {

        }
        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(IfpAnalyzersAnalyzer.DiagnosticId);

        public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            // Register a code action that will invoke the fix.
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: title,
                    createChangedDocument: c => ReplaceByGetterOnlyAutoProperty(context.Document, diagnosticSpan, c),
                    equivalenceKey: title),
                diagnostic);
        }

        private async Task<Document> ReplaceByGetterOnlyAutoProperty(Document document, TextSpan propertyDeclarationSpan, CancellationToken cancellationToken)
        {
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            var root = await document.GetSyntaxRootAsync(cancellationToken);
            var token = root.FindToken(propertyDeclarationSpan.Start);
            var property = token.Parent.AncestorsAndSelf().OfType<PropertyDeclarationSyntax>().First();
            var fieldVariableDeclarationSyntax = await GetFieldDeclarationSyntaxNode(property, cancellationToken, semanticModel);
            if (fieldVariableDeclarationSyntax == null) return document;
            var fieldDeclarationSyntax = fieldVariableDeclarationSyntax.AncestorsAndSelf().FirstOrDefault(sn => sn is FieldDeclarationSyntax) as FieldDeclarationSyntax;
            var fieldReferences = await GetFieldReferences(fieldVariableDeclarationSyntax, cancellationToken, semanticModel);
            var nodesToUpdate = fieldReferences.Cast<SyntaxNode>().Union(Enumerable.Repeat(property, 1)).Union(Enumerable.Repeat(fieldDeclarationSyntax, 1));
            var newRoot = root.ReplaceNodes(nodesToUpdate, (sn1, sn2) =>
            {
                if (sn1 is IdentifierNameSyntax)
                {
                    var identifier = sn1 as IdentifierNameSyntax;
                    var newIdentifier = SyntaxFactory.IdentifierName(property.Identifier.Text);
                    newIdentifier = newIdentifier.WithLeadingTrivia(identifier.GetLeadingTrivia()).WithTrailingTrivia(identifier.GetTrailingTrivia()).WithAdditionalAnnotations(Formatter.Annotation);
                    return newIdentifier;
                }
                if (sn1 is FieldDeclarationSyntax)
                    return sn1.RemoveNode(sn1, SyntaxRemoveOptions.KeepUnbalancedDirectives);
                if (sn1 is PropertyDeclarationSyntax)
                {
                    var prop = sn1 as PropertyDeclarationSyntax;
                    var fieldInitilization = GetFieldInitialization(fieldDeclarationSyntax);
                    var getter = SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration).WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
                    var accessorList = SyntaxFactory.AccessorList(
                        SyntaxFactory.List(new[] {
                            getter
                        }));
                    var newProp = prop.WithAccessorList(accessorList);
                    if (fieldInitilization != null)
                        newProp = newProp.WithInitializer(fieldInitilization).WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
                    newProp = newProp.WithLeadingTrivia(prop.GetLeadingTrivia()).WithTrailingTrivia(prop.GetTrailingTrivia()).WithAdditionalAnnotations(Formatter.Annotation);
                    return newProp;
                }
                throw new NotSupportedException();
            });
            var resultDocument = document.WithSyntaxRoot(newRoot);
            return resultDocument;
        }

        private EqualsValueClauseSyntax GetFieldInitialization(FieldDeclarationSyntax fieldDeclarationSyntax)
        {
            var declaration = fieldDeclarationSyntax.Declaration;
            if (declaration == null)
                return null;
            var variableWithPotentialInitizer = declaration.Variables.LastOrDefault();
            if (variableWithPotentialInitizer == null)
                return null;
            var initializer = variableWithPotentialInitizer.Initializer;
            return initializer;
        }

        private static async Task<IEnumerable<IdentifierNameSyntax>> GetFieldReferences(VariableDeclaratorSyntax fieldDeclarationSyntax, CancellationToken cancellationToken, SemanticModel semanticModel)
        {
            HashSet<IdentifierNameSyntax> fieldReferences = null;
            var fieldSymbol = semanticModel.GetDeclaredSymbol(fieldDeclarationSyntax);
            var declaredInType = fieldSymbol.ContainingType;
            foreach (var reference in declaredInType.DeclaringSyntaxReferences)
            {
                var allNodesOfType = (await reference.GetSyntaxAsync(cancellationToken)).DescendantNodes();
                var allFieldReferenceNodes = from n in allNodesOfType.OfType<IdentifierNameSyntax>()
                                             where n.Identifier.ValueText == fieldDeclarationSyntax.Identifier.ValueText
                                             select n;
                foreach (var fieldReference in allFieldReferenceNodes)
                {
                    var parentExpression = fieldReference.Parent;
                    if (parentExpression is MemberAccessExpressionSyntax)
                        parentExpression = parentExpression.Parent;
                    if (parentExpression is AssignmentExpressionSyntax)
                    {
                        var assignmentEx = (AssignmentExpressionSyntax)parentExpression;
                        if (assignmentEx.Left == fieldReference || assignmentEx.Left == fieldReference.Parent)
                            (fieldReferences ?? (fieldReferences = new HashSet<IdentifierNameSyntax>())).Add(fieldReference);
                    }
                }
            }
            return fieldReferences ?? Enumerable.Empty<IdentifierNameSyntax>();
        }

        private static async Task<VariableDeclaratorSyntax> GetFieldDeclarationSyntaxNode(PropertyDeclarationSyntax propertyDeclaration, CancellationToken cancellationToken, SemanticModel semanticModel)
        {
            var propertySymbol = semanticModel.GetDeclaredSymbol(propertyDeclaration, cancellationToken);
            var declaredProperty = propertySymbol.GetMethod.DeclaringSyntaxReferences.FirstOrDefault();
            var declaredPropertySyntax = await declaredProperty.GetSyntaxAsync(cancellationToken);
            var fieldIdentifier = declaredPropertySyntax.DescendantNodesAndTokens().Where(n => n.IsNode && n.Kind() == SyntaxKind.IdentifierName).FirstOrDefault();
            var fieldInfo = semanticModel.GetSymbolInfo(fieldIdentifier.AsNode());
            var fieldDeclaration = fieldInfo.Symbol.DeclaringSyntaxReferences.FirstOrDefault();
            var fieldDeclarationSyntax = await fieldDeclaration.GetSyntaxAsync();
            return fieldDeclarationSyntax as VariableDeclaratorSyntax;
        }
    }
}
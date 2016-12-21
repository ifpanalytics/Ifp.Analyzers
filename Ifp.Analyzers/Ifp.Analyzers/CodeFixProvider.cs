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

        public sealed override FixAllProvider GetFixAllProvider() => SerialFixAllProvider.Instance;

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
            var fieldVariableDeclaratorSyntax = await GetFieldDeclarationSyntaxNode(property, cancellationToken, semanticModel);
            if (fieldVariableDeclaratorSyntax == null) return document;
            var fieldReferences = await GetFieldReferences(fieldVariableDeclaratorSyntax, cancellationToken, semanticModel);
            var nodesToUpdate = fieldReferences.Cast<SyntaxNode>().Union(Enumerable.Repeat(property, 1)).Union(Enumerable.Repeat(fieldVariableDeclaratorSyntax, 1));
            //var newRoot = FixWithReplaceNodes(root, property, fieldDeclarationSyntax, nodesToUpdate);
            var newRoot = FixWithTrackNode(root, property, fieldVariableDeclaratorSyntax, nodesToUpdate);
            var resultDocument = document.WithSyntaxRoot(newRoot);
            return resultDocument;
        }

        private SyntaxNode FixWithTrackNode(SyntaxNode root, PropertyDeclarationSyntax property, VariableDeclaratorSyntax fieldVariableDeclaratorSyntax, IEnumerable<SyntaxNode> nodesToUpdate)
        {
            var newRoot = root.TrackNodes(nodesToUpdate);
            var fieldReferences = newRoot.GetCurrentNodes(nodesToUpdate.OfType<IdentifierNameSyntax>());
            foreach (var identifier in fieldReferences)
            {
                var newIdentifier = SyntaxFactory.IdentifierName(property.Identifier.Text);
                newIdentifier = newIdentifier.WithLeadingTrivia(identifier.GetLeadingTrivia()).WithTrailingTrivia(identifier.GetTrailingTrivia()).WithAdditionalAnnotations(Formatter.Annotation);
                newRoot = newRoot.ReplaceNode(identifier, newIdentifier);
            }
            var prop = newRoot.GetCurrentNode(nodesToUpdate.OfType<PropertyDeclarationSyntax>().Single());
            var fieldInitilization = GetFieldInitialization(fieldVariableDeclaratorSyntax);
            var getter = SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration).WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
            var accessorList = SyntaxFactory.AccessorList(
                SyntaxFactory.List(new[] {
                            getter
                }));
            var newProp = prop.WithAccessorList(accessorList);
            if (fieldInitilization != null)
                newProp = newProp.WithInitializer(fieldInitilization).WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
            newProp = newProp.WithLeadingTrivia(prop.GetLeadingTrivia()).WithTrailingTrivia(prop.GetTrailingTrivia()).WithAdditionalAnnotations(Formatter.Annotation);
            newRoot = newRoot.ReplaceNode(prop, newProp);
            var variableDeclarator = newRoot.GetCurrentNode(nodesToUpdate.OfType<VariableDeclaratorSyntax>().Single());
            var declaration = variableDeclarator.AncestorsAndSelf().OfType<VariableDeclarationSyntax>().First();
            if (declaration.Variables.Count == 1)
            {
                var fieldDeclaration = declaration.AncestorsAndSelf().OfType<FieldDeclarationSyntax>().First();
                newRoot = newRoot.RemoveNode(fieldDeclaration, SyntaxRemoveOptions.KeepUnbalancedDirectives);
            }
            else
                newRoot = newRoot.RemoveNode(variableDeclarator, SyntaxRemoveOptions.KeepUnbalancedDirectives);
            return newRoot;
        }

        private EqualsValueClauseSyntax GetFieldInitialization(VariableDeclaratorSyntax fieldVariableDeclaratorSyntax)
        {
            var declaration = fieldVariableDeclaratorSyntax.AncestorsAndSelf().OfType<VariableDeclarationSyntax>().First();
            if (declaration == null)
                return null;
            var variableWithPotentialInitizer = declaration.Variables.SkipWhile(v => v != fieldVariableDeclaratorSyntax).Where(v => v.Initializer != null).FirstOrDefault();
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
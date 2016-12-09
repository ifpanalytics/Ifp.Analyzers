using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Ifp.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class IfpAnalyzersAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "IFP0001";

        // You can change these strings in the Resources.resx file. If you do not want your analyzer to be localize-able, you can use regular strings for Title and MessageFormat.
        // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/Localizing%20Analyzers.md for more on localization
        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.AnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.AnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.AnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = "Simplification";

        private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Info, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.NamedType);
        }

        private static void AnalyzeSymbol(SymbolAnalysisContext context)
        {
            var namedTypeSymbol = (INamedTypeSymbol)context.Symbol;
            var properties = GetPropsWithOnlyGettersAndReadonlyBackingField(namedTypeSymbol, context);
            if (properties == null) return;
            foreach (var p in properties)
            {
                var diagnostic = Diagnostic.Create(Rule, p.Key.Locations[0], p.Key.Name);
                context.ReportDiagnostic(diagnostic);
            }
        }

        private static IDictionary<ISymbol, IFieldSymbol> GetPropsWithOnlyGettersAndReadonlyBackingField(INamedTypeSymbol type, SymbolAnalysisContext context)
        {
            Dictionary<ISymbol, IFieldSymbol> candidates = null;
            SemanticModel model = null;
            var allProperties = type.GetMembers().Where(s => s.Kind == SymbolKind.Property);
            foreach (var propertySymbol in allProperties.Cast<IPropertySymbol>())
            {
                if (!propertySymbol.IsReadOnly || propertySymbol.IsStatic) continue;
                var getMethod = propertySymbol.GetMethod;
                if (getMethod == null) continue;
                var reference = getMethod.DeclaringSyntaxReferences.FirstOrDefault();
                if (reference == null) continue;
                var declaration = reference.GetSyntax(context.CancellationToken) as AccessorDeclarationSyntax;
                if (declaration?.Body == null) continue;
                var returnNode = declaration.Body.ChildNodes().FirstOrDefault();
                if (returnNode?.Kind() != SyntaxKind.ReturnStatement) continue;
                var fieldNode = returnNode.ChildNodes().FirstOrDefault();
                if (fieldNode == null) continue;
                if (fieldNode.Kind() == SyntaxKind.SimpleMemberAccessExpression)
                    fieldNode = (fieldNode as MemberAccessExpressionSyntax).Name;
                if (fieldNode.Kind() != SyntaxKind.IdentifierName) continue;
                model = model ?? context.Compilation.GetSemanticModel(fieldNode.SyntaxTree);
                var symbolInfo = model.GetSymbolInfo(fieldNode).Symbol as IFieldSymbol;
                if (symbolInfo != null &&
                    symbolInfo.IsReadOnly &&
                    (symbolInfo.DeclaredAccessibility == Accessibility.Private || symbolInfo.DeclaredAccessibility == Accessibility.NotApplicable) &&
                    symbolInfo.ContainingType == propertySymbol.ContainingType &&
                    symbolInfo.Type.Equals(propertySymbol.Type))

                    (candidates ?? (candidates = new Dictionary<ISymbol, IFieldSymbol>())).Add(propertySymbol, symbolInfo);
            }
            return candidates;
        }
    }
}

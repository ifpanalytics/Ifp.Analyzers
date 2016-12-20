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
            context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.Property);
        }

        private static void AnalyzeSymbol(SymbolAnalysisContext context)
        {
            var namedTypeSymbol = (IPropertySymbol)context.Symbol;
            var properties = GetPropsWithOnlyGettersAndReadonlyBackingField(namedTypeSymbol, context);
            if (properties == null) return;
            var diagnostic = Diagnostic.Create(Rule, properties.Item1.Locations[0], properties.Item1.Name);
            context.ReportDiagnostic(diagnostic);
        }

        private static Tuple<ISymbol, IFieldSymbol> GetPropsWithOnlyGettersAndReadonlyBackingField(IPropertySymbol propertySymbol, SymbolAnalysisContext context)
        {
            SemanticModel model = null;
            if (!propertySymbol.IsReadOnly || propertySymbol.IsStatic || !propertySymbol.CanBeReferencedByName) return null;
            var getMethod = propertySymbol.GetMethod;
            if (getMethod == null) return null;
            var reference = getMethod.DeclaringSyntaxReferences.FirstOrDefault();
            if (reference == null) return null;
            var declaration = reference.GetSyntax(context.CancellationToken) as AccessorDeclarationSyntax;
            if (declaration?.Body == null) return null;
            var returnNode = declaration.Body.ChildNodes().FirstOrDefault();
            if (returnNode?.Kind() != SyntaxKind.ReturnStatement) return null;
            var fieldNode = returnNode.ChildNodes().FirstOrDefault();
            if (fieldNode == null) return null;
            if (fieldNode.Kind() == SyntaxKind.SimpleMemberAccessExpression)
                fieldNode = (fieldNode as MemberAccessExpressionSyntax).Name;
            if (fieldNode.Kind() != SyntaxKind.IdentifierName) return null;
            model = model ?? context.Compilation.GetSemanticModel(fieldNode.SyntaxTree);
            var symbolInfo = model.GetSymbolInfo(fieldNode).Symbol as IFieldSymbol;
            if (symbolInfo != null &&
                symbolInfo.IsReadOnly &&
                (symbolInfo.DeclaredAccessibility == Accessibility.Private || symbolInfo.DeclaredAccessibility == Accessibility.NotApplicable) &&
                symbolInfo.ContainingType == propertySymbol.ContainingType &&
                symbolInfo.Type.Equals(propertySymbol.Type))

                return new Tuple<ISymbol, IFieldSymbol>(propertySymbol, symbolInfo);
            return null;
        }
    }
}

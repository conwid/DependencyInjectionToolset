using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CreateConstructorRefactoring
{
    [ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(CreateConstructorRefactoringCodeRefactoringProvider)), Shared]
    internal class CreateConstructorRefactoringCodeRefactoringProvider : CodeRefactoringProvider
    {
        private bool CandidateField(FieldDeclarationSyntax fieldDecl)
        {
            return !(fieldDecl == null || fieldDecl.Modifiers.ToString().Contains(SyntaxFactory.Token(SyntaxKind.PublicKeyword).ToString())
                || fieldDecl.Modifiers.ToString().Contains(SyntaxFactory.Token(SyntaxKind.StaticKeyword).ToString())
                || !fieldDecl.Modifiers.ToString().Contains(SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword).ToString()));
        }

        private bool IsInjectableType(FieldDeclarationSyntax fieldDecl, SemanticModel model)
        {
            var symbolInfo = model.GetSymbolInfo(fieldDecl.Declaration.Type);
            if (symbolInfo.Symbol == null)
            {
                return false;
            }
            var typeSymbol = (INamedTypeSymbol)symbolInfo.Symbol;
            return (typeSymbol.IsAbstract && typeSymbol.TypeKind == TypeKind.Class) || typeSymbol.TypeKind == TypeKind.Interface;
        }

        private IMethodSymbol GetBaseCtor(ClassDeclarationSyntax classDecl, SemanticModel model)
        {
            var symbolInfo = model.GetDeclaredSymbol(classDecl);
            if (symbolInfo == null)
            {
                return null;
            }
            if (symbolInfo.BaseType == null || symbolInfo.BaseType.Name == "System.Object")
            {
                return null;
            }
            var baseCtors = symbolInfo.BaseType.GetMembers().Where(m => m.Kind == SymbolKind.Method).Cast<IMethodSymbol>().Where(m => m.MethodKind == MethodKind.Constructor);
            if (baseCtors.Any(c => c.Parameters.Length == 0))
            {
                return null;
            }
            return baseCtors.OrderByDescending(c => c.Parameters.Length).First();
        }

        public sealed override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var node = root.FindNode(context.Span);
            var model = await context.Document.GetSemanticModelAsync();
            var fieldDecl = node as FieldDeclarationSyntax;
            if (!CandidateField(fieldDecl) || !IsInjectableType(fieldDecl, model))
            {
                return;
            }

            // For any type declaration node, create a code action to reverse the identifier text.
            var action = CodeAction.Create("Create constructor for dependency injection", c => CreateCtor(context, context.Document, model, fieldDecl, c));

            // Register this code action.
            context.RegisterRefactoring(action);
        }

        private async Task<Document> CreateCtor(CodeRefactoringContext context, Document document, SemanticModel model, FieldDeclarationSyntax fieldDecl, CancellationToken cancellationToken)
        {
            var originalType = fieldDecl.Parent as ClassDeclarationSyntax;
            var fields = originalType.DescendantNodes().OfType<FieldDeclarationSyntax>();
            var candidateFields = fields.Where(f => CandidateField(f) && IsInjectableType(f, model));
            var parameterTokens = new List<SyntaxNodeOrToken>();
            var statements = new List<StatementSyntax>();
            foreach (var field in candidateFields)
            {
                parameterTokens.Add(SyntaxFactory.Parameter(new SyntaxList<AttributeListSyntax>(), new SyntaxTokenList(), field.Declaration.Type, field.Declaration.Variables[0].Identifier, null)); // todo: handle fielddeclarations with multiple variables
                parameterTokens.Add(SyntaxFactory.Token(SyntaxKind.CommaToken));
                statements.Add(SyntaxFactory.ExpressionStatement(
                                        SyntaxFactory.AssignmentExpression(
                                                SyntaxKind.SimpleAssignmentExpression,
                                                SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, SyntaxFactory.ThisExpression(), SyntaxFactory.IdentifierName(field.Declaration.Variables[0].Identifier)),
                                                SyntaxFactory.IdentifierName(field.Declaration.Variables[0].Identifier)
                    )));
            }

            var ctor = SyntaxFactory.ConstructorDeclaration(SyntaxFactory.Identifier(originalType.Identifier.ToString()))
                                  .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)));

            var baseCtor = GetBaseCtor(originalType, model);
            if (baseCtor != null)
            {
                foreach (var baseParam in baseCtor.Parameters)
                {
                    parameterTokens.Add(SyntaxFactory.Parameter(new SyntaxList<AttributeListSyntax>(), new SyntaxTokenList(), SyntaxFactory.IdentifierName(baseParam.Type.Name), SyntaxFactory.Identifier(baseParam.Name), null));
                    parameterTokens.Add(SyntaxFactory.Token(SyntaxKind.CommaToken));
                }
            }
            parameterTokens.RemoveAt(parameterTokens.Count - 1);

            ctor = ctor.WithParameterList(SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList<ParameterSyntax>(parameterTokens)));

            if (baseCtor != null)
            {
                var baseParameterTokens = new List<SyntaxNodeOrToken>();
                foreach (var baseParam in baseCtor.Parameters)
                {
                    baseParameterTokens.Add(SyntaxFactory.Argument(SyntaxFactory.IdentifierName(baseParam.Name)));
                    baseParameterTokens.Add(SyntaxFactory.Token(SyntaxKind.CommaToken));
                }
                baseParameterTokens.RemoveAt(baseParameterTokens.Count - 1);

                ctor = ctor.WithInitializer(
                        SyntaxFactory.ConstructorInitializer(
                            SyntaxKind.BaseConstructorInitializer,
                            SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList<ArgumentSyntax>(baseParameterTokens))));
            }

            ctor = ctor.WithBody(SyntaxFactory.Block(statements));

            var newType = originalType.AddMembers(ctor);

            var oldRoot = await context.Document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var newRoot = oldRoot.ReplaceNode(originalType, newType);

            return document.WithSyntaxRoot(newRoot);
        }
    }
}
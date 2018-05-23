using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
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
        protected string ActionName;

        public CreateConstructorRefactoringCodeRefactoringProvider()
        {
            ActionName = "Create constructor for dependency injection";
        }

        protected bool CandidateField(FieldDeclarationSyntax fieldDecl)
        {
            return !(fieldDecl == null || fieldDecl.Modifiers.ToString().Contains(SyntaxFactory.Token(SyntaxKind.PublicKeyword).ToString())
                || fieldDecl.Modifiers.ToString().Contains(SyntaxFactory.Token(SyntaxKind.StaticKeyword).ToString())
                || !fieldDecl.Modifiers.ToString().Contains(SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword).ToString()));
        }

        protected bool IsInjectableType(TypeSyntax typeSyntax, SemanticModel model)
        {
            var symbolInfo = model.GetSymbolInfo(typeSyntax);
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

        protected bool CandidateProperty(PropertyDeclarationSyntax propertyDecl)
        {
            return propertyDecl.AccessorList.Accessors.All(a => a.IsKind(SyntaxKind.GetAccessorDeclaration));
        }

        public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var node = root.FindNode(context.Span);
            var model = await context.Document.GetSemanticModelAsync();
            var fieldDecl = node as FieldDeclarationSyntax;
            var propertyDecl = node as PropertyDeclarationSyntax;
            if ((fieldDecl != null && CandidateField(fieldDecl) && IsInjectableType(fieldDecl.Declaration.Type, model)) || (propertyDecl != null && CandidateProperty(propertyDecl) && IsInjectableType(propertyDecl.Type, model)))
            {
                // For any type declaration node, create a code action to reverse the identifier text.
                var action = CodeAction.Create(ActionName, c => CreateCtor(context, context.Document, model, fieldDecl ?? (MemberDeclarationSyntax)propertyDecl, c));

                // Register this code action.
                context.RegisterRefactoring(action);
            }
        }

        protected virtual List<SyntaxNodeOrToken> GetParameterTokensForCtor(ClassDeclarationSyntax originalType, SemanticModel model)
        {
            var parameterTokens = new List<SyntaxNodeOrToken>();
            var fields = originalType.DescendantNodes().OfType<FieldDeclarationSyntax>();
            foreach (var field in fields.Where(f => CandidateField(f) && IsInjectableType(f.Declaration.Type, model)))
            {
                var paramName = field.Declaration.Variables[0].Identifier;
                parameterTokens.Add(SyntaxFactory.Parameter(new SyntaxList<AttributeListSyntax>(), new SyntaxTokenList(), field.Declaration.Type, paramName, null)); // todo: handle fielddeclarations with multiple variables
                parameterTokens.Add(SyntaxFactory.Token(SyntaxKind.CommaToken));
            }
            return parameterTokens;
        }

        protected virtual List<StatementSyntax> GetStatementsForCtor(ClassDeclarationSyntax originalType, SemanticModel model)
        {
            var statements = new List<StatementSyntax>();
            var fields = originalType.DescendantNodes().OfType<FieldDeclarationSyntax>();
            foreach (var field in fields.Where(f => CandidateField(f) && IsInjectableType(f.Declaration.Type, model)))
            {
                var paramName = field.Declaration.Variables[0].Identifier;
                statements.Add(SyntaxFactory.IfStatement(
                        SyntaxFactory.BinaryExpression(
                            SyntaxKind.EqualsExpression,
                            SyntaxFactory.IdentifierName(paramName),
                            SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression)
                         ),
                        SyntaxFactory.ThrowStatement(
                            SyntaxFactory.ObjectCreationExpression(
                                SyntaxFactory.IdentifierName(nameof(ArgumentNullException)),
                                                             SyntaxFactory.ArgumentList().AddArguments(
                                                                 SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(SyntaxFactory.IdentifierName(paramName).Identifier.ToString())))
                                                             ),
                                                             null
                                                                  )
                                                      )
                                             )
  );

                statements.Add(SyntaxFactory.ExpressionStatement(
                                        SyntaxFactory.AssignmentExpression(
                                                SyntaxKind.SimpleAssignmentExpression,
                                                SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, SyntaxFactory.ThisExpression(), SyntaxFactory.IdentifierName(field.Declaration.Variables[0].Identifier)),
                                                SyntaxFactory.IdentifierName(field.Declaration.Variables[0].Identifier)
                    )));
            }

            return statements;
        }

        protected virtual async Task<Document> CreateCtor(CodeRefactoringContext context, Document document, SemanticModel model, MemberDeclarationSyntax fieldDecl, CancellationToken cancellationToken)
        {
            var originalType = fieldDecl.Parent as ClassDeclarationSyntax;
            var parameterTokens = GetParameterTokensForCtor(originalType, model);
            var statements = GetStatementsForCtor(originalType, model);

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
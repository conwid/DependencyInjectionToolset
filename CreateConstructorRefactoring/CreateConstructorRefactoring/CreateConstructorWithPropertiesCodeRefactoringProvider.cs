using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CreateConstructorRefactoring
{
    [ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(CreateConstructorWithPropertiesCodeRefactoringProvider)), Shared]
    internal class CreateConstructorWithPropertiesCodeRefactoringProvider : CreateConstructorRefactoringCodeRefactoringProvider
    {
        public CreateConstructorWithPropertiesCodeRefactoringProvider()
        {
            ActionName = "Create constructor for dependency injection with properties";
        }
        
        protected override List<SyntaxNodeOrToken> GetParameterTokensForCtor(ClassDeclarationSyntax originalType, SemanticModel model)
        {
            var res = base.GetParameterTokensForCtor(originalType, model);
            var properties = originalType.DescendantNodes().OfType<PropertyDeclarationSyntax>();
            foreach (var property in properties.Where(f => CandidateProperty(f) && IsInjectableType(f.Type, model)))
            {
                var paramName = property.Identifier.ToString();
                paramName = char.ToLower(paramName[0]) + paramName.Substring(1);
                res.Add(SyntaxFactory.Parameter(new SyntaxList<AttributeListSyntax>(), new SyntaxTokenList(), property.Type, SyntaxFactory.Identifier(paramName), null)); // todo: handle fielddeclarations with multiple variables
                res.Add(SyntaxFactory.Token(SyntaxKind.CommaToken));
            }
            return res;
        }

        protected override List<StatementSyntax> GetStatementsForCtor(ClassDeclarationSyntax originalType, SemanticModel model)
        {
            var statements = base.GetStatementsForCtor(originalType, model);
            var properties = originalType.DescendantNodes().OfType<PropertyDeclarationSyntax>();
            foreach (var property in properties.Where(p => CandidateProperty(p) && IsInjectableType(p.Type, model)))
            {
                var paramName = property.Identifier.ToString();
                paramName = char.ToLower(paramName[0]) + paramName.Substring(1);
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
                                                SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, SyntaxFactory.ThisExpression(), SyntaxFactory.IdentifierName(property.Identifier)),
                                                SyntaxFactory.IdentifierName(paramName)
                    )));
            }

            return statements;
        }
    }
}

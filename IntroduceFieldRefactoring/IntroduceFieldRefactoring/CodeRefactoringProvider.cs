using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;

namespace IntroduceFieldRefactoring
{
    [ExportCodeRefactoringProvider( LanguageNames.CSharp, Name = nameof( IntroduceFieldRefactoringCodeRefactoringProvider ) ), Shared]
    internal class IntroduceFieldRefactoringCodeRefactoringProvider : CodeRefactoringProvider
    {
        public sealed override async Task ComputeRefactoringsAsync( CodeRefactoringContext context )
        {
            var root = await context.Document.GetSyntaxRootAsync( context.CancellationToken ).ConfigureAwait( false );
            var node = root.FindNode( context.Span );
            var parameterList = node as ParameterListSyntax;
            if( parameterList == null )
            {
                return;
            }

            var parameter = parameterList.Parameters.SingleOrDefault( p => p.Span.Contains( context.Span ) );
            if( parameter == null )
            {
                return;
            }
            var parameterName = GetParameterName( parameter );
            if( string.IsNullOrEmpty( parameterName ) )
            {
                return;
            }
            var uppercase = parameterName.Substring( 0, 1 ).ToUpper() + parameterName.Substring( 1 );

            if( !VariableExists( root, "_" + parameterName ) )
            {
                var action = CodeAction.Create( "Introduce and initialize field '_" + parameterName + "'", ct => CreateFieldAsync( context, parameter, parameterName, ct, true ) );
                context.RegisterRefactoring( action );
            }

            if( !VariableExists( root, parameterName ) )
            {
                var action2 = CodeAction.Create( "Introduce and initialize field 'this." + parameterName + "'", ct => CreateFieldAsync( context, parameter, parameterName, ct ) );
                context.RegisterRefactoring( action2 );
            }

        }

        private async Task<Document> CreateFieldAsync( CodeRefactoringContext context, ParameterSyntax parameter,
            string paramName, CancellationToken cancellationToken, bool useUnderscore = false )
        {
            ExpressionSyntax assignment = null;
            if( useUnderscore )
            {
                assignment = SyntaxFactory.AssignmentExpression(
                         SyntaxKind.SimpleAssignmentExpression,
                         SyntaxFactory.IdentifierName( "_" + paramName ),
                         SyntaxFactory.IdentifierName( paramName ) );
            }
            else
            {
                assignment = SyntaxFactory.AssignmentExpression(
                         SyntaxKind.SimpleAssignmentExpression,
                         SyntaxFactory.MemberAccessExpression( SyntaxKind.SimpleMemberAccessExpression, SyntaxFactory.ThisExpression(), SyntaxFactory.IdentifierName( paramName ) ),
                         SyntaxFactory.IdentifierName( paramName ) );
            }
            var oldConstructor = parameter.Ancestors().OfType<ConstructorDeclarationSyntax>().First();
            var newConstructor = oldConstructor.WithBody( oldConstructor.Body.AddStatements(
                 SyntaxFactory.ExpressionStatement( assignment ) ) );

            var oldClass = parameter.FirstAncestorOrSelf<ClassDeclarationSyntax>();
            var oldClassWithNewCtor = oldClass.ReplaceNode( oldConstructor, newConstructor );

            var fieldDeclaration = CreateFieldDeclaration( GetParameterType( parameter ), paramName, useUnderscore );
            var newClass = oldClassWithNewCtor
                .WithMembers( oldClassWithNewCtor.Members.Insert( 0, fieldDeclaration ) )
                .WithAdditionalAnnotations( Formatter.Annotation );

            var oldRoot = await context.Document.GetSyntaxRootAsync( cancellationToken ).ConfigureAwait( false );
            var newRoot = oldRoot.ReplaceNode( oldClass, newClass );

            return context.Document.WithSyntaxRoot( newRoot );
        }

        public static bool VariableExists( SyntaxNode root, params string[] variableNames )
        {
            return root
                .DescendantNodes()
                .OfType<VariableDeclarationSyntax>()
                .SelectMany( ps => ps.DescendantTokens().Where( t => t.IsKind( SyntaxKind.IdentifierToken ) && variableNames.Contains( t.ValueText ) ) )
                .Any();
        }

        public static string GetParameterType( ParameterSyntax parameter )
        {
            return parameter.DescendantNodes().First( node => node is TypeSyntax ).GetFirstToken().ValueText;
        }

        private static string GetParameterName( ParameterSyntax parameter )
        {
            return parameter.DescendantTokens().Where( t => t.IsKind( SyntaxKind.IdentifierToken ) ).Last().ValueText;
        }

        private static FieldDeclarationSyntax CreateFieldDeclaration( string type, string name, bool useUnderscore = false )
        {
            return SyntaxFactory.FieldDeclaration(
                SyntaxFactory.VariableDeclaration( SyntaxFactory.IdentifierName( type ) )
                .WithVariables( SyntaxFactory.SingletonSeparatedList( SyntaxFactory.VariableDeclarator( SyntaxFactory.Identifier( useUnderscore ? "_" + name : name ) ) ) ) )
                .WithModifiers( SyntaxFactory.TokenList( SyntaxFactory.Token( SyntaxKind.PrivateKeyword ), SyntaxFactory.Token( SyntaxKind.ReadOnlyKeyword ) ) );
        }
    }
}
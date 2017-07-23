using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CreateConstructorRefactoring
{
    internal class DIPropertyWalker
    {
        private class PropertyWalker : CSharpSyntaxWalker
        {
            private bool isValid;

            public override void VisitPropertyDeclaration( PropertyDeclarationSyntax node )
            {
                base.VisitPropertyDeclaration(node);
                if( node.Initializer != null || node.Modifiers.Any( m => m.Kind() == SyntaxKind.PublicKeyword || m.Kind() == SyntaxKind.StaticKeyword ) )
                {
                    isValid = false;
                }
            }

            public override void VisitAccessorDeclaration( AccessorDeclarationSyntax node )
            {
                if( !( node.Kind() == SyntaxKind.GetAccessorDeclaration && node.Body == null ) )
                {
                    isValid = false;
                }
            }

            public bool IsCandidate( PropertyDeclarationSyntax node )
            {
                isValid = true;
                VisitPropertyDeclaration( node );
                return isValid;
            }
        }

        public bool IsCandidate( PropertyDeclarationSyntax property )
        {
            var propertyVisitor = new PropertyWalker();
            return propertyVisitor.IsCandidate( property );
        }
    }
}

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CreateConstructorRefactoring
{
    internal class DIPropertyVisitor
    {
        private class PropertyVisitor : CSharpSyntaxVisitor
        {
            private bool isValid;

            public override void VisitPropertyDeclaration( PropertyDeclarationSyntax node )
            {
                //TODO: Why do I need to do this? 
                VisitAccessorList( node.AccessorList );
                if( node.Initializer != null || node.Modifiers.Any( m => m.Kind() == SyntaxKind.PublicKeyword || m.Kind() == SyntaxKind.StaticKeyword ) )
                {
                    isValid = false;
                }
            }

            public override void VisitAccessorList( AccessorListSyntax node )
            {
                //TODO: Why do I need to do this? 
                foreach( var accessorDeclarationSyntax in node.Accessors )
                {
                    VisitAccessorDeclaration(accessorDeclarationSyntax);
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
            var propertyVisitor = new PropertyVisitor();
            return propertyVisitor.IsCandidate( property );
        }
    }
}

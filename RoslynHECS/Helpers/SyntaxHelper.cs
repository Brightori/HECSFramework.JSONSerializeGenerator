using System.Linq;
using System.Runtime.CompilerServices;
using HECSFramework.Core.Generator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RoslynHECS.Helpers
{
    public static class SyntaxHelper
    {
        public static bool TryGetParent(this ClassDeclarationSyntax classDeclarationSyntax, out string parent, out ClassDeclarationSyntax parentClass)
        {
            if (classDeclarationSyntax.BaseList != null)
            {
                var parentType =  classDeclarationSyntax.BaseList.Types.FirstOrDefault(x => x.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.ClassDeclaration));
                parentClass = Program.classes.FirstOrDefault(x => x.Identifier.ValueText == parentType.Type.ToString());
                parent = parentClass != null ? parentClass.Identifier.ValueText : string.Empty;
                return true;
            }

            parent = null;
            parentClass = null;
            return false;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string GetType(MemberDeclarationSyntax memberDeclarationSyntax)
        {
            if (memberDeclarationSyntax is PropertyDeclarationSyntax property)
                return property.Type.ToString();
            else if (memberDeclarationSyntax is FieldDeclarationSyntax field)
                return field.Declaration.Type.ToString();

            return string.Empty;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string GetFieldName(MemberDeclarationSyntax memberDeclarationSyntax)
        {
            if (memberDeclarationSyntax is PropertyDeclarationSyntax property)
                return property.Identifier.ToString();
            else if (memberDeclarationSyntax is FieldDeclarationSyntax field)
                return field.Declaration.Variables[0].Identifier.ToString();

            return string.Empty;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddUnique(this ISyntax syntax, ISyntax add)
        {
            if (syntax.Tree.Any(x => x.ToString() == add.ToString()))
                return;

            syntax.Tree.Add(add);
        }
    }
}
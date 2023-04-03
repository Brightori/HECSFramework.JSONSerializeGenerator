using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Principal;
using HECSFramework.Core;
using HECSFramework.Core.Generator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;
using FieldDeclarationSyntax = Microsoft.CodeAnalysis.CSharp.Syntax.FieldDeclarationSyntax;
using GenericNameSyntax = Microsoft.CodeAnalysis.CSharp.Syntax.GenericNameSyntax;

namespace RoslynHECS.Helpers
{
    public static class SyntaxHelper
    {
        public static bool TryGetParent(this ClassDeclarationSyntax classDeclarationSyntax, out string parent, out ClassDeclarationSyntax parentClass)
        {
            if (classDeclarationSyntax.BaseList != null)
            {
                var parentType =  classDeclarationSyntax.BaseList.Types.FirstOrDefault(x => Program.nameToClassDeclaration.ContainsKey(x.Type.ToString()));
                parentClass = parentType != null ? Program.nameToClassDeclaration[parentType.ToString()] : null;
                parent = parentClass != null ? parentClass.Identifier.ValueText : string.Empty;
                return parentClass != null;
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

        public static string GetSpecialResolverName(this MemberInfoWithAttributes memberDeclarationSyntax)
        {
            var jsonResolver = memberDeclarationSyntax.Attributes.FirstOrDefault(x => x.Name.ToString() == "JSONHECSFieldByResolver");
            string resolver = string.Empty;

            if (jsonResolver != null)
            {
                var data = jsonResolver.ArgumentList.Arguments.First().ToString();
                data = data.Replace("typeof(", "");
                data = data.Replace(")", "");
                resolver = data;
            }

            return resolver;
        }

        public static string GetMemberType(this MemberDeclarationSyntax memberDeclarationSyntax)
        {
            if (memberDeclarationSyntax is FieldDeclarationSyntax field)
            {
                return field.Declaration.Type.ToString();
            }
            else if (memberDeclarationSyntax is PropertyDeclarationSyntax property)
            {
                return property.Type.ToString();
            }

            return default;
        }

        public static string GetMemberTypeName(this MemberDeclarationSyntax memberDeclarationSyntax)
        {
            if (memberDeclarationSyntax is FieldDeclarationSyntax field)
            {
                if (field.Declaration.Type is GenericNameSyntax nameSyntax)
                {
                    return nameSyntax.Identifier.Text;
                }

                return field.Declaration.Type.ToString();
            }
            else if (memberDeclarationSyntax is PropertyDeclarationSyntax property)
            {
                return property.Type.ToString();
            }

            return default;
        }

        public static string GetMemberFieldName(this MemberDeclarationSyntax memberDeclarationSyntax)
        {
            if (memberDeclarationSyntax is FieldDeclarationSyntax field)
            {
                return field.Declaration.Variables[0].Identifier.Text;
            }
            else if (memberDeclarationSyntax is PropertyDeclarationSyntax property)
            {
                return property.Identifier.ValueText;
            }

            return default;
        }
    }
}
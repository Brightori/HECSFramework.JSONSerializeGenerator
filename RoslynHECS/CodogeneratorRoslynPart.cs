using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RoslynHECS;
using RoslynHECS.DataTypes;
using RoslynHECS.Helpers;

namespace HECSFramework.Core.Generator
{
    public partial class CodeGenerator
    {
        public HashSet<ClassDeclarationSyntax> needResolver = new HashSet<ClassDeclarationSyntax>();
        public List<ClassDeclarationSyntax> containersSolve = new List<ClassDeclarationSyntax>();
        public List<Type> commands = new List<Type>();
        public List<string> alrdyAtContext = new List<string>();
        public const string JSONResolver = "JSONResolver";
        public const string Cs = ".cs";
        private string ResolverContainer = "ResolverDataContainer";
        public const string BluePrint = "BluePrint";
        public const string ContextSetter = "ContextSetter";

        public const string SystemBindSetter = "SystemBindSetter";
        public const string ISystemSetter = "ISystemSetter";
        public const string SystemBindContainer = "BindContainerForSys";

        public const string IReactGlobalCommand = "IReactGlobalCommand";
        public const string INetworkComponent = "INetworkComponent";
        public const string IReactCommand = "IReactCommand";
        public const string IReactComponentLocal = "IReactComponentLocal";
        public const string IReactComponentGlobal = "IReactComponentGlobal";

        public const string IReactGenericGlobalComponent = "IReactGenericGlobalComponent";
        public const string IReactGenericLocalComponent = "IReactGenericLocalComponent";

        public const string CurrentSystem = "currentSystem";

        public const string IReactNetworkCommandGlobal = "IReactNetworkCommandGlobal";
        public const string IReactNetworkCommandLocal = "IReactNetworkCommandLocal";


        #region JSONResolverMap

        public ISyntax GetJSONProvidersMaps()
        {
            var tree = new TreeSyntaxNode();
            var usings = new TreeSyntaxNode();
            var initModule = new TreeSyntaxNode();

            tree.Add(usings);
            tree.Add(new UsingSyntax("System"));
            tree.Add(new UsingSyntax("HECSFramework.Serialize"));
            tree.Add(new UsingSyntax("System.Collections.Generic", 1));

            tree.Add(new NameSpaceSyntax("HECSFramework.Core"));
            tree.Add(new LeftScopeSyntax());

            tree.Add(new TabSimpleSyntax(1, "public partial class ResolversMap"));
            
            tree.Add(new LeftScopeSyntax(1));

            tree.Add(GetDictionaryHelper.GetDictionaryMethod("GetTypeToJSONResolver", "Type", "JSONSerializationProvider", 2, out var bodyGetTypeToprovider));
            tree.Add(new ParagraphSyntax());

            tree.Add(GetDictionaryHelper.GetDictionaryMethod("GetTypeCodeToJSONResolver", "int", "JSONSerializationProvider", 2, out var bodyTypeIndexToProvider));
            tree.Add(new ParagraphSyntax());   
            
            tree.Add(GetDictionaryHelper.GetDictionaryMethod("GetTypeIndexToTypeJSON", "int", "Type", 2, out var bodyTypeIndexToType));
            tree.Add(new ParagraphSyntax()); 
            
            tree.Add(GetDictionaryHelper.GetDictionaryMethod("GetTypeToIndexJSON", "Type", "int", 2, out var bodyTypeToTypeIndex));
            tree.Add(new ParagraphSyntax());

            tree.Add(new TabSimpleSyntax(2, "partial void JSONModuleInit()"));
            tree.Add(new LeftScopeSyntax(2));

            tree.Add(new TabSimpleSyntax(3, "typeToJSONResolver = GetTypeToJSONResolver();"));
            tree.Add(new TabSimpleSyntax(3, "typeCodeToJSONResolver = GetTypeCodeToJSONResolver();"));
            tree.Add(new TabSimpleSyntax(3, "getTypeIndexToTypeJSON = GetTypeIndexToTypeJSON();"));
            tree.Add(new TabSimpleSyntax(3, "typeToIndexJSON = GetTypeToIndexJSON();"));

            tree.Add(new RightScopeSyntax(2));
            tree.Add(new RightScopeSyntax(1));
            tree.Add(new RightScopeSyntax());

            foreach (var l in Program.LinkedNodes)
            {
                if (l.Value.IsAbstract)
                    continue;

                bodyGetTypeToprovider.AddUnique(GetDictionaryHelper.DictionaryBodyRecord(4, $"typeof({l.Value.Name})", $"new JSONResolverProvider<{l.Value.Name},{l.Value.Name+JSONResolver}>()"));
                bodyTypeIndexToProvider.AddUnique(GetDictionaryHelper.DictionaryBodyRecord(4, $"{IndexGenerator.GenerateIndex(l.Value.Name)}", $"new JSONResolverProvider<{l.Value.Name},{l.Value.Name+JSONResolver}>()"));
                bodyTypeIndexToType.AddUnique(GetDictionaryHelper.DictionaryBodyRecord(4, $"{IndexGenerator.GenerateIndex(l.Value.Name)}", $"typeof({l.Value.Name})"));
                bodyTypeToTypeIndex.AddUnique(GetDictionaryHelper.DictionaryBodyRecord(4, $"typeof({l.Value.Name})", $"{IndexGenerator.GenerateIndex(l.Value.Name)}"));
            }

            return tree;
        }

        #endregion

        #region Resolvers

        public (bool valid, int Order, string resolver) IsValidField(MemberInfoWithAttributes fieldDeclarationSyntax)
        {
            if (fieldDeclarationSyntax.MemberDeclarationSyntax is PropertyDeclarationSyntax property)
            {
                if (property.AccessorList == null)
                    return (false, -1, string.Empty);

                var t = property.AccessorList.Accessors.FirstOrDefault(x => x.Keyword.Text == "set");

                if (t == null || t.Modifiers.Any(x => x.IsKind(SyntaxKind.PrivateKeyword) || x.IsKind(SyntaxKind.ProtectedKeyword)))
                    return (false, -1, string.Empty);
            }

            foreach (var a in fieldDeclarationSyntax.Attributes)
            {
                //todo "разобраться аккуратно с аттрибутами поля"
                if (a.Name.ToString() == ("Field") && fieldDeclarationSyntax.MemberDeclarationSyntax.Modifiers.ToString().Contains("public"))
                {
                    if (a.ArgumentList == null)
                        continue;
                    var resolver = string.Empty;

                    var arguments = a.ArgumentList.Arguments.ToArray();
                    var intValue = int.Parse(arguments[0].ToString());

                    var jsonResolver = fieldDeclarationSyntax.Attributes.FirstOrDefault(x => x.Name.ToString() == "JSONHECSFieldByResolver");

                    if (jsonResolver != null)
                    {
                        var data = jsonResolver.ArgumentList.Arguments.First().ToString();
                        data = data.Replace("typeof(", "");
                        data = data.Replace(")", "");
                        resolver = data;
                    }

                    return (true, intValue, resolver);
                }
            }

            return (false, -1, string.Empty);
        }

        //public static void GetNamespace(MemberDeclarationSyntax declaration, ISyntax tree)
        //{
        //    if (declaration is FieldDeclarationSyntax field)
        //    {
        //        if (field.Declaration.Type is GenericNameSyntax generic)
        //        {
        //            if (GetNameSpaceForCollection(generic.Identifier.Value.ToString(), out var namespaceCollection))
        //            {
        //                tree.AddUnique(new UsingSyntax(namespaceCollection));
        //            }

        //            foreach (var a in generic.TypeArgumentList.Arguments)
        //            {
        //                var arg = a.ToString();

        //                if (Program.structByName.TryGetValue(arg, out var value))
        //                {
        //                    if (value.Parent != null && value.Parent is NamespaceDeclarationSyntax ns)
        //                    {
        //                        tree.AddUnique(new UsingSyntax(ns.Name.ToString()));
        //                    }
        //                }

        //                if (Program.classesByName.TryGetValue(arg, out var classObject))
        //                {
        //                    if (classObject.Parent != null && classObject.Parent is NamespaceDeclarationSyntax ns)
        //                    {
        //                        tree.AddUnique(new UsingSyntax(ns.Name.ToString()));
        //                    }
        //                }
        //            }
        //        }
        //        else
        //        {

        //            var arg = field.Declaration.Type.ToString();

        //            if (Program.structByName.TryGetValue(arg, out var value))
        //            {
        //                if (value.Parent != null && value.Parent is NamespaceDeclarationSyntax ns)
        //                {
        //                    tree.AddUnique(new UsingSyntax(ns.Name.ToString()));
        //                }
        //            }

        //            if (Program.classesByName.TryGetValue(arg, out var classObject))
        //            {
        //                if (classObject.Parent != null && classObject.Parent is NamespaceDeclarationSyntax ns)
        //                {
        //                    tree.AddUnique(new UsingSyntax(ns.Name.ToString()));
        //                }
        //            }
        //        }
        //    }

        //    if (declaration is PropertyDeclarationSyntax property)
        //    {


        //        if (property.Type is GenericNameSyntax generic)
        //        {
        //            foreach (var a in generic.TypeArgumentList.Arguments)
        //            {
        //                var arg = a.ToString();

        //                if (Program.structByName.TryGetValue(arg, out var value))
        //                {
        //                    if (value.Parent != null && value.Parent is NamespaceDeclarationSyntax ns)
        //                    {
        //                        tree.AddUnique(new UsingSyntax(ns.Name.ToString()));
        //                    }
        //                }

        //                if (Program.classesByName.TryGetValue(arg, out var classObject))
        //                {
        //                    if (classObject.Parent != null && classObject.Parent is NamespaceDeclarationSyntax ns)
        //                    {
        //                        tree.AddUnique(new UsingSyntax(ns.Name.ToString()));
        //                    }
        //                }
        //            }
        //        }
        //        else
        //        {

        //            var arg = property.Type.ToString();

        //            if (Program.structByName.TryGetValue(arg, out var value))
        //            {
        //                if (value.Parent != null && value.Parent is NamespaceDeclarationSyntax ns)
        //                {
        //                    tree.AddUnique(new UsingSyntax(ns.Name.ToString()));
        //                }
        //            }

        //            if (Program.classesByName.TryGetValue(arg, out var classObject))
        //            {
        //                if (classObject.Parent != null && classObject.Parent is NamespaceDeclarationSyntax ns)
        //                {
        //                    tree.AddUnique(new UsingSyntax(ns.Name.ToString()));
        //                }
        //            }
        //        }
        //    }
        //}

        public ISyntax GetPartialClassForSerializePrivateFields(TypeDeclarationSyntax classDeclarationSyntax, string resolver, out ISyntax saveBody, out ISyntax loadBody)
        {
            var classSyntax = new TreeSyntaxNode();
            var getNamespace = GetNameSpace(classDeclarationSyntax);

            if (!string.IsNullOrEmpty(getNamespace))
            {
                classSyntax.Add(new NameSpaceSyntax(getNamespace));
                classSyntax.Add(new LeftScopeSyntax());
            }
            

            classSyntax.Add(new TabSimpleSyntax(1,
                $"public partial class {classDeclarationSyntax.Identifier.ValueText} : " +
                $"ISaveJSONToResolver<{resolver}>, ILoadFromJSONResolver<{resolver}>"));

            classSyntax.Add(new LeftScopeSyntax(1));
            classSyntax.Add(GetSaveResolverBody(resolver, out saveBody));
            classSyntax.Add(new ParagraphSyntax());
            classSyntax.Add(GetLoadResolverBody(resolver, out loadBody));
            classSyntax.Add(new RightScopeSyntax(1));

            if (!string.IsNullOrEmpty(getNamespace))
                classSyntax.Add(new RightScopeSyntax());
            
            return classSyntax;
        }

        public ISyntax GetSaveResolverBody(string resolver, out ISyntax body)
        {
            var tree = new TreeSyntaxNode();
            body = new TreeSyntaxNode();

            tree.Add(new TabSimpleSyntax(2, $"public void SaveToJSONResolver(ref {resolver} resolver)"));
            tree.Add(new LeftScopeSyntax(2));
            tree.Add(body);
            tree.Add(new RightScopeSyntax(2));

            return tree;
        }

        public ISyntax GetLoadResolverBody(string resolver, out ISyntax body)
        {
            var tree = new TreeSyntaxNode();
            body = new TreeSyntaxNode();

            tree.Add(new TabSimpleSyntax(2, $"public void LoadFromJSONResolver(ref {resolver} resolver)"));
            tree.Add(new LeftScopeSyntax(2));
            tree.Add(body);
            tree.Add(new RightScopeSyntax(2));

            return tree;
        }

        public (bool isValid, string nameSpace) GetNameSpaceForCollection(PropertyDeclarationSyntax propertyDeclaration)
        {
            var result = (false, string.Empty);

            var kind = propertyDeclaration.Type.Kind().ToString();

            if (kind.Contains("Array") || kind.Contains("Dictionary") || kind.Contains("List"))
            {
                var collection = propertyDeclaration.Type.DescendantNodes().ToList();

                foreach (var s in collection)
                {
                    if (s is IdentifierNameSyntax nameSyntax)
                    {
                        foreach (var cl in Program.classes)
                        {
                            if (cl.Identifier.ValueText.Contains(s.ToString()))
                            {
                                var nameSpace = cl.SyntaxTree.GetRoot().ChildNodes().FirstOrDefault(x => x is NamespaceDeclarationSyntax);

                                if (nameSpace != null)
                                {
                                    foreach (var child in nameSpace.ChildNodes())
                                    {
                                        if (child is QualifiedNameSyntax nameSyntaxNamespace)
                                        {
                                            var checkedName = nameSyntaxNamespace.ToString();
                                            if (checkedName == string.Empty || checkedName == "MessagePack.Resolvers")
                                                continue;

                                            return (true, checkedName);
                                        }

                                        if (child is IdentifierNameSyntax identifierName)
                                        {
                                            var checkedName = identifierName.ToString();
                                            if (checkedName == string.Empty || checkedName == "MessagePack.Resolvers")
                                                continue;

                                            return (true, checkedName);
                                        }
                                    }
                                }
                            }
                        }

                        foreach (var st in Program.structs)
                        {
                            if (st.Identifier.ValueText.Contains(s.ToString()))
                            {
                                var nameSpace = st.SyntaxTree.GetRoot().ChildNodes().FirstOrDefault(x => x is NamespaceDeclarationSyntax);

                                if (nameSpace != null)
                                {
                                    foreach (var child in nameSpace.ChildNodes())
                                    {
                                        if (child is QualifiedNameSyntax nameSyntaxNamespace)
                                        {
                                            var checkedName = nameSyntaxNamespace.ToString();
                                            if (checkedName == string.Empty || checkedName == "MessagePack.Resolvers")
                                                continue;

                                            return (true, checkedName);
                                        }

                                        if (child is IdentifierNameSyntax identifierName)
                                        {
                                            var checkedName = identifierName.ToString();
                                            if (checkedName == string.Empty || checkedName == "MessagePack.Resolvers")
                                                continue;

                                            return (true, checkedName);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return result;
        }

        public static bool GetNameSpaceForCollection(string name, out string collectionNamespace)
        {
            if (name == "Array" || name == "Dictionary" || name == "List" || name == "Dictionary" || name == "HashSet")
            {
                collectionNamespace = "System.Collections.Generic";
                return true;
            }

            collectionNamespace = string.Empty;
            return false;
        }

        public (bool isValid, ISyntax nameSpace) GetNameSpaceForCollection(FieldDeclarationSyntax field)
        {
            var kind = field.Declaration.Type.ToString();

            if (kind.Contains("Array") || kind.Contains("Dictionary") || kind.Contains("List") || kind.Contains("MoveCommandInfo"))
            {
                var collection = field.Declaration.Type.DescendantNodes().ToList();
                var currentUsings = new TreeSyntaxNode();

                if (kind.Contains("Dictionary"))
                {
                    currentUsings.AddUnique(new UsingSyntax("System.Collections.Generic"));
                }

                foreach (var s in collection)
                {
                    if (s is TypeArgumentListSyntax arguments)
                    {
                        foreach (var a in arguments.Arguments)
                            currentUsings.Add(GetNamespaces(a.ToString()));
                    }

                    if (s is IdentifierNameSyntax nameSyntax)
                    {
                        foreach (var cl in Program.classes)
                        {
                            if (cl.Identifier.ValueText.Contains(s.ToString()))
                            {
                                var nameSpace = cl.SyntaxTree.GetRoot().ChildNodes().FirstOrDefault(x => x is NamespaceDeclarationSyntax);

                                if (nameSpace != null)
                                {
                                    foreach (var child in nameSpace.ChildNodes())
                                    {
                                        if (child is QualifiedNameSyntax nameSyntaxNamespace)
                                        {
                                            var checkedName = nameSyntaxNamespace.ToString();
                                            if (checkedName == string.Empty || checkedName == "MessagePack.Resolvers")
                                                continue;

                                            currentUsings.Add(new UsingSyntax(checkedName));
                                        }

                                        if (child is IdentifierNameSyntax identifierName)
                                        {
                                            var checkedName = identifierName.ToString();
                                            if (checkedName == string.Empty || checkedName == "MessagePack.Resolvers")
                                                continue;

                                            currentUsings.Add(new UsingSyntax(checkedName));
                                        }
                                    }
                                }
                            }
                        }

                        foreach (var st in Program.structs)
                        {
                            if (st.Identifier.ValueText.Contains(s.ToString()))
                            {
                                var nameSpace = st.SyntaxTree.GetRoot().ChildNodes().FirstOrDefault(x => x is NamespaceDeclarationSyntax);

                                if (nameSpace != null)
                                {
                                    foreach (var child in nameSpace.ChildNodes())
                                    {
                                        if (child is QualifiedNameSyntax nameSyntaxNamespace)
                                        {
                                            var checkedName = nameSyntaxNamespace.ToString();
                                            if (checkedName == string.Empty || checkedName == "MessagePack.Resolvers")
                                                continue;

                                            currentUsings.Add(new UsingSyntax(checkedName));
                                        }

                                        if (child is IdentifierNameSyntax identifierName)
                                        {
                                            var checkedName = identifierName.ToString();
                                            if (checkedName == string.Empty || checkedName == "MessagePack.Resolvers")
                                                continue;

                                            currentUsings.Add(new UsingSyntax(checkedName));
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                return (true, currentUsings);
            }

            return (false, null);
        }

        private ISyntax GetNamespaces(string nameOfNode, bool isInterface = false)
        {
            var tree = new TreeSyntaxNode();

            var classes = Program.classes.Where(x => x.Identifier.ValueText == nameOfNode).ToList();
            var structs = Program.structs.Where(x => x.Identifier.ValueText == nameOfNode).ToList();

            var need = new List<TypeDeclarationSyntax>();
            need.AddRange(classes);
            need.AddRange(structs);
            //need.AddRange(interfaces);


            foreach (var c in need)
            {
                var childNodes = c.ChildNodes();

                foreach (var child in childNodes)
                {
                    if (child is QualifiedNameSyntax nameSyntaxNamespace)
                    {
                        var checkedName = nameSyntaxNamespace.ToString();
                        if (checkedName == string.Empty || checkedName == "MessagePack.Resolvers")
                            continue;

                        tree.Add(new UsingSyntax(checkedName));
                    }

                    if (child is IdentifierNameSyntax identifierName)
                    {
                        var checkedName = identifierName.ToString();
                        if (checkedName == string.Empty || checkedName == "MessagePack.Resolvers")
                            continue;

                        tree.Add(new UsingSyntax(checkedName));
                    }
                }
            }

            return tree;
        }


        private string GetNameSpace(PropertyDeclarationSyntax field)
        {
            var neededClass = Program.classes.FirstOrDefault(x => x.Identifier.ValueText == field.Identifier.ToString());
            var namespaceString = string.Empty;

            if (neededClass == null)
                return namespaceString;

            var tree = neededClass.SyntaxTree.GetRoot().ChildNodes();

            foreach (var cn in tree)
            {
                if (cn is NamespaceDeclarationSyntax declarationSyntax)
                {
                    var namespaceName = declarationSyntax.Name.ToString();
                    namespaceString = namespaceName;
                    break;
                }
            }

            return namespaceString;
        }

        private string GetNameSpace(TypeDeclarationSyntax field)
        {

            var tree = field.SyntaxTree.GetRoot().ChildNodes();
            string namespaceString = string.Empty;

            foreach (var cn in tree)
            {
                if (cn is NamespaceDeclarationSyntax declarationSyntax)
                {
                    var namespaceName = declarationSyntax.Name.ToString();
                    namespaceString = namespaceName;
                    break;
                }
            }

            return namespaceString;
        }

        private string GetNameSpace(FieldDeclarationSyntax field)
        {
            var neededClass = Program.classes.FirstOrDefault(x => x.Identifier.ValueText == field.Declaration.Type.ToString());
            var namespaceString = string.Empty;

            if (neededClass == null)
                return namespaceString;

            var tree = neededClass.SyntaxTree.GetRoot().ChildNodes();

            foreach (var cn in tree)
            {
                if (cn is NamespaceDeclarationSyntax declarationSyntax)
                {
                    var namespaceName = declarationSyntax.Name.ToString();
                    namespaceString = namespaceName;
                    break;
                }
            }

            return namespaceString;
        }

        private string GetListNameSpace(FieldDeclarationSyntax field)
        {
            var namespaceString = string.Empty;

            if (field.Declaration.Type.ToString().Contains("List"))
                namespaceString = "System.Collections.Generic";

            return namespaceString;
        }

        public (bool valid, int Order) IsValidProperty(PropertyDeclarationSyntax property)
        {
            if (!property.Modifiers.ToString().Contains("public"))
                return (false, -1);

            if (property.Type.ToString().Contains("ReactiveValue"))
            {
                var needed = property.AccessorList.Accessors.FirstOrDefault(x => x.Kind() == SyntaxKind.SetAccessorDeclaration);

                if (needed == null)
                    return (false, -1);

                foreach (var a in property.AttributeLists.SelectMany(x => x.Attributes).ToArray())
                {
                    if (a.ToString().Contains("Field") && property.Modifiers.ToString().Contains("public"))
                    {
                        var intValue = int.Parse(a.ArgumentList.Arguments.ToArray()[0].ToString());
                        Console.WriteLine("нашли реактив проперти");
                        return (true, intValue);
                    }
                }
            }
            else
            {
                if (property.AccessorList == null)
                    return (false, -1);

                var needed = property.AccessorList.Accessors.FirstOrDefault(x => x.Kind() == SyntaxKind.SetAccessorDeclaration);

                if (needed == null)
                    return (false, -1);

                if (needed.Modifiers.Any(x => x.Kind() == SyntaxKind.ProtectedKeyword || x.Kind() == SyntaxKind.PrivateKeyword))
                    return (false, -1);
            }

            foreach (var a in property.AttributeLists.SelectMany(x => x.Attributes).ToArray())
            {
                if (a.ToString().Contains("Field") && property.Modifiers.ToString().Contains("public"))
                {
                    var intValue = int.Parse(a.ArgumentList?.Arguments.ToArray()[0].ToString() ?? "0");
                    return (true, intValue);
                }
            }

            return (false, -1);
        }

        private ISyntax GetOutToEntityVoidBodyRoslyn(ClassDeclarationSyntax c)
        {
            var tree = new TreeSyntaxNode();
            tree.Add(new TabSimpleSyntax(3, $"var local = entity.GetComponent<{c.Identifier.ValueText}>();"));
            tree.Add(new TabSimpleSyntax(3, $"Out(ref local);"));
            return tree;
        }

        private ISyntax DefaultConstructor(Type type, List<(string type, string name)> data, ISyntax fields, ISyntax constructor)
        {
            var tree = new TreeSyntaxNode();
            var arguments = new TreeSyntaxNode();

            var defaultConstructor = new TreeSyntaxNode();
            var defaultconstructorSignature = new TreeSyntaxNode();

            tree.Add(new TabSimpleSyntax(2, $"[SerializationConstructor]"));
            tree.Add(defaultconstructorSignature);
            tree.Add(new LeftScopeSyntax(2));
            tree.Add(defaultConstructor);
            tree.Add(new RightScopeSyntax(2));

            if (data.Count == 0)
            {
                fields.Tree.Add(IsTagBool());
                constructor.Tree.Add(new TabSimpleSyntax(3, "IsTag = false;"));
                defaultConstructor.Tree.Add(new TabSimpleSyntax(3, "IsTag = false;"));
                arguments.Add(new SimpleSyntax("bool isTag"));

                defaultconstructorSignature.Add(new TabSimpleSyntax(2, $"public {type.Name + JSONResolver}({arguments})"));
                return tree;
            }

            for (int i = 0; i < data.Count; i++)
            {
                (string type, string name) d = data[i];
                var needComma = i < data.Count - 1 ? CParse.Comma : "";

                arguments.Add(new SimpleSyntax($"{d.type} {d.name}{needComma}"));
                defaultConstructor.Add(new TabSimpleSyntax(3, $"this.{d.name} = {d.name};"));
            }

            defaultconstructorSignature.Add(new TabSimpleSyntax(2, $"public {type.Name + JSONResolver}({arguments})"));
            return tree;
        }

        private ISyntax IsTagBool()
        {
            var tree = new TreeSyntaxNode();
            tree.Add(new TabSimpleSyntax(2, "[Key(0)]"));
            tree.Add(new TabSimpleSyntax(2, "public bool IsTag;"));
            return tree;
        }

        #endregion

        #region CustomAndUniversalResolvers

        public List<(string name, string data)> JSONResolvers()
        {
            var list = new List<(string name, string data)>(256);

            foreach (var l in Program.LinkedNodes)
            {
                if (l.Value.IsAbstract)
                    continue;

                list.Add((l.Value.Name, GetUniversalResolver(l.Value).ToString()));
            }

            return list;
        }

        private ISyntax GetUniversalResolver(LinkedNode linkedNode)
        {

            var tree = new TreeSyntaxNode();
            var usings = new TreeSyntaxNode();
            var fields = new TreeSyntaxNode();
            var inFunc = new TreeSyntaxNode();
            var defaultConstructor = new TreeSyntaxNode();
            var outFunc = new TreeSyntaxNode();
            var out2EntityFunc = new TreeSyntaxNode();

            var name = linkedNode.Name;

            tree.Add(usings);

            usings.AddUnique(new UsingSyntax("System"));
            usings.AddUnique(new UsingSyntax("HECSFramework.Core"));
            usings.AddUnique(new UsingSyntax("Newtonsoft.Json"));
            usings.AddUnique(new UsingSyntax("HECSFramework.Serialize"));

            tree.Add(new TabSimpleSyntax(1, "[JsonObject, Serializable]"));
            tree.Add(new TabSimpleSyntax(1, $"public partial struct {name + JSONResolver} : IJSONResolver<{name},{name + JSONResolver}>"));
            tree.Add(new LeftScopeSyntax(1));
            tree.Add(fields);
            tree.Add(new ParagraphSyntax());
            tree.Add(new TabSimpleSyntax(2, $"public {name + JSONResolver} In(ref {name} {name.ToLower()})"));
            tree.Add(new LeftScopeSyntax(2));
            tree.Add(inFunc);
            tree.Add(new RightScopeSyntax(2));

            tree.Add(new TabSimpleSyntax(2, $"public void Out(ref {name} {name.ToLower()})"));
            tree.Add(new LeftScopeSyntax(2));
            tree.Add(outFunc);
            tree.Add(new RightScopeSyntax(2));
            tree.Add(new RightScopeSyntax(1));
            tree.Add(new ParagraphSyntax());

            //((c.Members.ToArray()[0] as FieldDeclarationSyntax).AttributeLists.ToArray()[0].Attributes.ToArray()[0] as AttributeSyntax).ArgumentList.Arguments.ToArray()[0].ToString()
            var typeFields = new List<GatheredField>(128);
            List<(string type, string name)> fieldsForConstructor = new List<(string type, string name)>();

            foreach (var m in linkedNode.Members())
            {
                var validate = IsValidField(m);

                if (!validate.valid) continue;

                string type = "";
                string fieldName = "";

                var member = m.MemberDeclarationSyntax;

                if (member is FieldDeclarationSyntax field)
                {
                    fieldName = field.Declaration.Variables[0].Identifier.ToString();
                    type = field.Declaration.Type.ToString();
                }

                if (member is PropertyDeclarationSyntax property)
                {
                    fieldName = property.Identifier.Text;
                    type = property.Type.ToString();
                }

                if (string.IsNullOrEmpty(type) || string.IsNullOrEmpty(fieldName))
                    throw new Exception("we dont have type for field " + m.ToString());

                if (validate.valid)
                {
                    if (typeFields.Any(x => x.Order == validate.Order || x.FieldName == fieldName))
                        continue;

                    typeFields.Add(new GatheredField
                    {
                        Order = validate.Order,
                        Type = type,
                        FieldName = fieldName,
                        ResolverName = validate.resolver,
                        Node = member
                    });
                }
            }

            typeFields = typeFields.Distinct().ToList();

            foreach (var f in typeFields)
            {

                fields.Add(new TabSimpleSyntax(2, $"[JsonProperty({CParse.Quote}{f.FieldName}{CParse.Quote})]"));

                if (string.IsNullOrEmpty(f.ResolverName))
                    fields.Add(new TabSimpleSyntax(2, $"public {f.Type} {f.FieldName};"));
                else
                    fields.Add(new TabSimpleSyntax(2, $"public {f.ResolverName} {f.FieldName};"));

                fieldsForConstructor.Add((f.Type, f.FieldName));

                if (f.Node is PropertyDeclarationSyntax declarationSyntax && declarationSyntax.Type.ToString().Contains("ReactiveValue"))
                {
                    inFunc.Add(new TabSimpleSyntax(3, $"this.{f.FieldName} = {linkedNode.Name.ToLower()}.{f.FieldName}.CurrentValue;"));
                    outFunc.Add(new TabSimpleSyntax(3, $"{linkedNode.Name.ToLower()}.{f.FieldName}.CurrentValue = this.{f.FieldName};"));
                }
                else
                {
                    if (string.IsNullOrEmpty(f.ResolverName))
                    {
                        inFunc.Add(new TabSimpleSyntax(3, $"this.{f.FieldName} = {linkedNode.Name.ToLower()}.{f.FieldName};"));
                        outFunc.Add(new TabSimpleSyntax(3, $"{linkedNode.Name.ToLower()}.{f.FieldName} = this.{f.FieldName};"));
                    }
                    else
                    {
                        usings.AddUnique(new UsingSyntax("HECSFramework.Serialize"));
                        inFunc.Add(new TabSimpleSyntax(3, $"this.{f.FieldName} = new {f.ResolverName}().In(ref {linkedNode.Name.ToLower()}.{f.FieldName});"));
                        outFunc.Add(new TabSimpleSyntax(3, $"this.{f.FieldName}.Out(ref {linkedNode.Name.ToLower()}.{f.FieldName});"));
                    }
                }
            }

            if (linkedNode.IsAfterSerialization)
            {
                outFunc.Add(new TabSimpleSyntax(3, $"{linkedNode.Name.ToLower()}.AfterSync();"));
            }

            ////defaultConstructor.Add(DefaultConstructor(c, fieldsForConstructor, fields, constructor));
            

            usings.Tree.Add(new ParagraphSyntax());

            if (linkedNode.Members().Any(x=> x.IsPrivate && x.IsHECSSerializeField))
            {

                tree.AddUnique(new ParagraphSyntax());
                tree.Add(GetPartialClassForSerializePrivateFields(linkedNode.Type,
                    name + JSONResolver, out var saveBody, out var loadBody));

                var jsonResolverName = "resolver";

                foreach (var m in linkedNode.Members())
                {
                    if (m.IsPrivate && m.IsHECSSerializeField)
                    {
                        var specialResolver = m.GetSpecialResolverName();
                        var fieldName = m.MemberDeclarationSyntax.GetMemberFieldName();


                        if (!string.IsNullOrEmpty(specialResolver))
                        {
                            fields.Add(new TabSimpleSyntax(2, $"[JsonProperty({CParse.Quote}{fieldName}{CParse.Quote})]"));
                            fields.Add(new TabSimpleSyntax(2, $"public {specialResolver} {m.MemberDeclarationSyntax.GetMemberFieldName()};"));

                            inFunc.AddUnique(new TabSimpleSyntax(3, $"{linkedNode.Name.ToLower()}.SaveToJSONResolver(ref this);"));;
                            saveBody.AddUnique(new TabSimpleSyntax(3, $"{jsonResolverName}.{fieldName} = new {specialResolver}().In(ref {fieldName});"));
                            loadBody.AddUnique(new TabSimpleSyntax(3, $"{jsonResolverName}.{fieldName}.Out(ref {fieldName});"));

                            outFunc.AddUnique(new TabSimpleSyntax(3, $"{linkedNode.Name.ToLower()}.LoadFromJSONResolver(ref this);"));
                        }
                        else
                        {
                            fields.Add(new TabSimpleSyntax(2, $"[JsonProperty({CParse.Quote}{fieldName}{CParse.Quote})]"));
                            fields.Add(new TabSimpleSyntax(2, $"public {m.MemberDeclarationSyntax.GetMemberType()} {m.MemberDeclarationSyntax.GetMemberFieldName()};"));

                            inFunc.AddUnique(new TabSimpleSyntax(3, $"{linkedNode.Name.ToLower()}.SaveToJSONResolver(ref this);"));
                            saveBody.AddUnique(new TabSimpleSyntax(3, $"{jsonResolverName.ToLower()}.{fieldName} = {fieldName};"));
                            loadBody.AddUnique(new TabSimpleSyntax(3, $"{fieldName} = {jsonResolverName.ToLower()}.{fieldName};"));

                            outFunc.AddUnique(new TabSimpleSyntax(3, $"{linkedNode.Name.ToLower()}.LoadFromJSONResolver(ref this);"));
                        }
                    }
                }
            }

            inFunc.Add(new TabSimpleSyntax(3, "return this;"));
            return tree;
        }

        #endregion
    }
}
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
        public const string Resolver = "Resolver";
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

        private HashSet<ClassDeclarationSyntax> systemCasheParentsAndPartial = new HashSet<ClassDeclarationSyntax>(64);

        #region Resolvers
        public List<(string name, string content)> GetSerializationResolvers()
        {
            var list = new List<(string, string)>();

            foreach (var c in Program.componentOverData.Values)
            {
                if (c.IsAbstract)
                    continue;

                var needContinue = false;

                if (c.IsPartial)
                {
                    var attr2 = c.Parts.SelectMany(x => x.AttributeLists);

                    if (attr2 != null)
                    {
                        foreach (var attributeList in attr2)
                        {
                            foreach (var a in attributeList.Attributes)
                            {
                                if (a.Name.ToString() == "HECSDefaultResolver")
                                {
                                    containersSolve.Add(c.ClassDeclaration);
                                    needContinue = true;
                                    break;
                                }
                            }
                        }
                    }
                }
                else
                {
                    var attributeList = c.ClassDeclaration.AttributeLists;

                    foreach (var a in attributeList)
                    {
                        foreach (var attr in a.Attributes)
                        {
                            if (attr.Name.ToString() == "HECSDefaultResolver")
                            {
                                containersSolve.Add(c.ClassDeclaration);
                                needContinue = true;
                                break;
                            }
                        }
                    }
                }

                if (needContinue)
                    continue;

                containersSolve.Add(c.ClassDeclaration);
                needResolver.Add(c.ClassDeclaration);
            }

            foreach (var c in needResolver)
            {
                list.Add((c.Identifier.ValueText + Resolver + Cs, GetResolver(Program.componentOverData[c.Identifier.ValueText]).ToString()));
            }

            return list;
        }

        public (bool valid, int Order, string resolver) IsValidField(MemberDeclarationSyntax fieldDeclarationSyntax)
        {
            if (fieldDeclarationSyntax is PropertyDeclarationSyntax property)
            {
                if (property.AccessorList == null)
                    return (false, -1, string.Empty);

                var t = property.AccessorList.Accessors.FirstOrDefault(x => x.Keyword.Text == "set");

                if (t == null || t.Modifiers.Any(x => x.IsKind(SyntaxKind.PrivateKeyword) || x.IsKind(SyntaxKind.ProtectedKeyword)))
                    return (false, -1, string.Empty);
            }

            foreach (var a in fieldDeclarationSyntax.AttributeLists.SelectMany(x => x.Attributes).ToArray())
            {
                //todo "разобраться аккуратно с аттрибутами поля"
                if (a.Name.ToString() == ("Field") && fieldDeclarationSyntax.Modifiers.ToString().Contains("public"))
                {
                    if (a.ArgumentList == null)
                        continue;
                    var resolver = string.Empty;

                    var arguments = a.ArgumentList.Arguments.ToArray();
                    var intValue = int.Parse(arguments[0].ToString());

                    if (arguments.Length > 1)
                    {
                        var data = arguments[1].ToString();
                        data = data.Replace("typeof(", "");
                        data = data.Replace(")", "");
                        resolver = data;
                    }

                    return (true, intValue, resolver);
                }
            }

            return (false, -1, string.Empty);
        }

        public static void GetNamespace(MemberDeclarationSyntax declaration, ISyntax tree)
        {
            if (declaration is FieldDeclarationSyntax field)
            {
                if (field.Declaration.Type is GenericNameSyntax generic)
                {
                    if (GetNameSpaceForCollection(generic.Identifier.Value.ToString(), out var namespaceCollection))
                    {
                        tree.AddUnique(new UsingSyntax(namespaceCollection));
                    }

                    foreach (var a in generic.TypeArgumentList.Arguments)
                    {
                        var arg = a.ToString();

                        if (Program.structByName.TryGetValue(arg, out var value))
                        {
                            if (value.Parent != null && value.Parent is NamespaceDeclarationSyntax ns)
                            {
                                tree.AddUnique(new UsingSyntax(ns.Name.ToString()));
                            }
                        }

                        if (Program.classesByName.TryGetValue(arg, out var classObject))
                        {
                            if (classObject.Parent != null && classObject.Parent is NamespaceDeclarationSyntax ns)
                            {
                                tree.AddUnique(new UsingSyntax(ns.Name.ToString()));
                            }
                        }
                    }
                }
                else
                {

                    var arg = field.Declaration.Type.ToString();

                    if (Program.structByName.TryGetValue(arg, out var value))
                    {
                        if (value.Parent != null && value.Parent is NamespaceDeclarationSyntax ns)
                        {
                            tree.AddUnique(new UsingSyntax(ns.Name.ToString()));
                        }
                    }

                    if (Program.classesByName.TryGetValue(arg, out var classObject))
                    {
                        if (classObject.Parent != null && classObject.Parent is NamespaceDeclarationSyntax ns)
                        {
                            tree.AddUnique(new UsingSyntax(ns.Name.ToString()));
                        }
                    }
                }
            }

            if (declaration is PropertyDeclarationSyntax property)
            {


                if (property.Type is GenericNameSyntax generic)
                {
                    foreach (var a in generic.TypeArgumentList.Arguments)
                    {
                        var arg = a.ToString();

                        if (Program.structByName.TryGetValue(arg, out var value))
                        {
                            if (value.Parent != null && value.Parent is NamespaceDeclarationSyntax ns)
                            {
                                tree.AddUnique(new UsingSyntax(ns.Name.ToString()));
                            }
                        }

                        if (Program.classesByName.TryGetValue(arg, out var classObject))
                        {
                            if (classObject.Parent != null && classObject.Parent is NamespaceDeclarationSyntax ns)
                            {
                                tree.AddUnique(new UsingSyntax(ns.Name.ToString()));
                            }
                        }
                    }
                }
                else
                {

                    var arg = property.Type.ToString();

                    if (Program.structByName.TryGetValue(arg, out var value))
                    {
                        if (value.Parent != null && value.Parent is NamespaceDeclarationSyntax ns)
                        {
                            tree.AddUnique(new UsingSyntax(ns.Name.ToString()));
                        }
                    }

                    if (Program.classesByName.TryGetValue(arg, out var classObject))
                    {
                        if (classObject.Parent != null && classObject.Parent is NamespaceDeclarationSyntax ns)
                        {
                            tree.AddUnique(new UsingSyntax(ns.Name.ToString()));
                        }
                    }
                }
            }
        }

        public ISyntax GetPartialClassForSerializePrivateFields(ClassDeclarationSyntax classDeclarationSyntax, string resolver, out ISyntax saveBody, out ISyntax loadBody)
        {
            var classSyntax = new TreeSyntaxNode();

            classSyntax.Add(new NameSpaceSyntax("Components"));
            classSyntax.Add(new LeftScopeSyntax());

            classSyntax.Add(new TabSimpleSyntax(1,
                $"public partial class {classDeclarationSyntax.Identifier.ValueText} : " +
                $"ISaveToResolver<{resolver}>, ILoadFromResolver<{resolver}>"));

            classSyntax.Add(new LeftScopeSyntax(1));
            classSyntax.Add(GetSaveResolverBody(resolver, out saveBody));
            classSyntax.Add(new ParagraphSyntax());
            classSyntax.Add(GetLoadResolverBody(resolver, out loadBody));
            classSyntax.Add(new RightScopeSyntax(1));
            classSyntax.Add(new RightScopeSyntax());
            return classSyntax;
        }

        public ISyntax GetSaveResolverBody(string resolver, out ISyntax body)
        {
            var tree = new TreeSyntaxNode();
            body = new TreeSyntaxNode();

            tree.Add(new TabSimpleSyntax(2, $"public void Save(ref {resolver} resolver)"));
            tree.Add(new LeftScopeSyntax(2));
            tree.Add(body);
            tree.Add(new RightScopeSyntax(2));

            return tree;
        }

        public ISyntax GetLoadResolverBody(string resolver, out ISyntax body)
        {
            var tree = new TreeSyntaxNode();
            body = new TreeSyntaxNode();

            tree.Add(new TabSimpleSyntax(2, $"public void Load(ref {resolver} resolver)"));
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
                    AddUniqueSyntax(currentUsings, new UsingSyntax("System.Collections.Generic"));
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
            var interfaces = Program.interfaces.Where(x => x.Identifier.ValueText == nameOfNode).ToList();

            var need = new List<TypeDeclarationSyntax>();
            need.AddRange(classes);
            need.AddRange(structs);
            //need.AddRange(interfaces);

            foreach (var i in interfaces)
            {
                if (i.Parent is NamespaceDeclarationSyntax nspace)
                {
                    if (nspace.Name is IdentifierNameSyntax identifier)
                    {
                        AddUniqueSyntax(tree, new UsingSyntax(identifier.ToString()));
                    }
                    else if (nspace.Name is QualifiedNameSyntax identifier2)
                    {
                        AddUniqueSyntax(tree, new UsingSyntax(identifier2.ToString()));
                    }
                }
            }

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

                defaultconstructorSignature.Add(new TabSimpleSyntax(2, $"public {type.Name + Resolver}({arguments})"));
                return tree;
            }

            for (int i = 0; i < data.Count; i++)
            {
                (string type, string name) d = data[i];
                var needComma = i < data.Count - 1 ? CParse.Comma : "";

                arguments.Add(new SimpleSyntax($"{d.type} {d.name}{needComma}"));
                defaultConstructor.Add(new TabSimpleSyntax(3, $"this.{d.name} = {d.name};"));
            }

            defaultconstructorSignature.Add(new TabSimpleSyntax(2, $"public {type.Name + Resolver}({arguments})"));
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

        #region  ResolversMap
        public string GetResolverMap()
        {
            var tree = new TreeSyntaxNode();

            tree.Add(new UsingSyntax("Components"));
            tree.Add(new UsingSyntax("HECSFramework.Core"));
            tree.Add(new UsingSyntax("MessagePack.Resolvers"));
            tree.Add(new UsingSyntax("MessagePack", 1));
            tree.Add(GetUnionResolvers());
            tree.Add(new ParagraphSyntax());
            tree.Add(new NameSpaceSyntax("HECSFramework.Core"));
            tree.Add(new LeftScopeSyntax());
            tree.Add(new TabSimpleSyntax(1, "public partial class ResolversMap"));
            tree.Add(new LeftScopeSyntax(1));
            //tree.Add(GetResolverMapStaticConstructor()); we move this to client when mpc codogen
            tree.Add(ResolverMapConstructor());
            tree.Add(LoadDataFromContainerSwitch());
            tree.Add(GetContainerForComponentFuncProvider());
            tree.Add(ProcessComponents());
            tree.Add(GetComponentFromContainerFuncRealisation());
            tree.Add(ProcessResolverContainerRealisation());
            tree.Add(new RightScopeSyntax(1));
            tree.Add(new RightScopeSyntax());
            return tree.ToString();
        }

        private ISyntax GetResolverMapStaticConstructor()
        {
            var tree = new TreeSyntaxNode();

            tree.Add(new TabSimpleSyntax(2, "private static bool isMessagePackInited;"));
            tree.Add(new TabSimpleSyntax(3, "static ResolversMap()"));
            tree.Add(new LeftScopeSyntax(3));
            tree.Add(new TabSimpleSyntax(4, "if (isMessagePackInited)"));
            tree.Add(new TabSimpleSyntax(5, "return;"));
            tree.Add(new TabSimpleSyntax(4, "StaticCompositeResolver.Instance.Register(StandardResolver.Instance, GeneratedResolver.Instance);"));
            tree.Add(new TabSimpleSyntax(4, "isMessagePackInited = true;"));
            tree.Add(new TabSimpleSyntax(4, "MessagePackSerializer.DefaultOptions = MessagePackSerializerOptions.Standard.WithResolver(StaticCompositeResolver.Instance);"));
            tree.Add(new RightScopeSyntax());
            tree.Add(new ParagraphSyntax());

            return tree;
        }

        private ISyntax GetUnionResolvers()
        {
            var tree = new TreeSyntaxNode();
            var unionPart = new TreeSyntaxNode();
            tree.Add(unionPart);
            tree.Add(new TabSimpleSyntax(0, "public partial interface IData { }"));

            for (int i = 0; i < containersSolve.Count; i++)
            {
                var name = containersSolve[i].Identifier.ValueText;
                unionPart.Add(new TabSimpleSyntax(0, $"[Union({i}, typeof({name}Resolver))]"));
            }

            return tree;
        }

        private ISyntax ProcessResolverContainerRealisation()
        {
            var tree = new TreeSyntaxNode();
            var caseBody = new TreeSyntaxNode();

            tree.Add(new ParagraphSyntax());
            tree.Add(new TabSimpleSyntax(2, "private void ProcessResolverContainerRealisation(ref ResolverDataContainer dataContainerForResolving, ref Entity entity)"));
            tree.Add(new LeftScopeSyntax(2));
            tree.Add(new TabSimpleSyntax(3, "switch (dataContainerForResolving.TypeHashCode)"));
            tree.Add(new LeftScopeSyntax(3));
            tree.Add(caseBody);
            tree.Add(new RightScopeSyntax(3));
            tree.Add(new RightScopeSyntax(2));

            foreach (var container in containersSolve)
            {
                var name = container.Identifier.ValueText;
                caseBody.Add(new TabSimpleSyntax(4, $"case {IndexGenerator.GetIndexForType(name)}:"));
                caseBody.Add(new TabSimpleSyntax(5, $"var {name}{Resolver.ToLower()} = MessagePackSerializer.Deserialize<{name}{Resolver}>(dataContainerForResolving.Data);"));
                caseBody.Add(new TabSimpleSyntax(5, $"var {name}component = entity.GetOrAddComponent<{name}>();"));
                caseBody.Add(new TabSimpleSyntax(5, $"{name}{Resolver.ToLower()}.Out(ref {name}component);"));
                caseBody.Add(new TabSimpleSyntax(5, $"break;"));
            }

            return tree;
        }

        private ISyntax GetComponentFromContainerFuncRealisation()
        {
            var tree = new TreeSyntaxNode();
            var caseBody = new TreeSyntaxNode();

            tree.Add(new ParagraphSyntax());
            tree.Add(new TabSimpleSyntax(2, "private IComponent GetComponentFromContainerFuncRealisation(ResolverDataContainer resolverDataContainer)"));
            tree.Add(new LeftScopeSyntax(2));
            tree.Add(new TabSimpleSyntax(3, "switch (resolverDataContainer.TypeHashCode)"));
            tree.Add(new LeftScopeSyntax(3));
            tree.Add(caseBody);
            tree.Add(new RightScopeSyntax(3));
            tree.Add(new TabSimpleSyntax(4, "return default;"));
            tree.Add(new RightScopeSyntax(2));

            foreach (var container in containersSolve)
            {
                var name = container.Identifier.ValueText;
                caseBody.Add(new TabSimpleSyntax(4, $"case {IndexGenerator.GetIndexForType(name)}:"));
                caseBody.Add(new TabSimpleSyntax(5, $"var {name}new = new {name}();"));
                caseBody.Add(new TabSimpleSyntax(5, $"var {name}data = MessagePackSerializer.Deserialize<{name}{Resolver}>(resolverDataContainer.Data);"));
                caseBody.Add(new TabSimpleSyntax(5, $"{name}data.Out(ref {name}new);"));
                caseBody.Add(new TabSimpleSyntax(5, $"return {name}new;"));
            }

            return tree;
        }

        private ISyntax ProcessComponents()
        {
            var tree = new TreeSyntaxNode();
            var caseBody = new TreeSyntaxNode();

            tree.Add(new ParagraphSyntax());
            tree.Add(new TabSimpleSyntax(2, $"private void ProcessComponents(ref {ResolverContainer} dataContainerForResolving, int worldIndex)"));
            tree.Add(new LeftScopeSyntax(2));
            tree.Add(new TabSimpleSyntax(3, "switch (dataContainerForResolving.TypeHashCode)"));
            tree.Add(new LeftScopeSyntax(3));
            tree.Add(caseBody);
            tree.Add(new RightScopeSyntax(3));
            tree.Add(new RightScopeSyntax(2));

            foreach (var container in containersSolve)
            {
                var name = container.Identifier.ValueText;
                caseBody.Add(new TabSimpleSyntax(4, $"case {IndexGenerator.GetIndexForType(name)}:"));
                caseBody.Add(new TabSimpleSyntax(5, $"var {name}{Resolver.ToLower()} = MessagePackSerializer.Deserialize<{name}{Resolver}>(dataContainerForResolving.Data);"));
                caseBody.Add(new TabSimpleSyntax(5, $"if (EntityManager.TryGetEntityByID(dataContainerForResolving.EntityGuid, out var entityOf{name}))"));
                caseBody.Add(new LeftScopeSyntax(5));
                caseBody.Add(new TabSimpleSyntax(6, $"var {name}component = entityOf{name}.GetOrAddComponent<{name}>();"));
                caseBody.Add(new TabSimpleSyntax(6, $"{name}{Resolver.ToLower()}.Out(ref {name}component);"));
                caseBody.Add(new RightScopeSyntax(5));
                caseBody.Add(new TabSimpleSyntax(5, $"break;"));
            }

            return tree;
        }

        private ISyntax GetContainerForComponentFuncProvider()
        {
            var tree = new TreeSyntaxNode();
            var caseBody = new TreeSyntaxNode();

            tree.Add(new ParagraphSyntax());
            tree.Add(new TabSimpleSyntax(2, $"private {ResolverContainer} GetContainerForComponentFuncProvider<T>(T component) where T: IComponent"));
            tree.Add(new LeftScopeSyntax(2));
            tree.Add(new TabSimpleSyntax(3, "switch (component.GetTypeHashCode)"));
            tree.Add(new LeftScopeSyntax(3));
            tree.Add(caseBody);
            tree.Add(new RightScopeSyntax(3));
            tree.Add(new TabSimpleSyntax(3, "return default;"));
            tree.Add(new RightScopeSyntax(2));

            foreach (var container in containersSolve)
            {
                var name = container.Identifier.ValueText;

                var lowerContainerName = (name + Resolver).ToLower();
                caseBody.Add(new TabSimpleSyntax(4, $"case {IndexGenerator.GetIndexForType(name)}:"));
                caseBody.Add(new TabSimpleSyntax(5, $"var {lowerContainerName} = component as {name};"));
                caseBody.Add(new TabSimpleSyntax(5, $"var {name}Data = new {name + Resolver}().In(ref {lowerContainerName});"));
                caseBody.Add(new TabSimpleSyntax(5, $"return PackComponentToContainer(component, {name}Data);"));
            }

            return tree;
        }

        private ISyntax LoadDataFromContainerSwitch()
        {
            var tree = new TreeSyntaxNode();
            tree.Add(new ParagraphSyntax());
            tree.Add(new TabSimpleSyntax(2, $"partial void LoadDataFromContainerSwitch({"ResolverDataContainer"} dataContainerForResolving, int worldIndex)"));
            tree.Add(new LeftScopeSyntax(2));
            tree.Add(new TabSimpleSyntax(3, "switch (dataContainerForResolving.Type)"));
            tree.Add(new LeftScopeSyntax(3));
            tree.Add(new TabSimpleSyntax(4, "case 0:"));
            tree.Add(new TabSimpleSyntax(5, "ProcessComponents(ref dataContainerForResolving, worldIndex);"));
            tree.Add(new TabSimpleSyntax(5, "break;"));
            tree.Add(new RightScopeSyntax(3));
            tree.Add(new RightScopeSyntax(2));
            return tree;
        }

        private ISyntax ResolverMapConstructor()
        {
            var tree = new TreeSyntaxNode();

            tree.Add(new TabSimpleSyntax(2, "public ResolversMap()"));
            tree.Add(new LeftScopeSyntax(2));
            tree.Add(new TabSimpleSyntax(3, "GetComponentContainerFunc = GetContainerForComponentFuncProvider;"));
            tree.Add(new TabSimpleSyntax(3, "ProcessResolverContainer = ProcessResolverContainerRealisation;"));
            tree.Add(new TabSimpleSyntax(3, "GetComponentFromContainer = GetComponentFromContainerFuncRealisation;"));
            tree.Add(new TabSimpleSyntax(3, "InitPartialCommandResolvers();"));
            tree.Add(new TabSimpleSyntax(3, "InitCustomResolvers();"));
            tree.Add(new RightScopeSyntax(2));
            tree.Add(new TabSimpleSyntax(2, "partial void InitCustomResolvers();"));
            return tree;
        }
        #endregion

        #region CustomAndUniversalResolvers

        public string GetCustomResolversMap()
        {
            var tree = new TreeSyntaxNode();
            var usings = new TreeSyntaxNode();
            tree.Add(usings);

            usings.AddUnique(new UsingSyntax("System"));
            usings.AddUnique(new UsingSyntax("System.Collections.Generic"));
            tree.Add(new ParagraphSyntax());
            tree.Add(new NameSpaceSyntax("HECSFramework.Core"));
            tree.Add(new LeftScopeSyntax());
            tree.Add(GetUniversalResolvers(usings));
            tree.Add(new TabSimpleSyntax(1, "public partial class ResolversMap"));
            tree.Add(new LeftScopeSyntax(1));
            tree.Add(GetCustomProvidersPartialInitMethod());
            tree.Add(GetDictionaryHelper.GetDictionaryMethod("GetTypeToCustomResolver", "Type", "CustomResolverProviderBase", 2, out var customTypeToResolvers));
            tree.Add(GetDictionaryHelper.GetDictionaryMethod("GetTypeCodeToCustomResolver", "int", "CustomResolverProviderBase", 2, out var typeCodeToCustomResolver));
            tree.Add(GetDictionaryHelper.GetDictionaryMethod("GetTypeIndexToType", "int", "Type", 2, out var typeIndexToType));
            tree.Add(new RightScopeSyntax(1));
            tree.Add(new RightScopeSyntax());

            foreach (var cr in Program.customHecsResolvers)
            {
                if (Program.classesByName.TryGetValue(cr.Key, out var classNeeded))
                {
                    if (classNeeded.Parent is NamespaceDeclarationSyntax namespaceDeclaration)
                    {
                        usings.AddUnique(new UsingSyntax(namespaceDeclaration.Name.ToString()));
                    }
                }

                customTypeToResolvers.Tree.Add(GetDictionaryHelper.DictionaryBodyRecord(4, $"typeof({cr.Key})",
                    $"new CustomResolverProvider<{cr.Key}, {cr.Value.ResolverName}>()"));

                typeCodeToCustomResolver.Tree.Add(GetDictionaryHelper.DictionaryBodyRecord(4, $"{IndexGenerator.GenerateIndex(cr.Key)}",
                    $"new CustomResolverProvider<{cr.Key}, {cr.Value.ResolverName}>()"));

                typeIndexToType.Tree.Add(GetDictionaryHelper.DictionaryBodyRecord(4, $"{IndexGenerator.GenerateIndex(cr.Key)}",
                    $"typeof({cr.Key})"));
            }

            usings.Add(new ParagraphSyntax());
            return tree.ToString();
        }

        private ISyntax GetUniversalResolvers(ISyntax usings)
        {
            var tree = new TreeSyntaxNode();
            foreach (var ur in Program.hecsResolverCollection)
            {
                tree.Add(GetUniversalResolver(ur.Value, usings));
            }

            return tree;
        }

        private ISyntax GetUniversalResolver(LinkedNode c, ISyntax usings)
        {
            c.GetAllParentsAndParts(c.Parts);

            var tree = new TreeSyntaxNode();
            var fields = new TreeSyntaxNode();
            var constructor = new TreeSyntaxNode();
            var defaultConstructor = new TreeSyntaxNode();
            var outFunc = new TreeSyntaxNode();
            var out2EntityFunc = new TreeSyntaxNode();

            var name = c.Name;

            usings.AddUnique(new UsingSyntax("System"));
            usings.AddUnique(new UsingSyntax("Commands"));
            usings.AddUnique(new UsingSyntax("Components"));
            usings.AddUnique(new UsingSyntax("MessagePack"));
            usings.AddUnique(new UsingSyntax("HECSFramework.Serialize"));

            tree.Add(new TabSimpleSyntax(1, "[MessagePackObject, Serializable]"));
            tree.Add(new TabSimpleSyntax(1, $"public partial struct {name + Resolver} : IResolver<{name + Resolver},{name}>, IData"));
            tree.Add(new LeftScopeSyntax(1));
            tree.Add(fields);
            tree.Add(new ParagraphSyntax());
            tree.Add(new TabSimpleSyntax(2, $"public {name + Resolver} In(ref {name} {name.ToLower()})"));
            tree.Add(new LeftScopeSyntax(2));
            tree.Add(constructor);
            tree.Add(new RightScopeSyntax(2));

            tree.Add(new TabSimpleSyntax(2, $"public void Out(ref {name} {name.ToLower()})"));
            tree.Add(new LeftScopeSyntax(2));
            tree.Add(outFunc);
            tree.Add(new RightScopeSyntax(2));
            tree.Add(new RightScopeSyntax(1));
            tree.Add(new ParagraphSyntax());


            c.Interfaces.Clear();
            c.GetInterfaces(c.Interfaces);

            if (c.Interfaces.Any(x => x.Name == "IBeforeSerializationComponent"))
                constructor.Add(new TabSimpleSyntax(3, $"{c.Name.ToLower()}.BeforeSync();"));

            //((c.Members.ToArray()[0] as FieldDeclarationSyntax).AttributeLists.ToArray()[0].Attributes.ToArray()[0] as AttributeSyntax).ArgumentList.Arguments.ToArray()[0].ToString()
            var typeFields = new List<GatheredField>(128);
            List<(string type, string name)> fieldsForConstructor = new List<(string type, string name)>();

            foreach (var parts in c.Parts)
            {
                foreach (var m in parts.Members)
                {
                    if (m is MemberDeclarationSyntax member)
                    {
                        var validate = IsValidField(member);

                        if (!validate.valid) continue;


                        GetNamespace(member, usings);

                        string type = "";
                        string fieldName = "";

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
                }
            }

            typeFields = typeFields.Distinct().ToList();

            foreach (var f in typeFields)
            {

                fields.Add(new TabSimpleSyntax(2, $"[Key({f.Order})]"));

                if (string.IsNullOrEmpty(f.ResolverName))
                    fields.Add(new TabSimpleSyntax(2, $"public {f.Type} {f.FieldName};"));
                else
                    fields.Add(new TabSimpleSyntax(2, $"public {f.ResolverName} {f.FieldName};"));

                fieldsForConstructor.Add((f.Type, f.FieldName));

                if (f.Node is PropertyDeclarationSyntax declarationSyntax && declarationSyntax.Type.ToString().Contains("ReactiveValue"))
                {
                    constructor.Add(new TabSimpleSyntax(3, $"this.{f.FieldName} = {c.Name.ToLower()}.{f.FieldName}.CurrentValue;"));
                    outFunc.Add(new TabSimpleSyntax(3, $"{c.Name.ToLower()}.{f.FieldName}.CurrentValue = this.{f.FieldName};"));
                }
                else
                {
                    if (string.IsNullOrEmpty(f.ResolverName))
                    {
                        constructor.Add(new TabSimpleSyntax(3, $"this.{f.FieldName} = {c.Name.ToLower()}.{f.FieldName};"));
                        outFunc.Add(new TabSimpleSyntax(3, $"{c.Name.ToLower()}.{f.FieldName} = this.{f.FieldName};"));
                    }
                    else
                    {
                        AddUniqueSyntax(usings, new UsingSyntax("HECSFramework.Serialize"));
                        constructor.Add(new TabSimpleSyntax(3, $"this.{f.FieldName} = new {f.ResolverName}().In(ref {c.Name.ToLower()}.{f.FieldName});"));
                        outFunc.Add(new TabSimpleSyntax(3, $"this.{f.FieldName}.Out(ref {c.Name.ToLower()}.{f.FieldName});"));
                    }
                }
            }

            if (c.Interfaces.Any(x => x.Name == "IAfterSerializationComponent"))
            {
                outFunc.Add(new TabSimpleSyntax(3, $"{c.Name.ToLower()}.AfterSync();"));
            }

            ////defaultConstructor.Add(DefaultConstructor(c, fieldsForConstructor, fields, constructor));
            constructor.Add(new TabSimpleSyntax(3, "return this;"));

            usings.Tree.Add(new ParagraphSyntax());
            return tree;
        }

        private ISyntax GetCustomProvidersPartialInitMethod()
        {
            var tree = new TreeSyntaxNode();

            tree.Add(new TabSimpleSyntax(2, "partial void InitCustomResolvers()"));
            tree.Add(new LeftScopeSyntax(2));
            tree.Add(new TabSimpleSyntax(3, "typeToCustomResolver = GetTypeToCustomResolver();"));
            tree.Add(new TabSimpleSyntax(3, "typeCodeToCustomResolver = GetTypeCodeToCustomResolver();"));
            tree.Add(new TabSimpleSyntax(3, "getTypeIndexToType = GetTypeIndexToType();"));
            tree.Add(new RightScopeSyntax(2));

            return tree;
        }


        #endregion

        #region CommandsResolvers

        /// <summary>
        /// we generate here commands map and short ids staff
        /// </summary>
        /// <param name="commands"></param>
        /// <returns></returns>
        public string GenerateNetworkCommandsAndShortIdsMap(List<StructDeclarationSyntax> commands)
        {
            var tree = new TreeSyntaxNode();
            var resolvers = new TreeSyntaxNode();
            var typeToIdDictionary = new TreeSyntaxNode();
            var dictionaryBody = new TreeSyntaxNode();
            var genericMethod = new TreeSyntaxNode();

            tree.Add(new UsingSyntax("Commands"));
            tree.Add(new UsingSyntax("Components"));
            tree.Add(new UsingSyntax("System"));
            tree.Add(new UsingSyntax("HECSFramework.Serialize"));
            tree.Add(new UsingSyntax("System.Collections.Generic", 1));
            tree.Add(new NameSpaceSyntax("HECSFramework.Core"));
            tree.Add(new LeftScopeSyntax());
            tree.Add(new TabSimpleSyntax(1, "public partial class ResolversMap"));
            tree.Add(new LeftScopeSyntax(1));
            tree.Add(new TabSimpleSyntax(2, "public Dictionary<int, ICommandResolver> Map = new Dictionary<int, ICommandResolver>"));
            tree.Add(new LeftScopeSyntax(2));
            tree.Add(resolvers);
            tree.Add(new RightScopeSyntax(2, true));
            tree.Add(new ParagraphSyntax());
            tree.Add(typeToIdDictionary);
            tree.Add(new ParagraphSyntax());
            tree.Add(GetShortIdPart());
            tree.Add(new ParagraphSyntax());
            tree.Add(InitPartialCommandResolvers());
            tree.Add(new RightScopeSyntax(1));
            tree.Add(new RightScopeSyntax(0));

            foreach (var t in commands)
                resolvers.Add(GetCommandResolver(t));

            typeToIdDictionary.Add(new TabSimpleSyntax(2, "public Dictionary<Type, int> CommandsIDs = new Dictionary<Type, int>"));
            typeToIdDictionary.Add(new LeftScopeSyntax(2));
            typeToIdDictionary.Add(dictionaryBody);
            typeToIdDictionary.Add(new RightScopeSyntax(2, true));

            for (int i = 0; i < commands.Count; i++)
            {
                var t = commands[i];
                dictionaryBody.Add(GetCommandMethod(t));

                //if (i < commands.Count - 1)
                //    dictionaryBody.Add(new ParagraphSyntax());
            }

            return tree.ToString();
        }


        /// <summary>
        /// here we codogen all around shortIDs
        /// </summary>
        /// <returns></returns>
        public ISyntax GetShortIdPart()
        {
            var tree = new TreeSyntaxNode();
            HashSet<ShortIDObject> shortIDs = new HashSet<ShortIDObject>(512);
            ushort count = 1;

            //gather network components
            foreach (var c in Program.componentOverData.Values)
            {
                if (c.IsAbstract)
                    continue;

                foreach (var i in c.Interfaces)
                {
                    if (i.Name == INetworkComponent)
                    {
                        shortIDs.Add(new ShortIDObject
                        {
                            Type = c.Name,
                            TypeCode = IndexGenerator.GenerateIndex(c.Name),
                            DataType = 2,
                        });
                    }
                }
            }

            foreach (var c in Program.networkCommands)
            {
                var shortIDdata = new ShortIDObject();

                shortIDdata.Type = c.Identifier.ValueText;
                shortIDdata.TypeCode = IndexGenerator.GenerateIndex(c.Identifier.ValueText);

                if (c.BaseList.ChildNodes().Any(x => x.ToString().Contains("INetworkCommand")))
                {
                    shortIDdata.DataType = 0;
                }
                else
                {
                    shortIDdata.DataType = 1;
                }

                shortIDs.Add(shortIDdata);
            }

            shortIDs = shortIDs.OrderBy(x => x.Type).ToHashSet();

            foreach (var i in shortIDs)
            {
                i.ShortId = count;
                count++;
            }

            tree.Add(GetDictionaryHelper.GetDictionaryMethod("GetTypeToShort", nameof(Type), "ushort", 2, out var typeToshortBody));
            tree.Add(GetDictionaryHelper.GetDictionaryMethod("GetShortToTypeCode", "ushort", "int", 2, out var shortToTypeCodeBody));
            tree.Add(GetDictionaryHelper.GetDictionaryMethod("GetShortToDataType", "ushort", "byte", 2, out var getShortToDataType));
            tree.Add(GetDictionaryHelper.GetDictionaryMethod("GetTypeCodeToShort", "int", "ushort", 2, out var typeCodeToShort));
            tree.Add(GetDictionaryHelper.GetDictionaryMethod("GetComponentProviders", "int", "ComponentSerializeProvider", 2, out var componentProviders));

            foreach (var i in shortIDs)
            {
                typeToshortBody.Tree.Add(GetDictionaryHelper.DictionaryBodyRecord(4, $"typeof({i.Type})", i.ShortId.ToString()));
                shortToTypeCodeBody.Tree.Add(GetDictionaryHelper.DictionaryBodyRecord(4, i.ShortId.ToString(), i.TypeCode.ToString()));
                getShortToDataType.Tree.Add(GetDictionaryHelper.DictionaryBodyRecord(4, i.ShortId.ToString(), i.DataType.ToString()));
                typeCodeToShort.Tree.Add(GetDictionaryHelper.DictionaryBodyRecord(4, i.TypeCode.ToString(), i.ShortId.ToString()));
            }

            foreach (var c in Program.componentOverData.Values)
            {
                if (c.IsAbstract)
                    continue;

                if (c.Interfaces.Any(x => x.Name == INetworkComponent))
                {
                    componentProviders.Tree.Add(GetDictionaryHelper.DictionaryBodyRecord(4,
                        IndexGenerator.GenerateIndex(c.Name).ToString(), $"new ComponentResolver<{c.Name},{c.Name}{Resolver}, {c.Name}{Resolver}>()"));
                }
            }


            tree.Add(InitShortIDPart());

            return tree;
        }

        private ISyntax InitShortIDPart()
        {
            var tree = new TreeSyntaxNode();

            tree.Add(new TabSimpleSyntax(2, "private void InitShortIds()"));
            tree.Add(new LeftScopeSyntax(2));
            tree.Add(new TabSimpleSyntax(3, "typeToShort = GetTypeToShort();"));
            tree.Add(new TabSimpleSyntax(3, "shortToTypeCode = GetShortToTypeCode();"));
            tree.Add(new TabSimpleSyntax(3, "shortToDataType = GetShortToDataType();"));
            tree.Add(new TabSimpleSyntax(3, "typeCodeToShort = GetTypeCodeToShort();"));
            tree.Add(new TabSimpleSyntax(3, "componentProviders = GetComponentProviders();"));
            tree.Add(new RightScopeSyntax(2));

            return tree;
        }

        private ISyntax GetCommandMethod(StructDeclarationSyntax command)
        {
            var tree = new TreeSyntaxNode();
            tree.Add(new TabSimpleSyntax(3, $"{{typeof({command.Identifier.ValueText}), {IndexGenerator.GetIndexForType(command.Identifier.ValueText)}}},"));
            return tree;
        }

        private ISyntax InitPartialCommandResolvers()
        {
            var tree = new TreeSyntaxNode();
            tree.Add(new TabSimpleSyntax(2, "partial void InitPartialCommandResolvers()"));
            tree.Add(new LeftScopeSyntax(2));
            tree.Add(new TabSimpleSyntax(3, "hashTypeToResolver = Map;"));
            tree.Add(new TabSimpleSyntax(3, "typeTohash = CommandsIDs;"));

            ///this part of short ids, u should check GetShortIdPart()
            tree.Add(new TabSimpleSyntax(3, "InitShortIds();"));
            tree.Add(new RightScopeSyntax(2));
            tree.Add(new ParagraphSyntax());

            return tree;
        }

        private ISyntax GetCommandResolver(StructDeclarationSyntax type)
        {
            return new TabSimpleSyntax(3, $"{{{IndexGenerator.GetIndexForType(type.Identifier.ValueText)}, new CommandResolver<{type.Identifier.ValueText}>()}},");
        }

        #endregion
    }
}
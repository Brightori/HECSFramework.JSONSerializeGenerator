using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HECSFramework.Core.Generator;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using RoslynHECS.DataTypes;
using RoslynHECS.Helpers;
using ClassDeclarationSyntax = Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax;
using SyntaxNode = Microsoft.CodeAnalysis.SyntaxNode;

namespace RoslynHECS
{
    class Program
    {
        public static List<string> components = new List<string>(2048);

        //resolvers collection
        public static Dictionary<string, ResolverData> customHecsResolvers = new Dictionary<string, ResolverData>(512);
        public static Dictionary<string, LinkedNode> LinkedNodes = new Dictionary<string, LinkedNode>(512);
        public static Dictionary<string, ClassDeclarationSyntax> nameToClassDeclaration = new Dictionary<string, ClassDeclarationSyntax>(512);

        public static List<ClassDeclarationSyntax> classes;
        public static List<StructDeclarationSyntax> structs;

        public static string ScriptsPath = @"D:\Develop\ZombieWar\Assets\";
        public static string HECSGenerated = @"D:\Develop\ZombieWar\Assets\Scripts\HECSGenerated\";
        //public static string ScriptsPath = @"E:\repos\Kefir\minilife-server\MinilifeServer\";
        //public static string HECSGenerated = @"E:\repos\Kefir\minilife-server\MinilifeServer\HECSGenerated\";

        public const string JSONHECSSerialize = "JSONHECSSerialize";
        public const string JSONHECSFieldByResolver = "JSONHECSFieldByResolver";
        public const string IAfterSerializationComponent = "IAfterSerializationComponent";
        public const string IBeforeSerializationComponent = "IBeforeSerializationComponent";
        public const string JsonFolder = "/JSONResolvers/";

        private static List<FileInfo> files;

        private static FileInfo alrdyHaveCommandMap;
        public static CSharpCompilation Compilation;

        static void Main(string[] args)
        {
            CheckArgs(args);

            Console.WriteLine($"Путь: {ScriptsPath}");
            Console.WriteLine($"Путь кодогена: {HECSGenerated}");
            Console.WriteLine($"Найдены аргументы запуска: {string.Join(", ", args)}");
            Console.WriteLine($"Доступные аргументы: {Environment.NewLine}{string.Join(Environment.NewLine, new[] { "path:путь_до_скриптов", "no_blueprints", "no_resolvers", "no_commands", "server" })}");

            var test = Directory.GetDirectories(ScriptsPath);

            //var files = new DirectoryInfo(ScriptsPath).GetFiles("*.cs", SearchOption.AllDirectories);
            files = new DirectoryInfo(ScriptsPath).GetFiles("*.cs", SearchOption.AllDirectories).Where(x => !x.FullName.Contains("\\Plugins") && !x.FullName.Contains("\\HECSGenerated") && !x.FullName.Contains("\\MessagePack")).ToList();
            Console.WriteLine(files.Count);

            var list = new List<SyntaxTree>(2048);

            foreach (var f in files)
            {
                if (f.Extension == ".cs")
                {
                    var s = File.ReadAllText(f.FullName);
                    var syntaxTree = CSharpSyntaxTree.ParseText(s);
                    list.Add(syntaxTree);
                }
            }

            Compilation = CSharpCompilation.Create("HelloWorld").AddSyntaxTrees(list);


            var classVisitor = new ClassVirtualizationVisitor();
            var structVisitor = new StructVirtualizationVisitor();
            var interfaceVisitor = new InterfaceVirtualizationVisitor();

            foreach (var syntaxTree in list)
            {
                classVisitor.Visit(syntaxTree.GetRoot());
                structVisitor.Visit(syntaxTree.GetRoot());
                interfaceVisitor.Visit(syntaxTree.GetRoot());
            }

            classes = classVisitor.Classes;
            structs = structVisitor.Structs;

            ProcessClasses();

            foreach (var s in structs)
                ProcessStructs(s);

            SaveFiles();
            Console.WriteLine("успешно сохранено");
            //Thread.Sleep(1500);
        }

        private static void CheckArgs(string[] args)
        {
            if (args == null || args.Length == 0)
                return;

            var path = args.SingleOrDefault(a => a.Contains("path:"))?.Replace("path:", "").TrimStart('-');
            var server = args.Any(a => a.Contains("server"));
            if (path != null)
            {
                ScriptsPath = path;
                ScriptsPath = Path.GetFullPath(ScriptsPath);
                if (!ScriptsPath.EndsWith(Path.DirectorySeparatorChar.ToString())) ScriptsPath += Path.DirectorySeparatorChar;

                HECSGenerated = server ? Path.Combine(ScriptsPath, "HECSGenerated") : Path.Combine(ScriptsPath, "Scripts", "HECSGenerated");
                HECSGenerated = Path.GetFullPath(HECSGenerated);
                if (!HECSGenerated.EndsWith(Path.DirectorySeparatorChar.ToString())) HECSGenerated += Path.DirectorySeparatorChar;
            }
        }

        private static void SaveFiles()
        {
            var processGeneration = new CodeGenerator();
            var list = processGeneration.JSONResolvers();

            foreach (var resolver in list)
            {
                SaveToFile(resolver.name + "Resolver" + ".cs", resolver.data, HECSGenerated + Program.JsonFolder);
            }

            SaveToFile("JSONResolverMap.cs", processGeneration.GetJSONProvidersMaps().ToString(), HECSGenerated);

            //SaveToFile(TypeProvider, processGeneration.GenerateTypesMapRoslyn(), HECSGenerated);
            //SaveToFile(MaskProvider, processGeneration.GenerateMaskProviderRoslyn(), HECSGenerated);
            //SaveToFile(SystemBindings, processGeneration.GetSystemBindsByRoslyn(), HECSGenerated);
            //SaveToFile(ComponentContext, processGeneration.GetComponentContextRoslyn(), HECSGenerated);
            //SaveToFile(HecsMasks, processGeneration.GenerateHecsMasksRoslyn(), HECSGenerated);
            //SaveToFile(Documentation, processGeneration.GetDocumentationRoslyn(), HECSGenerated); не получается нормально автоматизировать, слишком сложные параметры у атрибута

            //SaveToFile("ComponentsWorldPart.cs", processGeneration.GetEntitiesWorldPart(), HECSGenerated);
        }

        private static void CleanDirectory(string path)
        {
            DirectoryInfo directoryInfo = new DirectoryInfo(path);

            if (!directoryInfo.Exists)
                return;

            foreach (FileInfo file in directoryInfo.GetFiles())
            {
                file.Delete();
            }
            foreach (DirectoryInfo dir in directoryInfo.GetDirectories())
            {
                dir.Delete(true);
            }
        }

        private static void SaveToFile(string name, string data, string pathToDirectory, bool needToImport = false)
        {
            var path = pathToDirectory + name;

            try
            {
                if (!Directory.Exists(pathToDirectory))
                    Directory.CreateDirectory(pathToDirectory);

                File.WriteAllText(path, data);
            }
            catch
            {
                Console.WriteLine("we cant save file to " + pathToDirectory);
            }
        }

        private static void SaveToFileToFullPath(string data, string fullPath)
        {
            try
            {
                File.WriteAllText(fullPath, data);
            }
            catch
            {
                Console.WriteLine("we cant save file to " + fullPath);
            }
        }


        private static void ProcessStructs(StructDeclarationSyntax s)
        {
            //we add here custom resolvers what alrdy on project
            if (s.AttributeLists.Count > 0)
            {
                foreach (var a in s.AttributeLists)
                {
                    foreach (var attr in a.Attributes)
                    {
                        if (attr.Name.ToString() == JSONHECSSerialize)
                        {
                            var arguments = attr.ArgumentList.Arguments;

                            foreach (var arg in arguments)
                            {
                                if (arg.Expression is TypeOfExpressionSyntax needed)
                                {
                                    if (needed.Type is IdentifierNameSyntax identifierNameSyntax)
                                    {
                                        var needeType = identifierNameSyntax.Identifier.ValueText;
                                        customHecsResolvers.Add(needeType, new ResolverData { TypeToResolve = needeType, ResolverName = s.Identifier.ValueText });
                                    }
                                    else if (needed.Type is PredefinedTypeSyntax predefinedTypeSyntax)
                                    {
                                        var needeType = predefinedTypeSyntax.ToString();
                                        customHecsResolvers.Add(needeType, new ResolverData { TypeToResolve = needeType, ResolverName = s.Identifier.ValueText });
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        public static IEnumerable<INamedTypeSymbol> GetTypesByMetadataName(Compilation compilation, string typeMetadataName)
        {
            return compilation.References
                .Select(compilation.GetAssemblyOrModuleSymbol)
                .OfType<IAssemblySymbol>()
                .Select(assemblySymbol => assemblySymbol.GetTypeByMetadataName(typeMetadataName))
                .Where(t => t != null);
        }

        private static void ProcessClasses()
        {
            //we gather here classes 
            foreach (var c in classes)
            {
                if (LinkedNodes.ContainsKey(c.Identifier.ValueText))
                    continue;

                if (c.AttributeLists.Count > 0)
                {
                    foreach (var a in c.AttributeLists)
                    {
                        foreach (var attr in a.Attributes)
                        {
                            if (attr.ToString() == JSONHECSSerialize)
                            {
                                var linkedNode = new LinkedNode(c);
                                LinkedNodes.TryAdd(linkedNode.Name, linkedNode);
                            }
                        }
                    }
                }
            }
        }

        class ClassVirtualizationVisitor : CSharpSyntaxRewriter
        {
            public ClassVirtualizationVisitor()
            {
                Classes = new List<ClassDeclarationSyntax>(2048);
            }

            public List<ClassDeclarationSyntax> Classes { get; set; }

            public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node)
            {
                base.VisitClassDeclaration(node);
                Classes.Add(node); // save your visited classes
                nameToClassDeclaration.TryAdd(node.Identifier.ValueText, node);
                return node;
            }
        }

        class StructVirtualizationVisitor : CSharpSyntaxRewriter
        {
            public StructVirtualizationVisitor()
            {
                Structs = new List<StructDeclarationSyntax>(2048);
            }

            public List<StructDeclarationSyntax> Structs { get; set; }

            public override SyntaxNode VisitStructDeclaration(StructDeclarationSyntax node)
            {
                node = (StructDeclarationSyntax)base.VisitStructDeclaration(node);
                Structs.Add(node); // save your visited classes
                return node;
            }
        }

        class InterfaceVirtualizationVisitor : CSharpSyntaxRewriter
        {
            public List<InterfaceDeclarationSyntax> Interfaces { get; set; }

            public InterfaceVirtualizationVisitor()
            {
                Interfaces = new List<InterfaceDeclarationSyntax>(2048);
            }

            public override SyntaxNode Visit(SyntaxNode node)
            {
                if (node is InterfaceDeclarationSyntax inter)
                    VisitInterfaceDeclaration(inter);

                return base.Visit(node);
            }

            public override SyntaxNode VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
            {
                Interfaces.Add(node);
                return base.VisitInterfaceDeclaration(node);
            }
        }

        private static VisualStudioInstance SelectVisualStudioInstance(VisualStudioInstance[] visualStudioInstances)
        {
            Console.WriteLine("Multiple installs of MSBuild detected please select one:");
            for (int i = 0; i < visualStudioInstances.Length; i++)
            {
                Console.WriteLine($"Instance {i + 1}");
                Console.WriteLine($"    Name: {visualStudioInstances[i].Name}");
                Console.WriteLine($"    Version: {visualStudioInstances[i].Version}");
                Console.WriteLine($"    MSBuild Path: {visualStudioInstances[i].MSBuildPath}");
            }

            while (true)
            {
                var userResponse = Console.ReadLine();
                if (int.TryParse(userResponse, out int instanceNumber) &&
                    instanceNumber > 0 &&
                    instanceNumber <= visualStudioInstances.Length)
                {
                    return visualStudioInstances[instanceNumber - 1];
                }
                Console.WriteLine("Input not accepted, try again.");
            }
        }

        private class ConsoleProgressReporter : IProgress<ProjectLoadProgress>
        {
            public void Report(ProjectLoadProgress loadProgress)
            {
                var projectDisplay = Path.GetFileName(loadProgress.FilePath);
                if (loadProgress.TargetFramework != null)
                {
                    projectDisplay += $" ({loadProgress.TargetFramework})";
                }

                Console.WriteLine($"{loadProgress.Operation,-15} {loadProgress.ElapsedTime,-15:m\\:ss\\.fffffff} {projectDisplay}");
            }
        }
    }

    public class LinkedNode
    {
        public LinkedNode Parent;
        public TypeDeclarationSyntax Type;
        public HashSet<TypeDeclarationSyntax> Parts = new HashSet<TypeDeclarationSyntax>(3);

        public string Name;
        public bool IsStruct;
        public bool IsAbstract;
        public bool IsPartial;

        public bool IsAfterSerialization;
        public bool IsBeforeSerialization;

        //we hold here info from all parts
        public HashSet<MemberInfoWithAttributes> FieldsWithAttibutes = new HashSet<MemberInfoWithAttributes>(8);

        public LinkedNode(ClassDeclarationSyntax classDeclarationSyntax)
        {
            IsAbstract = classDeclarationSyntax.Modifiers.Any(x => x.IsKind(SyntaxKind.AbstractKeyword));
            IsPartial = classDeclarationSyntax.Modifiers.Any(x => x.IsKind(SyntaxKind.PartialKeyword));
            Type = classDeclarationSyntax;
            Name = classDeclarationSyntax.Identifier.ValueText;

            Parts.Add(classDeclarationSyntax);

            if (IsPartial)
            {
                foreach (var gatheredClass in Program.classes)
                {
                    if (gatheredClass.Identifier.ValueText == classDeclarationSyntax.Identifier.ValueText)
                    {
                        Parts.Add(gatheredClass);
                    }
                }
            }

            if (SyntaxHelper.TryGetParent(classDeclarationSyntax, out var parent, out var parentClass))
            {
                if (Program.LinkedNodes.TryGetValue(parent, out var linkedNode))
                {
                    Parent = linkedNode;
                }
                else
                {
                    Parent = new LinkedNode(parentClass);
                }
            }

            foreach (var p in Parts)
            {
                foreach (var m in p.Members)
                {
                    if (m.AttributeLists.Count > 0)
                    {
                        var info = new MemberInfoWithAttributes(m);
                        FieldsWithAttibutes.Add(info);
                    }
                }
            }
        }

        private void SetSerializationBools()
        {
            if (Parent != null)
            {
                if (Parent.IsAfterSerialization)
                    IsAfterSerialization = true;

                if (Parent.IsBeforeSerialization)
                    IsBeforeSerialization = true;
            }

            foreach (var p in Parts)
            {
                if (p.BaseList != null)
                {
                    foreach (var bn in p.BaseList.Types)
                    {
                        if (bn.Type.ToString() == Program.IAfterSerializationComponent)
                        {
                            IsAfterSerialization = true;
                        }

                        if (bn.Type.ToString() == Program.IBeforeSerializationComponent)
                        {
                            IsBeforeSerialization = true;
                        }
                    }
                }
            }
        }

        public LinkedNode(StructDeclarationSyntax structDeclarationSyntax)
        {
            IsStruct = true;
            IsPartial = structDeclarationSyntax.IsKind(SyntaxKind.PartialKeyword);
        }

        /// <summary>
        /// all member with fields on parents and parts 
        /// </summary>
        /// <returns></returns>
        public IEnumerable<MemberInfoWithAttributes> Members()
        {
            foreach (var m in FieldsWithAttibutes)
                yield return m;

            if (Parent != null)
            {
                foreach (var m in Parent.Members())
                    yield return m;
            }
        }
    }

    public class MemberInfoWithAttributes
    {
        public HashSet<AttributeSyntax> Attributes = new HashSet<AttributeSyntax>(2);
        public MemberDeclarationSyntax MemberDeclarationSyntax;

        public bool IsPrivate { get; private set; }
        public bool IsHECSSerializeField { get; private set; }

        public MemberInfoWithAttributes(MemberDeclarationSyntax memberDeclarationSyntax)
        {

            foreach (var attr in memberDeclarationSyntax.AttributeLists)
            {
                foreach (var a in attr.Attributes)
                {
                    Attributes.Add(a);
                }
            }

            IsPrivate = memberDeclarationSyntax.Modifiers.Any(x => x.IsKind(SyntaxKind.ProtectedKeyword) || x.IsKind(SyntaxKind.PrivateKeyword));
            IsHECSSerializeField = Attributes.Any(x => x.Name.ToString() == "Field");
            MemberDeclarationSyntax = memberDeclarationSyntax;
        }
    }
}
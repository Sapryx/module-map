using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ModuleMap.Repository;

namespace ModuleMap.Analysis;

public sealed class ReferenceCollector
{
    private static readonly HashSet<string> NativeImportAttributes = ["DllImportAttribute", "LibraryImportAttribute"];
    private readonly GitRepository repository;

    public ReferenceCollector(GitRepository repository)
    {
        this.repository = repository;
    }

    public async Task CollectAsync(List<Project> projects, SourceIndex index)
    {
        foreach(var project in projects)
        {
            var compilation = await project.GetCompilationAsync();
            if(compilation == null)
            {
                continue;
            }

            foreach(var document in project.Documents)
            {
                var source = document.FilePath == null
                    ? null
                    : index.FindByFile(repository.ToRelative(document.FilePath));
                var tree = await document.GetSyntaxTreeAsync();
                if(source == null || tree == null)
                {
                    continue;
                }

                var model = compilation.GetSemanticModel(tree);
                foreach(var node in (await tree.GetRootAsync()).DescendantNodes())
                {
                    Visit(node, model, compilation, source, index);
                }
            }
        }

        LinkMarkup(index);
    }

    private static INamedTypeSymbol? TypeOf(ISymbol? symbol)
    {
        var type = symbol switch
        {
            INamedTypeSymbol named => named,
            IAliasSymbol alias => alias.Target as INamedTypeSymbol,
            _ => symbol?.ContainingType
        };
        while(type?.ContainingType != null)
        {
            type = type.ContainingType;
        }

        return type?.OriginalDefinition;
    }

    private static void AddNativeLibraries(IMethodSymbol? method, SourceUnit source)
    {
        if(method == null)
        {
            return;
        }

        foreach(var attribute in method.GetAttributes())
        {
            bool isImport = attribute.AttributeClass != null
                && NativeImportAttributes.Contains(attribute.AttributeClass.Name);
            if(isImport && attribute.ConstructorArguments.Length > 0
                && attribute.ConstructorArguments[0].Value is string library)
            {
                source.NativeLibraries.Add(library);
            }
        }
    }

    private static void LinkMarkup(SourceIndex index)
    {
        foreach(var unit in index.Units)
        {
            foreach(string name in unit.MarkupReferences)
            {
                var target = index.FindByType(name);
                if(target != null && target != unit)
                {
                    unit.LinkTo(target).TypeNames.Add(name.Substring(name.LastIndexOf('.') + 1));
                }
            }
        }
    }

    private void Visit(
        SyntaxNode node,
        SemanticModel model,
        Compilation compilation,
        SourceUnit source,
        SourceIndex index
    )
    {
        if(node is SimpleNameSyntax)
        {
            var info = model.GetSymbolInfo(node);
            AddReference(TypeOf(info.Symbol ?? info.CandidateSymbols.FirstOrDefault()), compilation, source, index);
        }
        else if(node is BaseTypeSyntax baseType)
        {
            var type = model.GetTypeInfo(baseType.Type).Type as INamedTypeSymbol;
            var target = type == null || type.TypeKind != TypeKind.Interface ? null : FindUnit(type, index);
            if(target != null && target != source)
            {
                source.LinkTo(target).Implements = true;
            }
        }
        else if(node is AssignmentExpressionSyntax assignment && IsEventSubscription(assignment))
        {
            var symbol = model.GetSymbolInfo(assignment.Left).Symbol as IEventSymbol;
            var target = symbol == null ? null : FindUnit(symbol.ContainingType, index);
            if(target != null && target != source)
            {
                source.LinkTo(target).Listens = true;
            }
        }
        else if(node is MethodDeclarationSyntax method)
        {
            AddNativeLibraries(model.GetDeclaredSymbol(method), source);
        }
    }

    private static bool IsEventSubscription(AssignmentExpressionSyntax assignment)
    {
        return assignment.IsKind(SyntaxKind.AddAssignmentExpression)
            || assignment.IsKind(SyntaxKind.SubtractAssignmentExpression);
    }

    private void AddReference(INamedTypeSymbol? type, Compilation compilation, SourceUnit source, SourceIndex index)
    {
        if(type == null || type.TypeKind == TypeKind.Error)
        {
            return;
        }

        var target = FindUnit(type, index);
        if(target != null)
        {
            if(target != source)
            {
                source.LinkTo(target).TypeNames.Add(type.Name);
            }

            return;
        }

        var reference = type.ContainingAssembly == null
            ? null
            : compilation.GetMetadataReference(type.ContainingAssembly) as PortableExecutableReference;
        if(reference?.FilePath != null)
        {
            source.ExternalAssemblies.Add(reference.FilePath);
        }
    }

    private SourceUnit? FindUnit(INamedTypeSymbol type, SourceIndex index)
    {
        var location = type.Locations.FirstOrDefault(it => it.IsInSource);
        var byFile = location?.SourceTree == null
            ? null
            : index.FindByFile(repository.ToRelative(location.SourceTree.FilePath));
        return byFile ?? index.FindByType(SourceIndex.FullName(type));
    }
}

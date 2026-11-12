using Microsoft.CodeAnalysis;

namespace ModuleMap.Map;

public sealed class ModuleClassifier
{
    private static readonly string[] UiAssemblyPrefixes =
    [
        "Avalonia",
        "PresentationFramework",
        "PresentationCore",
        "Microsoft.Maui",
        "Microsoft.WinUI",
        "System.Windows.Forms"
    ];

    public void Classify(List<ModuleDraft> drafts)
    {
        foreach(var draft in drafts)
        {
            draft.Kind = KindOf(draft);
        }

        foreach(var draft in drafts)
        {
            draft.Portability = PortabilityOf(draft);
        }
    }

    private static string KindOf(ModuleDraft draft)
    {
        var types = draft.Types;
        if(draft.IsRoot || draft.IsHidden)
        {
            return "infra";
        }

        if(draft.Group.ViewModel != null)
        {
            return "feature";
        }

        if(draft.Group.Units.Any(it => it.HasMarkup) || types.Any(IsUiType))
        {
            return "ui";
        }

        if(types.Count > 0 && types.All(IsModelType))
        {
            return "model";
        }

        if(types.Count > 0 && types.All(it => it.IsStatic))
        {
            return "utility";
        }

        return draft.Packages.Count > 0 || draft.Natives.Count > 0 ? "adapter" : "service";
    }

    private static int PortabilityOf(ModuleDraft draft)
    {
        if(draft.IsRoot)
        {
            return 1;
        }

        int dependencies = draft.Outgoing.Values
            .Where(it => it.IsDependency && it.To.Kind != "model")
            .Select(it => it.To)
            .Distinct()
            .Count();
        if(dependencies == 0)
        {
            return 3;
        }

        return dependencies <= 2 ? 2 : 1;
    }

    private static bool IsUiType(INamedTypeSymbol type)
    {
        for(var current = type.BaseType; current != null; current = current.BaseType)
        {
            string assembly = current.ContainingAssembly?.Name ?? "";
            if(UiAssemblyPrefixes.Any(it => assembly.StartsWith(it, StringComparison.Ordinal)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsModelType(INamedTypeSymbol type)
    {
        if(type.TypeKind == TypeKind.Enum)
        {
            return true;
        }

        if(type.TypeKind != TypeKind.Class && type.TypeKind != TypeKind.Struct || type.IsStatic)
        {
            return false;
        }

        return type.GetMembers().All(it => it is IFieldSymbol
            || it is IPropertySymbol
            || it is INamedTypeSymbol
            || it is IMethodSymbol { MethodKind: not MethodKind.Ordinary }
            || it.IsImplicitlyDeclared);
    }
}

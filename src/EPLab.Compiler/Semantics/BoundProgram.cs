using EPLab.Compiler.Syntax;

namespace EPLab.Compiler.Semantics;

public sealed record BoundProgram(
    EPLabProject Project,
    IReadOnlyList<CompilationUnitSyntax> Units,
    IReadOnlyList<string> NamespaceImports,
    IReadOnlyList<string> AssemblyImports,
    PluginSyntax? Plugin,
    IReadOnlyList<ConfigItemSyntax> ConfigItems,
    IReadOnlyList<EventBindingSyntax> EventBindings,
    IReadOnlyList<CommandBindingSyntax> CommandBindings,
    IReadOnlyList<DeclarationSyntax> Declarations,
    IReadOnlyDictionary<string, ClassDeclarationSyntax> Classes,
    IReadOnlyDictionary<string, TypeSyntax> KnownTypes);

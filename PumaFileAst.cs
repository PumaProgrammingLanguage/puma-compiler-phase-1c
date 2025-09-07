using System.Collections.Generic;

class PumaFileAst
{
    // Using section: list of using statements (namespaces, files, aliases)
    public List<string> Usings { get; } = new();

    // Type/Trait/Module section
    public string? TypeName { get; set; }
    public string? BaseType { get; set; }
    public string? TraitName { get; set; }
    public string? ModuleName { get; set; }
    public List<string> InheritedTraits { get; } = new();

    // Enums section
    public List<EnumAst> Enums { get; } = new();

    // Records section
    public List<RecordAst> Records { get; } = new();

    // Properties section
    public List<PropertyAst> Properties { get; } = new();

    // Initialize/Start section
    public InitializeAst? Initialize { get; set; }
    public StartAst? Start { get; set; }

    // Finalize section
    public FinalizeAst? Finalize { get; set; }

    // Functions section
    public List<FunctionAst> Functions { get; } = new();
    public List<DelegateAst> Delegates { get; } = new();
}

// Example AST classes for extensibility
class EnumAst
{
    public string Name { get; set; }
    public string UnderlyingType { get; set; }
    public List<EnumMemberAst> Members { get; } = new();
}

class EnumMemberAst
{
    public string Name { get; set; }
    public string Value { get; set; }
}

class RecordAst
{
    public string Name { get; set; }
    public List<RecordMemberAst> Members { get; } = new();
}

class RecordMemberAst
{
    public string Name { get; set; }
    public string Type { get; set; }
}

class PropertyAst
{
    public string Name { get; set; }
    public string Type { get; set; }
    public string Value { get; set; }
    public string AccessModifier { get; set; }
    public string MutabilityModifier { get; set; }
    public bool IsOptional { get; set; }
}

class InitializeAst
{
    public List<ParameterAst> Parameters { get; } = new();
    public List<string> Body { get; } = new();
}

class StartAst
{
    public List<ParameterAst> Parameters { get; } = new();
    public List<string> Body { get; } = new();
}

class FinalizeAst
{
    public List<string> Body { get; } = new();
}

class FunctionAst
{
    public string Name { get; set; }
    public string ReturnType { get; set; }
    public List<ParameterAst> Parameters { get; } = new();
    public List<string> Body { get; } = new();
    public string AccessModifier { get; set; }
}

class DelegateAst
{
    public string Name { get; set; }
    public List<ParameterAst> Parameters { get; } = new();
}

class ParameterAst
{
    public string Name { get; set; }
    public string Type { get; set; }
    public string DefaultValue { get; set; }
    public string MutabilityModifier { get; set; }
}
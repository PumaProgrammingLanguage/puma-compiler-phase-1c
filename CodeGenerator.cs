static class CodeGenerator
{
    // Step 2: Generate C++ code from AST
    static public string GenerateCpp(PumaFileAst ast)
    {
        var cpp = new List<string>();

        if (!string.IsNullOrEmpty(ast.TypeName))
        {
            cpp.Add($"class {ast.TypeName} : public {ast.BaseType} {{");
            cpp.Add("public:");
        }

        foreach (var e in ast.Enums)
        {
            cpp.Add($"    enum StatusSetting {{ {e} }};");
        }

        foreach (var p in ast.Properties)
        {
            cpp.Add($"    // property: {p}");
        }

        if (!string.IsNullOrEmpty(ast.TypeName))
        {
            cpp.Add("};");
        }

        return string.Join(Environment.NewLine, cpp);
    }
}

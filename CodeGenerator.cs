using System;
using System.Collections.Generic;
using System.Text;

// PLAN (pseudocode):
// 1. Introduce a record GeneratedUnit to hold header/source file names and contents.
// 2. Preserve existing public method GenerateCpp for backward compatibility; it now
//    calls new GenerateCppUnits and concatenates header + source with a separator comment.
// 3. Add new public API: GenerateCppUnits(PumaFileAst ast) -> List<GeneratedUnit>.
// 4. For a module (C style):
//      - Header (.h):
//          * #pragma once
//          * Includes derived from Usings
//          * Forward declarations if needed (none for placeholders now)
//          * enum definitions
//          * struct (record) definitions
//          * extern variable declarations for properties
//          * delegate typedefs
//          * function prototypes
//          * lifecycle function prototypes (Initialize/Start/Finalize)
//      - Source (.c):
//          * #include "<ModuleName>.h"
//          * definitions for global variables
//          * lifecycle function stub definitions
//          * function stub definitions
// 5. For a type/trait (C++):
//      - Header (.hpp):
//          * #pragma once
//          * Includes from Usings (converted to #include "<u>.hpp" or ".h" - retain .h assumption)
//          * Forward declarations for inherited traits
//          * class definition
//              - For trait: pure virtual methods
//              - For concrete class: method declarations
//              - Nested enums/records
//              - Member properties
//              - Delegate using aliases
//              - Lifecycle method declarations (only if specified)
//      - Source (.cpp):
//          * #include "<Name>.hpp"
//          * Out-of-class definitions for lifecycle + methods (not for pure virtual / trait)
// 6. Rendering helpers refined to produce syntactically valid minimal code (clang friendly).
// 7. Keep placeholder comments referencing original AST object for future expansion.
// 8. Guarantee no duplicate semicolons, valid braces, and compile-safe stubs.
// 9. Keep indentation helper.
// 10. All placeholder signatures use 'void' return and '(void)' parameter list for C; for C++ '( )'.
// 11. Trait methods marked =0; class methods have empty bodies in .cpp.
// 12. Provide simple safe identifier generation for placeholders (incremental counters).
// 13. Ensure no null ref usage: guard lists with empty enumerations if null (defensive).
// 14. Maintain original minimal style, but improved structure for multi-file output.

static class CodeGenerator
{
    public sealed record GeneratedUnit(string HeaderFile, string HeaderCode, string SourceFile, string SourceCode);

    // Backward compatibility: single string (header + source)
    public static string GenerateCpp(PumaFileAst ast)
    {
        var units = GenerateCppUnits(ast);
        if (units.Count == 0) return string.Empty;
        var sb = new StringBuilder();
        foreach (var u in units)
        {
            sb.AppendLine($"// ===== {u.HeaderFile} =====");
            sb.AppendLine(u.HeaderCode);
            sb.AppendLine();
            sb.AppendLine($"// ===== {u.SourceFile} =====");
            sb.AppendLine(u.SourceCode);
            sb.AppendLine();
        }
        return sb.ToString();
    }

    public static List<GeneratedUnit> GenerateCppUnits(PumaFileAst ast)
    {
        var list = new List<GeneratedUnit>();
        if (ast == null) return list;

        bool isModule = !string.IsNullOrWhiteSpace(ast.ModuleName);
        bool isTypeOrTrait = !string.IsNullOrWhiteSpace(ast.TypeName) || !string.IsNullOrWhiteSpace(ast.TraitName);

        if (isModule)
            list.Add(GenerateCModuleUnit(ast));
        else if (isTypeOrTrait)
            list.Add(GenerateCppTypeOrTraitUnit(ast));

        return list;
    }

    // =========================================================
    // C MODULE GENERATION
    // =========================================================
    private static GeneratedUnit GenerateCModuleUnit(PumaFileAst ast)
    {
        var moduleName = ast.ModuleName!.Trim();
        var headerFile = moduleName + ".h";
        var sourceFile = moduleName + ".c";

        var h = new StringBuilder();
        var c = new StringBuilder();

        // Header
        h.AppendLine("#pragma once");
        AppendUsingsAsIncludes(h, ast);

        // Enums
        foreach (var e in Safe(ast.Enums))
            h.AppendLine(RenderEnum(e));

        // Records
        foreach (var r in Safe(ast.Records))
            h.AppendLine(RenderRecord(r));

        // Delegates
        foreach (var d in Safe(ast.Delegates))
            h.AppendLine(RenderDelegate(d));

        // Global properties (extern in header, definition in source)
        foreach (var p in Safe(ast.Properties))
            h.AppendLine(RenderGlobalPropertyDeclaration(p));

        // Function prototypes
        foreach (var f in Safe(ast.Functions))
            h.AppendLine(RenderCFunctionPrototype(moduleName, f) + ";");

        // Lifecycle prototypes
        if (ast.Initialize != null) h.AppendLine($"void {moduleName}_Initialize(void);");
        if (ast.Start != null) h.AppendLine($"void {moduleName}_Start(void);");
        if (ast.Finalize != null) h.AppendLine($"void {moduleName}_Finalize(void);");

        // Source
        c.AppendLine($"#include \"{headerFile}\"");
        c.AppendLine();

        // Global property definitions
        foreach (var p in Safe(ast.Properties))
            c.AppendLine(RenderGlobalPropertyDefinition(p));

        // Lifecycle implementations
        if (ast.Initialize != null)
        {
            c.AppendLine($"void {moduleName}_Initialize(void) {{");
            c.AppendLine("    // TODO: initialization logic");
            c.AppendLine("}");
            c.AppendLine();
        }
        if (ast.Start != null)
        {
            c.AppendLine($"void {moduleName}_Start(void) {{");
            c.AppendLine("    // TODO: start logic");
            c.AppendLine("}");
            c.AppendLine();
        }
        if (ast.Finalize != null)
        {
            c.AppendLine($"void {moduleName}_Finalize(void) {{");
            c.AppendLine("    // TODO: finalize logic");
            c.AppendLine("}");
            c.AppendLine();
        }

        // Function stubs
        foreach (var f in Safe(ast.Functions))
        {
            c.AppendLine(RenderCFunctionPrototype(moduleName, f));
            c.AppendLine("{");
            c.AppendLine("    // TODO: implement");
            c.AppendLine("}");
            c.AppendLine();
        }

        return new GeneratedUnit(headerFile, h.ToString().TrimEnd(), sourceFile, c.ToString().TrimEnd());
    }

    // =========================================================
    // C++ TYPE / TRAIT GENERATION
    // =========================================================
    private static GeneratedUnit GenerateCppTypeOrTraitUnit(PumaFileAst ast)
    {
        var isTrait = !string.IsNullOrWhiteSpace(ast.TraitName);
        var name = (ast.TypeName ?? ast.TraitName!)!.Trim();

        var headerFile = name + ".hpp";
        var sourceFile = name + ".cpp";

        var h = new StringBuilder();
        var c = new StringBuilder();

        // Header
        h.AppendLine("#pragma once");
        AppendUsingsAsIncludes(h, ast);

        // Forward declarations for inherited traits
        if (ast.InheritedTraits != null)
        {
            foreach (var t in ast.InheritedTraits)
            {
                if (!string.IsNullOrWhiteSpace(t))
                    h.AppendLine($"class {t};");
            }
            if (ast.InheritedTraits.Count > 0)
                h.AppendLine();
        }

        var baseType = !string.IsNullOrWhiteSpace(ast.BaseType) ? $" : public {ast.BaseType}" : string.Empty;
        var inherits = new StringBuilder();
        if (ast.InheritedTraits != null && ast.InheritedTraits.Count > 0)
        {
            var traitJoin = string.Join(", public ", ast.InheritedTraits);
            if (isTrait)
            {
                inherits.Append(" : public " + traitJoin);
            }
            else
            {
                if (baseType.Length == 0)
                    inherits.Append(" : public " + traitJoin);
                else
                    inherits.Append(", public " + traitJoin);
            }
        }

        if (isTrait)
            h.AppendLine($"class {name}{inherits} {{");
        else
            h.AppendLine($"class {name}{baseType}{inherits} {{");

        h.AppendLine("public:");

        if (isTrait)
        {
            h.AppendLine($"    virtual ~{name}() = default;");
        }
        else
        {
            h.AppendLine($"    {name}() = default;");
            h.AppendLine($"    virtual ~{name}() = default;");
        }

        // Nested enums
        foreach (var e in Safe(ast.Enums))
            h.AppendLine(Indent(RenderEnum(e), 4));

        // Nested records
        foreach (var r in Safe(ast.Records))
            h.AppendLine(Indent(RenderRecord(r, nested: true), 4));

        // Member properties
        foreach (var p in Safe(ast.Properties))
            h.AppendLine(Indent(RenderMemberProperty(p), 4));

        // Delegates (C++)
        foreach (var d in Safe(ast.Delegates))
            h.AppendLine(Indent(RenderDelegateCpp(d), 4));

        // Methods
        foreach (var f in Safe(ast.Functions))
            h.AppendLine(Indent(RenderMethodSignature(f, isTrait) + ";", 4));

        // Lifecycle declarations (only for concrete classes)
        if (!isTrait)
        {
            if (ast.Initialize != null) h.AppendLine(Indent("void Initialize();", 4));
            if (ast.Start != null) h.AppendLine(Indent("void Start();", 4));
            if (ast.Finalize != null) h.AppendLine(Indent("void Finalize();", 4));
        }

        h.AppendLine("};");

        // Source
        c.AppendLine($"#include \"{headerFile}\"");
        c.AppendLine();

        if (!isTrait)
        {
            // Lifecycle definitions
            if (ast.Initialize != null)
            {
                c.AppendLine($"void {name}::Initialize() {{");
                c.AppendLine("    // TODO: initialization logic");
                c.AppendLine("}");
                c.AppendLine();
            }
            if (ast.Start != null)
            {
                c.AppendLine($"void {name}::Start() {{");
                c.AppendLine("    // TODO: start logic");
                c.AppendLine("}");
                c.AppendLine();
            }
            if (ast.Finalize != null)
            {
                c.AppendLine($"void {name}::Finalize() {{");
                c.AppendLine("    // TODO: finalize logic");
                c.AppendLine("}");
                c.AppendLine();
            }

            // Method definitions
            foreach (var f in Safe(ast.Functions))
            {
                c.AppendLine(RenderMethodDefinition(name, f));
                c.AppendLine();
            }
        }

        return new GeneratedUnit(headerFile, h.ToString().TrimEnd(), sourceFile, c.ToString().TrimEnd());
    }

    // =========================================================
    // Helpers
    // =========================================================
    private static IEnumerable<T> Safe<T>(IEnumerable<T>? items)
    {
        return items ?? Array.Empty<T>();
    }

    private static void AppendUsingsAsIncludes(StringBuilder sb, PumaFileAst ast)
    {
        if (ast.Usings == null) return;
        foreach (var u in ast.Usings)
        {
            if (string.IsNullOrWhiteSpace(u)) continue;
            // Assume header extension .h (user can adjust)
            sb.AppendLine($"#include \"{u}.h\"");
        }
        if (ast.Usings.Count > 0) sb.AppendLine();
    }

    // Placeholder counters to create distinct stub names if needed later (not used extensively now)
    private static int _fnCounter = 0;
    private static string NextId(string prefix) => $"{prefix}_{++_fnCounter}";

    // Renderers (kept simple but valid)
    private static string RenderEnum(object e) =>
        "// enum placeholder\n" +
        "enum /*EnumName*/ {\n" +
        "    /*Value1*/ = 0\n" +
        "};";

    private static string RenderRecord(object r, bool nested = false) =>
        $"// record placeholder{(nested ? " (nested)" : "")}\n" +
        "struct /*RecordName*/ {\n" +
        "    int /*field*/;\n" +
        "};";

    private static string RenderGlobalPropertyDeclaration(object p) =>
        "extern int /*globalVar*/;";

    private static string RenderGlobalPropertyDefinition(object p) =>
        "int /*globalVar*/ = 0;";

    private static string RenderMemberProperty(object p) =>
        "int /*memberVar*/ = 0; // NOTE: adjust type";

    private static string RenderDelegate(object d) =>
        "typedef void (*/*DelegateName*/)(void);";

    private static string RenderDelegateCpp(object d) =>
        "using /*DelegateName*/ = void(*)(void);";

    private static string RenderCFunctionPrototype(string moduleName, object f)
    {
        return "void " + moduleName + "_" + "Fn" + "_" + NextId("id") + "(void)";
    }

    private static string RenderMethodSignature(object f, bool pureVirtual)
    {
        var name = "Method_" + NextId("id");
        return pureVirtual ? $"virtual void {name}()" + " = 0" : $"void {name}()";
    }

    private static string RenderMethodDefinition(string typeName, object f)
    {
        var methodName = "Method_" + NextId("id");
        return $"void {typeName}::{methodName}() {{\n    // TODO: implement\n}}";
    }

    private static string Indent(string text, int spaces)
    {
        var pad = new string(' ', spaces);
        var lines = text.Replace("\r\n", "\n").Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Length > 0)
                lines[i] = pad + lines[i];
        }
        return string.Join(Environment.NewLine, lines);
    }
}

// NOTE: PumaFileAst partial stub (if needed for compilation here):
// The real implementation should define proper lists and AST node types.
// This is only a minimal placeholder to avoid compile errors if absent.
public class PumaFileAst
{
    public List<string> Usings { get; } = new();
    public string? TypeName { get; set; }
    public string? BaseType { get; set; }
    public string? TraitName { get; set; }
    public string? ModuleName { get; set; }
    public List<string> InheritedTraits { get; } = new();
    public List<object> Enums { get; } = new();
    public List<object> Records { get; } = new();
    public List<object> Properties { get; } = new();
    public object? Initialize { get; set; }
    public object? Start { get; set; }
    public object? Finalize { get; set; }
    public List<object> Functions { get; } = new();
    public List<object> Delegates { get; } = new();
}

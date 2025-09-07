using System.Text.RegularExpressions;
using System.Linq;

static class Parser
{
    // Step 1: Parse Puma code into a simple AST
    public static (PumaFileAst Ast, int ErrorCount) ParseCode(string fileName, string code)
    {
        /*
        PSEUDOCODE PLAN (added finalize section):
        1. Initialize AST, errorCount, sectionPositions list.
        2. Parse 'using' directives; record position.
        3. Parse exactly one of type/trait/module; collect inheritance traits if 'type' and optional 'has'.
        4. Parse 'enums' block -> simple names line by line.
        5. Parse 'records' block -> simple names.
        6. Parse 'properties' block -> raw names only (current implementation).
        7. Parse 'initialize' and 'start'; ensure not both; validate 'start' only with module.
        8. NEW: Parse 'finalize' block (no content captured yet, just presence) and record position.
        9. Parse 'functions' block -> extract function names naively.
        10. Validate order with updated expected order including 'finalize':
           using -> type/trait/module -> enums -> records -> properties -> initialize/start -> finalize -> functions
        11. Return (ast, errorCount).
        Notes:
        - 'finalize' is optional and independent; allowed with either initialize or start.
        - Delegates are NOT a separate section (per user) so no delegate section parsing added.
        */

        var ast = new PumaFileAst();
        int errorCount = 0;

        var sectionPositions = new List<(string Name, int Index)>();

        // Parse usings
        var usingMatches = Regex.Matches(code, @"using\s+([\w\.]+)");
        if (usingMatches.Count > 0)
        {
            int firstUsingIndex = usingMatches.Cast<Match>().Min(m => m.Index);
            sectionPositions.Add(("using", firstUsingIndex));
            foreach (Match match in usingMatches)
                ast.Usings.Add(match.Groups[1].Value);
        }

        // Parse type, trait, module
        var typeMatch = Regex.Match(code, @"type\s+(\w+)\s+is\s+(\w+)", RegexOptions.Singleline);
        var traitMatch = Regex.Match(code, @"trait\s+(\w+)", RegexOptions.Singleline);
        var moduleMatch = Regex.Match(code, @"module\s+(\w+)", RegexOptions.Singleline);

        int foundCount = (typeMatch.Success ? 1 : 0) + (traitMatch.Success ? 1 : 0) + (moduleMatch.Success ? 1 : 0);
        if (foundCount == 2)
        {
            Console.WriteLine($"[{fileName}] Error: There should be only one of: type, trait or module.");
            {
                string firstSection = typeMatch.Success ? "type" : "trait";
                string secondSection = traitMatch.Success ? "trait" : "module";
                Console.WriteLine($"[{fileName}]        Found a {firstSection} and a {secondSection} section.");
            }
            errorCount++;
        }
        else if (foundCount == 3)
        {
            Console.WriteLine($"[{fileName}]        Found a type, a trait and a module section in the same file.");
            errorCount++;
        }

        if (typeMatch.Success)
        {
            ast.TypeName = typeMatch.Groups[1].Value;
            ast.BaseType = typeMatch.Groups[2].Value;
            sectionPositions.Add(("type/trait/module", typeMatch.Index));

            var inheritsMatch = Regex.Match(code, @"has\s+([\w,\s]+)");
            if (inheritsMatch.Success)
            {
                var traits = inheritsMatch.Groups[1].Value.Split(',');
                foreach (var trait in traits)
                {
                    var trimmed = trait.Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                        ast.InheritedTraits.Add(trimmed);
                }
            }
        }
        else if (traitMatch.Success)
        {
            ast.TraitName = traitMatch.Groups[1].Value;
            sectionPositions.Add(("type/trait/module", traitMatch.Index));
        }
        else if (moduleMatch.Success)
        {
            ast.ModuleName = moduleMatch.Groups[1].Value;
            sectionPositions.Add(("type/trait/module", moduleMatch.Index));
        }

        // Enums
        var enumsMatch = Regex.Match(code, @"enums\b(.*?)(?:\n\s*\n|$)", RegexOptions.Singleline);
        if (enumsMatch.Success)
        {
            sectionPositions.Add(("enums", enumsMatch.Index));
            var enumBlock = enumsMatch.Groups[1].Value;
            var enumLines = enumBlock.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in enumLines)
            {
                var trimmed = line.Trim();
                if (!string.IsNullOrEmpty(trimmed) && !trimmed.Contains(","))
                    ast.Enums.Add(new EnumAst { Name = trimmed });
            }
        }

        // Records
        var recordsMatch = Regex.Match(code, @"records\b(.*?)(?:\n\s*\n|$)", RegexOptions.Singleline);
        if (recordsMatch.Success)
        {
            sectionPositions.Add(("records", recordsMatch.Index));
            var recordBlock = recordsMatch.Groups[1].Value;
            var recordLines = recordBlock.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in recordLines)
            {
                var trimmed = line.Trim();
                if (!string.IsNullOrEmpty(trimmed) && !trimmed.Contains(","))
                    ast.Records.Add(new RecordAst { Name = trimmed });
            }
        }

        // Properties
        var propMatch = Regex.Match(code, @"properties\b(.*?)(?:\n\s*\n|$)", RegexOptions.Singleline);
        if (propMatch.Success)
        {
            sectionPositions.Add(("properties", propMatch.Index));
            var propBlock = propMatch.Groups[1].Value;
            var propLines = propBlock.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in propLines)
            {
                var trimmed = line.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                    ast.Properties.Add(new PropertyAst { Name = trimmed });
            }
        }

        // Initialize or Start
        var initializeMatch = Regex.Match(code, @"initialize\b(.*?)(?:\n\s*\n|$)", RegexOptions.Singleline);
        var startMatch = Regex.Match(code, @"start\b(.*?)(?:\n\s*\n|$)", RegexOptions.Singleline);

        if (initializeMatch.Success && startMatch.Success)
        {
            Console.WriteLine($"[{fileName}] Error: Both 'initialize' and 'start' sections found. Only one allowed.");
            errorCount++;
        }

        if (initializeMatch.Success)
        {
            sectionPositions.Add(("initialize/start", initializeMatch.Index));
            ast.Initialize = new InitializeAst { };
        }

        if (startMatch.Success)
        {
            if (!moduleMatch.Success)
            {
                Console.WriteLine($"[{fileName}] Error: 'start' section is only valid in a module file.");
                errorCount++;
            }
            sectionPositions.Add(("initialize/start", startMatch.Index));
            ast.Start = new StartAst { };
        }

        // Finalize (new section)
        var finalizeMatch = Regex.Match(code, @"finalize\b(.*?)(?:\n\s*\n|$)", RegexOptions.Singleline);
        if (finalizeMatch.Success)
        {
            sectionPositions.Add(("finalize", finalizeMatch.Index));
            ast.Finalize = new FinalizeAst { };
        }

        // Functions
        var functionsMatch = Regex.Match(code, @"functions\b(.*?)(?:\n\s*\n|$)", RegexOptions.Singleline);
        if (functionsMatch.Success)
        {
            sectionPositions.Add(("functions", functionsMatch.Index));
            var funcBlock = functionsMatch.Groups[1].Value;

            var funcLines = funcBlock
                .Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrWhiteSpace(l));

            foreach (var line in funcLines)
            {
                if (line.StartsWith("#") || line == "{" || line == "}") continue;

                var nameMatch = Regex.Match(line, @"^([A-Za-z_]\w*)\s*\(");
                string name = nameMatch.Success ? nameMatch.Groups[1].Value : line.Split(' ', '(', ':').FirstOrDefault() ?? line;

                ast.Functions.Add(new FunctionAst
                {
                    Name = name
                });
            }
        }

        // ---- Section order validation ----
        // Updated expected order to include 'finalize' after initialize/start and before functions.
        string[] expectedOrder = {
            "using",
            "type/trait/module",
            "enums",
            "records",
            "properties",
            "initialize/start",
            "finalize",
            "functions"
        };

        var rank = expectedOrder
            .Select((name, idx) => (name, idx))
            .ToDictionary(t => t.name, t => t.idx);

        var orderedFound = sectionPositions
            .Where(p => rank.ContainsKey(p.Name))
            .OrderBy(p => p.Index)
            .ToList();

        for (int i = 1; i < orderedFound.Count; i++)
        {
            var prev = orderedFound[i - 1];
            var current = orderedFound[i];
            if (rank[current.Name] < rank[prev.Name])
            {
                Console.WriteLine($"[{fileName}] Error: Section '{current.Name}' appears before '{prev.Name}' but should come after. Expected order: {string.Join(" -> ", expectedOrder)}.");
                errorCount++;
            }
        }

        return (ast, errorCount);
    }
}

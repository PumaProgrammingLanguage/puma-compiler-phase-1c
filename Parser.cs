using System.Text.RegularExpressions;

static class Parser
{
    // Step 1: Parse Puma code into a simple AST
    static public PumaFileAst ParseCode(string code)
    {
        var ast = new PumaFileAst();

        var typeMatch = Regex.Match(code, @"type\s+(\w+)\s+is\s+(\w+)");
        if (typeMatch.Success)
        {
            ast.TypeName = typeMatch.Groups[1].Value;
            ast.BaseType = typeMatch.Groups[2].Value;
        }

        var enumsMatch = Regex.Match(code, @"enums\s*(.*?)end", RegexOptions.Singleline);
        if (enumsMatch.Success)
        {
            var enumBlock = enumsMatch.Groups[1].Value;
            var enumLines = enumBlock.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in enumLines)
            {
                var trimmed = line.Trim();
                if (!string.IsNullOrEmpty(trimmed) && !trimmed.Contains(","))
                    ast.Enums.Add(trimmed);
            }
        }

        var propMatch = Regex.Match(code, @"properties\s*(.*?)\n", RegexOptions.Singleline);
        if (propMatch.Success)
        {
            var propBlock = propMatch.Groups[1].Value;
            var propLines = propBlock.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in propLines)
            {
                var trimmed = line.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                    ast.Properties.Add(trimmed);
            }
        }

        return ast;
    }
}

using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using static Parser;
using static CodeGenerator;


class PumaCompiler
{
    public string SourceFolder { get; }
    public string BuildFolder { get; }

    public PumaCompiler(string sourceFolder, string buildFolder)
    {
        SourceFolder = sourceFolder;
        BuildFolder = buildFolder;
        Directory.CreateDirectory(BuildFolder);
    }

    public void Compile()
    {
        foreach (var file in Directory.GetFiles(SourceFolder, "*.puma"))
        {
            string pumaCode = File.ReadAllText(file);

            // Prefer full path if the parser uses it for diagnostics; otherwise use Path.GetFileName(file)
            var parseResult = Parser.ParseCode(file, pumaCode);

            // If parser reports an error code > 0, skip the next three lines (code generation & file write)
            if (parseResult.ErrorCount > 0)
                continue;

            string cppCode = CodeGenerator.GenerateCpp(parseResult.Ast);
            string outFile = Path.Combine(BuildFolder, Path.GetFileNameWithoutExtension(file) + ".cpp");
            File.WriteAllText(outFile, cppCode);
        }
    }
}
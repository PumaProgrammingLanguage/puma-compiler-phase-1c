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
            var pumaCode = File.ReadAllText(file);
            var ast = Parser.ParseCode(pumaCode);
            var cppCode = CodeGenerator.GenerateCpp(ast);
            var outFile = Path.Combine(BuildFolder, Path.GetFileNameWithoutExtension(file) + ".cpp");
            File.WriteAllText(outFile, cppCode);
        }
    }
}
using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;

// Entry point
class Program
{
    static void Main(string[] args)
    {
        var sourceFolder = "src"; // Change as needed
        var buildFolder = "build";
        var compiler = new PumaCompiler(sourceFolder, buildFolder);
        compiler.Compile();
        Console.WriteLine("Compilation complete.");
    }
}
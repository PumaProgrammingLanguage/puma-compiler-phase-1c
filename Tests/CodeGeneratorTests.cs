using System;
using System.Collections.Generic;
using System.Linq;

namespace Puma.Tests
{
    internal static class CodeGeneratorTests
    {
        private static int _passed;
        private static int _failed;
        private static readonly List<string> _failMessages = new();

        public static int RunAll()
        {
            _passed = 0;
            _failed = 0;
            _failMessages.Clear();

            Test_EmptyAstReturnsNoUnits();
            Test_ModuleBasicGeneration();
            Test_ModuleLifecycleGeneration();
            Test_TypeGenerationWithBaseAndTraits();
            Test_TraitGenerationNoSourceMethods();
            Test_GenerateCppAggregateOutput();

            Console.WriteLine($"CodeGeneratorTests: Passed={_passed} Failed={_failed}");
            if (_failed > 0)
            {
                Console.WriteLine("Failures:");
                foreach (var f in _failMessages) Console.WriteLine("  " + f);
            }
            return _failed;
        }

        private static PumaFileAst NewAst() => new PumaFileAst();

        private static void Test_EmptyAstReturnsNoUnits()
        {
            var ast = NewAst(); // no module/type/trait names
            var units = CodeGenerator.GenerateCppUnits(ast);
            AssertEqual("EmptyAst Units Count", 0, units.Count);
        }

        private static void Test_ModuleBasicGeneration()
        {
            var ast = NewAst();
            ast.ModuleName = "MyModule";
            ast.Usings.Add("Other");
            ast.Functions.Add(new FunctionAst("DoWork"));
            var units = CodeGenerator.GenerateCppUnits(ast);
            AssertEqual("Module Units Count", 1, units.Count);
            var u = units[0];
            AssertEqual("Module Header File", "MyModule.h", u.HeaderFile);
            AssertEqual("Module Source File", "MyModule.c", u.SourceFile);
            AssertTrue("Module Header HasPragma", u.HeaderCode.Contains("#pragma once"));
            AssertTrue("Module Header HasInclude", u.HeaderCode.Contains("#include \"Other.h\""));
            AssertTrue("Module Header HasFunctionPrototypePrefix", u.HeaderCode.Contains("void MyModule_Fn_id_"));
            AssertTrue("Module Source IncludesHeader", u.SourceCode.Contains("#include \"MyModule.h\""));
            AssertTrue("Module Source HasFunctionStub", u.SourceCode.Contains("MyModule_Fn_id_"));
        }

        private static void Test_ModuleLifecycleGeneration()
        {
            var ast = NewAst();
            ast.ModuleName = "App";
            ast.Initialize = new InitializeAst();
            ast.Start = new StartAst();
            ast.Finalize = new FinalizeAst();
            var units = CodeGenerator.GenerateCppUnits(ast);
            var h = units[0].HeaderCode;
            var c = units[0].SourceCode;
            AssertTrue("Lifecycle Initialize Prototype", h.Contains("void App_Initialize(void);"));
            AssertTrue("Lifecycle Start Prototype", h.Contains("void App_Start(void);"));
            AssertTrue("Lifecycle Finalize Prototype", h.Contains("void App_Finalize(void);"));
            AssertTrue("Lifecycle Initialize Impl", c.Contains("void App_Initialize(void)"));
            AssertTrue("Lifecycle Start Impl", c.Contains("void App_Start(void)"));
            AssertTrue("Lifecycle Finalize Impl", c.Contains("void App_Finalize(void)"));
        }

        private static void Test_TypeGenerationWithBaseAndTraits()
        {
            var ast = NewAst();
            ast.TypeName = "Widget";
            ast.BaseType = "objectBase";
            ast.InheritedTraits.Add("Traceable");
            ast.InheritedTraits.Add("Serializable");
            ast.Functions.Add(new FunctionAst("Run"));
            ast.Initialize = new InitializeAst();
            ast.Finalize = new FinalizeAst();
            var units = CodeGenerator.GenerateCppUnits(ast);
            AssertEqual("Type Units Count", 1, units.Count);
            var u = units[0];
            AssertEqual("Type Header File", "Widget.hpp", u.HeaderFile);
            AssertEqual("Type Source File", "Widget.cpp", u.SourceFile);
            AssertTrue("Type Header ClassDecl", u.HeaderCode.Contains("class Widget"));
            AssertTrue("Type Header BaseType", u.HeaderCode.Contains(": public objectBase"));
            AssertTrue("Type Header Traits", u.HeaderCode.Contains("public Traceable") && u.HeaderCode.Contains("public Serializable"));
            AssertTrue("Type Header Method Signature", u.HeaderCode.Contains("void Method_id_"));
            AssertTrue("Type Header Initialize Decl", u.HeaderCode.Contains("void Initialize();"));
            AssertTrue("Type Source Initialize Def", u.SourceCode.Contains("void Widget::Initialize()"));
            AssertTrue("Type Source Finalize Def", u.SourceCode.Contains("void Widget::Finalize()"));
            AssertTrue("Type Source Method Def", u.SourceCode.Contains("void Widget::Method_id_"));
        }

        private static void Test_TraitGenerationNoSourceMethods()
        {
            var ast = NewAst();
            ast.TraitName = "Loggable";
            ast.Functions.Add(new FunctionAst("Log"));
            var units = CodeGenerator.GenerateCppUnits(ast);
            var u = units[0];
            AssertEqual("Trait Header File", "Loggable.hpp", u.HeaderFile);
            AssertEqual("Trait Source File", "Loggable.cpp", u.SourceFile);
            AssertTrue("Trait Header PureVirtual", u.HeaderCode.Contains("virtual void Method_id_"));
            // Trait should not have method bodies (only include + blank lines)
            AssertFalse("Trait Source ShouldNotContainMethodImpl", u.SourceCode.Contains("Loggable::Method_id_"));
        }

        private static void Test_GenerateCppAggregateOutput()
        {
            var ast = NewAst();
            ast.ModuleName = "Core";
            ast.Functions.Add(new FunctionAst ("X"));
            string combined = CodeGenerator.GenerateCpp(ast);
            AssertTrue("Aggregate Contains Header Sentinal", combined.Contains("// ===== Core.h ====="));
            AssertTrue("Aggregate Contains Source Sentinal", combined.Contains("// ===== Core.c ====="));
            AssertTrue("Aggregate Contains Prototype", combined.Contains("void Core_Fn_id_"));
        }

        // ----------------- Assertion Helpers -----------------
        private static void AssertEqual<T>(string name, T expected, T actual)
        {
            if (!Equals(expected, actual)) Fail($"{name}: expected '{expected}' got '{actual}'"); else Pass();
        }

        private static void AssertTrue(string name, bool condition)
        {
            if (!condition) Fail($"{name}: condition false"); else Pass();
        }

        private static void AssertFalse(string name, bool condition)
        {
            if (condition) Fail($"{name}: condition true unexpectedly"); else Pass();
        }

        private static void Pass() => _passed++;
        private static void Fail(string message)
        {
            _failed++;
            _failMessages.Add(message);
        }
    }
}

// Optional runner (call CodeGeneratorTestRunner.Run() or integrate with a main program)
internal static class CodeGeneratorTestRunner
{
    public static void Run()
    {
        var failed = Puma.Tests.CodeGeneratorTests.RunAll();
        if (failed > 0)
            Environment.ExitCode = 1;
    }
}
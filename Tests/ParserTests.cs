using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

// Lightweight test harness (no external framework)
namespace Puma.Tests
{
    internal static class ParserTests
    {
        private static int _passed;
        private static int _failed;
        private static readonly List<string> _failMessages = new();

        public static int RunAll()
        {
            _passed = 0;
            _failed = 0;
            _failMessages.Clear();

            Test_EmptyFile();
            Test_MinimalTypeOnly();
            Test_FullValidOrder();
            Test_OutOfOrderSections();
            Test_MultipleTypeLikeHeaders();
            Test_StartWithoutModule();
            Test_InitializeAndStartConflict();
            Test_InheritedTraitsParsing();
            Test_FinalizeParsing();
            Test_FunctionsParsing();

            Console.WriteLine($"ParserTests: Passed={_passed} Failed={_failed}");
            if (_failed > 0)
            {
                Console.WriteLine("Failures:");
                foreach (var msg in _failMessages)
                    Console.WriteLine("  " + msg);
            }
            return _failed;
        }

        private static void Test_EmptyFile()
        {
            var (ast, errors) = Parser.ParseCode("Empty.puma", "");
            AssertEqual("EmptyFile Errors", 0, errors);
            AssertNull("EmptyFile Type", ast.TypeName);
        }

        private static void Test_MinimalTypeOnly()
        {
            string code = "type MyType is object\n";
            var (ast, errors) = Parser.ParseCode("TypeOnly.puma", code);
            AssertEqual("TypeOnly Errors", 0, errors);
            AssertEqual("TypeOnly TypeName", "MyType", ast.TypeName);
            AssertEqual("TypeOnly BaseType", "object", ast.BaseType);
        }

        private static void Test_FullValidOrder()
        {
            string code = @"
using System.IO
type T1 is object has TraitA, TraitB
enums
    Color
records
    Rec1
properties
    Count = 0
initialize
    Count = 1
finalize
    // cleanup
functions
    DoWork()
        // body
";
            var (ast, errors) = Parser.ParseCode("FullValid.puma", code);
            AssertEqual("FullValid Errors", 0, errors);
            AssertEqual("FullValid Usings", 1, ast.Usings.Count);
            AssertEqual("FullValid Enums", true, ast.Enums.Count >= 1);
            AssertEqual("FullValid Records", true, ast.Records.Count >= 1);
            AssertEqual("FullValid Properties", 1, ast.Properties.Count);
            AssertNotNull("FullValid Initialize", ast.Initialize);
            AssertNotNull("FullValid Finalize", ast.Finalize);
            AssertEqual("FullValid Functions", 1, ast.Functions.Count);
            AssertEqual("FullValid Traits Parsed", 2, ast.InheritedTraits.Count);
        }

        private static void Test_OutOfOrderSections()
        {
            // properties before enums should trigger ordering error
            string code = @"
type T2 is object
properties
    A = 0
enums
    Color
";
            var (_, errors) = Parser.ParseCode("OutOfOrder.puma", code);
            AssertTrue("OutOfOrder HasError", errors > 0);
        }

        private static void Test_MultipleTypeLikeHeaders()
        {
            string code = @"
type A is object
trait B
";
            var (_, errors) = Parser.ParseCode("MultiHeader.puma", code);
            AssertTrue("MultiHeader Errors", errors > 0);
        }

        private static void Test_StartWithoutModule()
        {
            string code = @"
type A is object
start
    // invalid start
";
            var (_, errors) = Parser.ParseCode("StartNoModule.puma", code);
            AssertTrue("StartNoModule Errors", errors > 0);
        }

        private static void Test_InitializeAndStartConflict()
        {
            string code = @"
type A is object
initialize
    // init
start
    // start (invalid combination per current parser rule)
";
            var (_, errors) = Parser.ParseCode("InitStartConflict.puma", code);
            AssertTrue("InitStartConflict Errors>=1", errors >= 1);
        }

        private static void Test_InheritedTraitsParsing()
        {
            string code = @"
type MyType is object has Logging, Metrics, Serialization
";
            var (ast, errors) = Parser.ParseCode("Traits.puma", code);
            AssertEqual("Traits Errors", 0, errors);
            AssertEqual("Traits Count", 3, ast.InheritedTraits.Count);
            AssertEqual("Traits First", "Logging", ast.InheritedTraits[0]);
        }

        private static void Test_FinalizeParsing()
        {
            string code = @"
type MyType is object
finalize
    // cleanup
";
            var (ast, errors) = Parser.ParseCode("Finalize.puma", code);
            AssertEqual("Finalize Errors", 0, errors);
            AssertNotNull("Finalize Ast.Finalize", ast.Finalize);
        }

        private static void Test_FunctionsParsing()
        {
            string code = @"
type X is object
functions
    DoOne()
        // body
    end
    DoTwo(param1 int32)
        // body
    end
";
            var (ast, errors) = Parser.ParseCode("Functions.puma", code);
            AssertEqual("Functions Errors", 0, errors);
            AssertEqual("Functions Count", 2, ast.Functions.Count);
            var names = ast.Functions.Select(f => f.Name).ToList();
            AssertTrue("Functions Contains DoOne", names.Contains("DoOne"));
            AssertTrue("Functions Contains DoTwo", names.Contains("DoTwo"));
        }

        // -------- Assertion Helpers --------
        private static void AssertEqual<T>(string name, T expected, T actual)
        {
            if (!Equals(expected, actual))
                Fail($"{name}: Expected '{expected}' got '{actual}'");
            else
                Pass();
        }

        private static void AssertTrue(string name, bool condition)
        {
            if (!condition) Fail($"{name}: Condition false"); else Pass();
        }

        private static void AssertNull(string name, object? value)
        {
            if (value != null) Fail($"{name}: Expected null"); else Pass();
        }

        private static void AssertNotNull(string name, object? value)
        {
            if (value == null) Fail($"{name}: Expected not null"); else Pass();
        }

        private static void Pass() => _passed++;
        private static void Fail(string message)
        {
            _failed++;
            _failMessages.Add(message);
        }
    }
}

// Optional runner (call ParserTestRunner.Run() manually, or add to your Main)
internal static class ParserTestRunner
{
    public static void Run()
    {
        var failed = Puma.Tests.ParserTests.RunAll();
        if (failed > 0)
            Environment.ExitCode = 1;
    }
}
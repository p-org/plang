﻿using PChecker.SystematicTesting;
using PChecker.PChecker.Compiler;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Plang.Compiler;
using UnitTests.Core;

namespace UnitTests.Runners
{
    internal class CoyoteRunner : ICompilerTestRunner
    {
        private static readonly string CoyoteAssemblyLocation =
            Path.GetDirectoryName(typeof(TestingEngine).GetTypeInfo().Assembly.Location);

        private readonly FileInfo[] nativeSources;
        private readonly FileInfo[] sources;

        public CoyoteRunner(FileInfo[] sources)
        {
            this.sources = sources;
            nativeSources = new FileInfo[] { };
        }

        public CoyoteRunner(FileInfo[] sources, FileInfo[] nativeSources)
        {
            this.sources = sources;
            this.nativeSources = nativeSources;
        }

        private void FileCopy(string src, string target, bool overwrite)
        {
            // during parallel testing we might get "The process cannot access the file because it is being used by another process."
            int retries = 5;
            while (retries-- > 0)
            {
                try
                {
                    File.Copy(src, target, overwrite);
                    return;
                }
                catch (System.IO.IOException)
                {
                    if (retries == 1)
                    {
                        throw;
                    }
                    System.Threading.Thread.Sleep(1000);
                }
            }
        }

        public int? RunTest(DirectoryInfo scratchDirectory, out string stdout, out string stderr)
        {
            stdout = "";
            stderr = "";
            // Do not want to use the auto-generated Test.cs file
            CreateFileWithMainFunction(scratchDirectory);
            // Do not want to use the auto-generated csproj file
            CreateCSProjFile(scratchDirectory);
            // copy the foreign code to the folder
            foreach (FileInfo nativeFile in nativeSources)
            {
                FileCopy(nativeFile.FullName, Path.Combine(scratchDirectory.FullName, nativeFile.Name), true);
            }

            int exitCode = DoCompile(scratchDirectory);

            if (exitCode == 0)
            {
                exitCode = RunCoyoteTester(scratchDirectory.FullName,
                    Path.Combine(scratchDirectory.FullName, "./net6.0/Main.dll"), out string testStdout, out string testStderr);
                stdout += testStdout;
                stderr += testStderr;
            }

            return exitCode;
        }

        private void CreateCSProjFile(DirectoryInfo scratchDirectory)
        {
            const string csprojTemplate = @"
<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net6.0</TargetFramework>
    <ApplicationIcon />
    <OutputType>Exe</OutputType>
    <StartupObject />
    <LangVersion>latest</LangVersion>
    <OutputPath>.</OutputPath>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include=""$(PFolder)/Src/PRuntimes/PCSharpRuntime/CSharpRuntime.csproj"" />
    <ProjectReference Include=""$(PFolder)/Src/PChecker/Source/Core/Core.csproj"" />
  </ItemGroup>
</Project>";
            using var outputFile = new StreamWriter(Path.Combine(scratchDirectory.FullName, "Main.csproj"), false);
            outputFile.WriteLine(csprojTemplate);
        }

        private void CreateFileWithMainFunction(DirectoryInfo dir)
        {
            string testCode = @"
using PChecker;
using PChecker.SystematicTesting;
using System;
using System.Linq;

namespace PImplementation
{
    public class _TestRegression {
        public static void Main(string[] args)
        {
            // Optional: increases verbosity level to see the Coyote runtime log.
            Configuration configuration = Configuration.Create().WithTestingIterations(1000);
            configuration.WithMaxSchedulingSteps(1000);
            TestingEngine engine = TestingEngine.Create(configuration, DefaultImpl.Execute);
            engine.Run();
            string bug = engine.TestReport.BugReports.FirstOrDefault();
            if (bug != null)
            {
                Console.WriteLine(bug);
                Environment.Exit(1);
            }
            Environment.Exit(0);

            // for debugging:
            /* For replaying a bug and single stepping
            Configuration configuration = Configuration.Create();
            configuration.WithVerbosityEnabled(true);
            // update the path to the schedule file.
            configuration.WithReplayStrategy(""AfterNewUpdate.schedule"");
            TestingEngine engine = TestingEngine.Create(configuration, DefaultImpl.Execute);
            engine.Run();
            string bug = engine.TestReport.BugReports.FirstOrDefault();
            if (bug != null)
            {
                Console.WriteLine(bug);
            }
            */
        }
    }
}";
            using (StreamWriter outputFile = new StreamWriter(Path.Combine(dir.FullName, "Test.cs"), false))
            {
                outputFile.WriteLine(testCode);
            }
        }

        private int RunCoyoteTester(string directory, string dllPath, out string stdout, out string stderr)
        {
            return ProcessHelper.RunWithOutput(directory, out stdout, out stderr, "dotnet", dllPath);
        }

        private int DoCompile(DirectoryInfo scratchDirectory)
        {
            Compiler compiler = new Compiler();
            TestExecutionStream outputStream = new TestExecutionStream(scratchDirectory);
            CompilationJob compilationJob = new CompilationJob(outputStream, scratchDirectory, CompilerOutput.CSharp, sources.Select(x => x.FullName).ToList(), "Main", scratchDirectory);
            try
            {
                compiler.Compile(compilationJob);
                return 0;
            }
            catch (TranslationException e)
            {
                compilationJob.Output.WriteError("Error:\n" + e.Message);
                return 1;
            }
            catch (Exception ex)
            {
                compilationJob.Output.WriteError($"<Internal Error>:\n {ex.Message}\n<Please report to the P team or create an issue on GitHub, Thanks!>");
                return 1;
            }
        }
    }
}
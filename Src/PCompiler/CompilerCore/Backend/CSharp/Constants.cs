namespace Plang.Compiler.Backend.CSharp
{
    internal class Constants
    {
        internal static readonly string csprojTemplate = @"
<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ApplicationIcon />
    <OutputType>Exe</OutputType>
    <StartupObject />
    <LangVersion>latest</LangVersion>
    <OutputPath>./</OutputPath>
  </PropertyGroup>
-foreign-include-
  <ItemGroup>
    <Reference Include=""PCheckerCore"">
        <HintPath>-assembly-path-/PCheckerCore.dll</HintPath>
    </Reference>
  </ItemGroup>
</Project>";

        internal static readonly string mainCode = @"
using PChecker;
using PChecker.SystematicTesting;
using System;
using System.IO;
using System.Linq;
using PChecker.Actors;

namespace PImplementation
{
    public class _TestRegression {
        public static void Main(string[] args)
        {
            /*
            CheckerConfiguration configuration = CheckerConfiguration.Create();
            configuration.WithVerbosityEnabled(true);
            // update the path to the schedule file.
            string schedule = File.ReadAllText(""absolute path to *.schedule file"");
            configuration.WithReplayStrategy(schedule);
            TestingEngine engine = TestingEngine.Create(configuration, (Action<IActorRuntime>)PImplementation.<Name of the test case>.Execute);
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
    }
}
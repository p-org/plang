package psym;

import static org.junit.jupiter.api.Assertions.assertTimeoutPreemptively;

import java.io.File;
import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.time.Duration;
import java.util.*;
import java.util.stream.Collectors;
import java.util.stream.Stream;
import org.junit.jupiter.api.Assertions;
import org.junit.jupiter.api.DynamicTest;
import org.junit.jupiter.api.TestFactory;
import org.junit.jupiter.api.function.Executable;
import psym.runtime.logger.Log4JConfig;

/**
 * Runner for Symbolic P Regressions. Pre-requisites: Run from P repository as a submodule Build the
 * symbolic compiler to ../Bld/Drops/Release/Binaries/Pc.dll Place test cases as source P files at
 * ../Tst/SymbolicRegressionTests/
 */
public class TestSymbolicRegression {
  private static final String outputDirectory = "output/testCases";
  private static final List<String> excluded = new ArrayList<>();
  private static String mode = "verification";
  private static String timeout = "60";
  private static String schedules = "50";
  private static String maxSteps = "300";
  private static String runArgs = "";
  private static boolean initialized = false;

  private static void setRunArgs() {
    String md = System.getProperty("mode");
    String to = System.getProperty("timeout");
    String it = System.getProperty("schedules");
    String ms = System.getProperty("max.steps");
    String psymArgs = System.getProperty("psym.args");

    if (md != null && !md.isEmpty()) {
      switch (md) {
        case "verification":
        case "coverage":
          mode = md;
          break;
        default:
          break;
      }
    }
    if (to != null && !to.isEmpty()) {
      timeout = to;
    }
    if (it != null && !it.isEmpty()) {
      schedules = it;
    }
    if (ms != null && !ms.isEmpty()) {
      maxSteps = ms;
    }

    runArgs += String.format(" --timeout %s --schedules %s --max-steps %s", timeout, schedules, maxSteps);

    if (psymArgs != null && !psymArgs.isEmpty()) {
      runArgs += String.format(" --psym-args :%s ", psymArgs.replace(" ", ":"));
    }
    PSymTestLogger.log(String.format("Running in mode %s with arguments:  %s", mode, runArgs));
  }

  private static void createExcludeList() {
    // TODO: fix key not found error when using set as key
    excluded.add("../../../Tst/RegressionTests/Integration/Correct/TestMapSet");

    // TODO Unsupported: too many choices to throw error if choices more than 10000
    excluded.add("../../../Tst/RegressionTests/Feature3Exprs/DynamicError/tooManyChoicesSeq");
    excluded.add("../../../Tst/RegressionTests/Feature3Exprs/DynamicError/tooManyChoicesSet");
    excluded.add("../../../Tst/RegressionTests/Feature3Exprs/DynamicError/tooManyChoicesMap");

    // TODO Unsupported: liveness with temperatures
    excluded.add("../../../Tst/RegressionTests/Liveness/Correct/Liveness_1");
    excluded.add("../../../Tst/RegressionTests/Liveness/Correct/Liveness_1_falsePass");
    excluded.add("../../../Tst/RegressionTests/Liveness/Correct/Liveness_FAIRNONDET");
    excluded.add("../../../Tst/RegressionTests/Liveness/Correct/Liveness_FAIRNONDET2");

    // TODO Unsupported: deadlock detection
    excluded.add("../../../Tst/RegressionTests/Feature2Stmts/DynamicError/receive2");
    excluded.add("../../../Tst/RegressionTests/Feature2Stmts/DynamicError/receive6");
    excluded.add("../../../Tst/RegressionTests/Feature2Stmts/DynamicError/receive7");

    // TODO Unsupported: continue statement
    excluded.add("../../../Tst/RegressionTests/Feature2Stmts/Correct/foreach2");
    excluded.add("../../../Tst/RegressionTests/Feature2Stmts/Correct/foreach4");
    excluded.add("../../../Tst/RegressionTests/Feature2Stmts/DynamicError/foreach2");
    excluded.add("../../../Tst/RegressionTests/Feature2Stmts/DynamicError/foreach4");
    excluded.add("../../../Tst/RegressionTests/Feature2Stmts/DynamicError/continue1");

    // TODO Unsupported: receive in state exit functions
    excluded.add("../../../Tst/RegressionTests/Feature2Stmts/Correct/receive14");
    excluded.add("../../../Tst/RegressionTests/Feature2Stmts/Correct/receive15");
    excluded.add("../../../Tst/RegressionTests/Feature2Stmts/Correct/receive16");

    // TODO Unsupported: nested type casting in tuple fields with any type
    excluded.add("../../../Tst/RegressionTests/Feature4DataTypes/Correct/nonAtomicDataTypes");
    excluded.add("../../../Tst/RegressionTests/Feature3Exprs/Correct/cast3");

    // TODO Unsupported: null events
    excluded.add("../../../Tst/RegressionTests/Integration/Correct/openwsn1");
    excluded.add("../../../Tst/RegressionTests/Integration/DynamicError/SEM_TwoMachines_9");
    excluded.add("../../../Tst/RegressionTests/Integration/DynamicError/SEM_OneMachine_42");
    excluded.add("../../../Tst/RegressionTests/Integration/DynamicError/SEM_TwoMachines_12");
    excluded.add("../../../Tst/RegressionTests/Integration/DynamicError/SEM_OneMachine_41");
    excluded.add("../../../Tst/RegressionTests/Integration/DynamicError/SEM_OneMachine_38");
    excluded.add("../../../Tst/RegressionTests/Integration/DynamicError/SEM_TwoMachines_16");
    excluded.add("../../../Tst/RegressionTests/Integration/DynamicError/SEM_OneMachine_36");
    excluded.add("../../../Tst/RegressionTests/Integration/DynamicError/SEM_TwoMachines_18");
    excluded.add("../../../Tst/RegressionTests/Integration/DynamicError/SEM_TwoMachines_19");
    excluded.add("../../../Tst/RegressionTests/Integration/DynamicError/SEM_OneMachine_37");
    excluded.add("../../../Tst/RegressionTests/Integration/DynamicError/SEM_TwoMachines_17");
    excluded.add("../../../Tst/RegressionTests/Integration/DynamicError/SEM_OneMachine_39");
    excluded.add("../../../Tst/RegressionTests/Integration/DynamicError/SEM_TwoMachines_10");
    excluded.add("../../../Tst/RegressionTests/Feature1SMLevelDecls/Correct/BugRepro");
    excluded.add("../../../Tst/RegressionTests/Feature1SMLevelDecls/Correct/MoreThan32Events");
    excluded.add(
        "../../../Tst/RegressionTests/Feature1SMLevelDecls/StaticError/ActionAndTransitionSameEvent");
    excluded.add("../../../Tst/RegressionTests/Feature1SMLevelDecls/StaticError/IgnoredNullEvent");
    excluded.add("../../../Tst/RegressionTests/Feature1SMLevelDecls/StaticError/SentNullEvent");
    excluded.add("../../../Tst/RegressionTests/Feature1SMLevelDecls/StaticError/RaisedNullEvent");
    excluded.add("../../../Tst/RegressionTests/Feature1SMLevelDecls/StaticError/AnonFuns");
    excluded.add("../../../Tst/RegressionTests/Feature1SMLevelDecls/StaticError/NullEventDecl");
    excluded.add("../../../Tst/RegressionTests/Feature1SMLevelDecls/StaticError/DeferredNullEvent");
    excluded.add(
        "../../../Tst/RegressionTests/Feature1SMLevelDecls/StaticError/UndefinedStateInTransition");
    excluded.add(
        "../../../Tst/RegressionTests/Feature1SMLevelDecls/StaticError/TransitionOnNullInSpecMachine");
    excluded.add("../../../Tst/RegressionTests/Feature3Exprs/StaticError/payloadEntry");
    excluded.add("../../../Tst/RegressionTests/Feature3Exprs/StaticError/payloadTransitions");
    excluded.add("../../../Tst/RegressionTests/Feature3Exprs/StaticError/payloadActions");
    excluded.add("../../../Tst/RegressionTests/Feature3Exprs/StaticError/payloadEntry_1");
    excluded.add("../../../Tst/RegressionTests/Feature3Exprs/StaticError/payloadActionsFuns");
  }

  private static void initialize() {
    Log4JConfig.configureLog4J();
    PSymTestLogger.Initialize(outputDirectory);
    setRunArgs();
    createExcludeList();
    initialized = true;
  }

  Map<String, List<String>> getFiles(String testDirPath) {
    Map<String, List<String>> result = new HashMap<>();
    File[] directories = new File(testDirPath).listFiles(File::isDirectory);
    for (File dir : directories) {
      if (excluded.stream().anyMatch(dir.toString()::equals)) {
        continue;
      }
      try (Stream<Path> walk = Files.walk(Paths.get(dir.toURI()))) {
        Stream<String> projectFilesStream =
            walk.map(Path::toString).filter(f -> f.endsWith(".java") || f.endsWith(".p"));
        projectFilesStream =
            projectFilesStream.filter(f -> excluded.stream().noneMatch(f::contains));
        List<String> projectFiles = projectFilesStream.collect(Collectors.toList());
        if (!projectFiles.isEmpty()) result.put(dir.toString(), projectFiles);
      } catch (IOException e) {
        e.printStackTrace();
      }
    }
    PSymTestLogger.log(String.format("  Found %s tests in %s", result.size(), testDirPath));
    return result;
  }

  void runDynamicTest(
      int expected,
      List<String> testCasePaths,
      String testCasePath,
      Collection<DynamicTest> dynamicTests) {
    Executable exec =
        () ->
            Assertions.assertEquals(
                expected,
                TestCaseExecutor.runTestCase(
                    testCasePaths,
                    testCasePath,
                    TestSymbolicRegression.mode,
                    TestSymbolicRegression.runArgs,
                    outputDirectory,
                    expected));
    DynamicTest dynamicTest =
        DynamicTest.dynamicTest(
            testCasePath, () -> assertTimeoutPreemptively(Duration.ofMinutes(60), exec));
    dynamicTests.add(dynamicTest);
  }

  Collection<DynamicTest> loadTests(String testDirPath) {
    if (!initialized) {
      initialize();
    }

    Collection<DynamicTest> dynamicTests = new ArrayList<>();

    List<String> testDirs = new ArrayList<>();

    try (Stream<Path> walk = Files.walk(Paths.get(testDirPath))) {
      testDirs =
          walk.map(Path::toString)
              .filter(
                  f ->
                      f.endsWith("Correct")
                          || f.endsWith("DynamicError")
                          || f.endsWith("StaticError"))
              .collect(Collectors.toList());
    } catch (IOException e) {
      e.printStackTrace();
    }

    for (String testDir : testDirs) {
      Map<String, List<String>> paths = getFiles(testDir);
      List<String> pathKeys = new ArrayList<>(paths.keySet());
      Collections.sort(pathKeys, String.CASE_INSENSITIVE_ORDER);

      if (testDir.contains("Correct")) {
        for (String key : pathKeys) {
          runDynamicTest(0, paths.get(key), key, dynamicTests);
        }
      } else if (testDir.contains("DynamicError")) {
        for (String key : pathKeys) {
          runDynamicTest(2, paths.get(key), key, dynamicTests);
        }
      } else if (testDir.contains("StaticError")) {
        for (String key : pathKeys) {
          runDynamicTest(1, paths.get(key), key, dynamicTests);
        }
      }
    }
    return dynamicTests;
  }

  @TestFactory
  // @Timeout(value = 1, unit = TimeUnit.MILLISECONDS)
  Collection<DynamicTest> loadSymbolicRegressionsTests() {
    return loadTests("./SymbolicRegressionTests/Integration");
  }

  @TestFactory
  // @Timeout(value = 1, unit = TimeUnit.MILLISECONDS)
  public Collection<DynamicTest> loadIntegrationTests() {
    return loadTests("../../../Tst/RegressionTests/Integration");
  }

  @TestFactory
  // @Timeout(value = 1, unit = TimeUnit.MILLISECONDS)
  Collection<DynamicTest> loadCombinedTests() {
    return loadTests("../../../Tst/RegressionTests/Combined");
  }

  @TestFactory
  Collection<DynamicTest> loadSMLevelDeclsTests() {
    return loadTests("../../../Tst/RegressionTests/Feature1SMLevelDecls");
  }

  @TestFactory
  // @Timeout(value = 1, unit = TimeUnit.MILLISECONDS)
  Collection<DynamicTest> loadStmtsTests() {
    return loadTests("../../../Tst/RegressionTests/Feature2Stmts");
  }

  @TestFactory
  // @Timeout(value = 1, unit = TimeUnit.MILLISECONDS)
  Collection<DynamicTest> loadExpressionTests() {
    return loadTests("../../../Tst/RegressionTests/Feature3Exprs");
  }

  @TestFactory
  // @Timeout(value = 1, unit = TimeUnit.MILLISECONDS)
  Collection<DynamicTest> loadDataTypeTests() {
    return loadTests("../../../Tst/RegressionTests/Feature4DataTypes");
  }

  // TODO Unsupported: module system
  //    @TestFactory
  //        //@Timeout(value = 1, unit = TimeUnit.MILLISECONDS)
  //    Collection<DynamicTest>  loadModuleSystemTests() {
  //        return loadTests("../../../Tst/RegressionTests/Feature5ModuleSystem");
  //    }

  @TestFactory
  //@Timeout(value = 1, unit = TimeUnit.MILLISECONDS)
  Collection<DynamicTest>  loadLivenessTests() {
      return loadTests("../../../Tst/RegressionTests/Liveness");
  }
}

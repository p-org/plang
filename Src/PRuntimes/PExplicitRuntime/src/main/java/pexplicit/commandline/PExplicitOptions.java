package pexplicit.commandline;

import static java.lang.System.exit;

import java.io.*;
import org.apache.commons.cli.*;

/**
 * Represents the CLI options for PExplicit runtime
 */
public class PExplicitOptions {
    private static final Options allOptions;
    private static final Options visibleOptions;
    private static final PrintWriter writer = new PrintWriter(System.out);
    private static final HelpFormatter formatter = new HelpFormatter();

    static {
        allOptions = new Options();
        visibleOptions = new Options();

        /*
         * Basic options
         */

        // test driver name
        Option testName =
                Option.builder("tc")
                        .longOpt("testcase")
                        .desc("Test case to explore")
                        .numberOfArgs(1)
                        .hasArg()
                        .argName("Test Case (string)")
                        .build();
        addOption(testName);

        // project name
        Option projName =
                Option.builder("pn")
                        .longOpt("projname")
                        .desc("Project name")
                        .numberOfArgs(1)
                        .hasArg()
                        .argName("Project Name (string)")
                        .build();
        addOption(projName);

        // output folder
        Option outputDir =
                Option.builder("o")
                        .longOpt("outdir")
                        .desc("Dump output to directory (absolute or relative path)")
                        .numberOfArgs(1)
                        .hasArg()
                        .argName("Output Dir (string)")
                        .build();
        addOption(outputDir);

        // time limit
        Option timeLimit =
                Option.builder("t")
                        .longOpt("timeout")
                        .desc("Timeout in seconds (disabled by default)")
                        .numberOfArgs(1)
                        .hasArg()
                        .argName("Time Limit (seconds)")
                        .build();
        addOption(timeLimit);

        // memory limit
        Option memLimit =
                Option.builder("m")
                        .longOpt("memout")
                        .desc("Memory limit in Giga bytes (auto-detect by default)")
                        .numberOfArgs(1)
                        .hasArg()
                        .argName("Memory Limit (GB)")
                        .build();
        addOption(memLimit);

        // set the level of verbosity
        Option verbosity =
                Option.builder("v")
                        .longOpt("verbose")
                        .desc("Level of verbose log output during exploration (default: 0)")
                        .numberOfArgs(1)
                        .hasArg()
                        .argName("Log Verbosity (integer)")
                        .build();
        addOption(verbosity);

        /*
         * Exploration options
         */

        // strategy of exploration
        Option strategy =
                Option.builder("st")
                        .longOpt("strategy")
                        .desc("Exploration strategy: dfs (default: dfs)")
                        .numberOfArgs(1)
                        .hasArg()
                        .argName("Strategy (string)")
                        .build();
        addOption(strategy);

        // max number of schedules for the search
        Option maxSchedules =
                Option.builder("s")
                        .longOpt("schedules")
                        .desc("Number of schedules to explore (default: 1)")
                        .numberOfArgs(1)
                        .hasArg()
                        .argName("Schedules (integer)")
                        .build();
        addOption(maxSchedules);

        // max steps/depth bound for the search
        Option maxSteps =
                Option.builder("ms")
                        .longOpt("max-steps")
                        .desc("Max scheduling steps to be explored (default: 10,000)")
                        .numberOfArgs(1)
                        .hasArg()
                        .argName("Max Steps (integer)")
                        .build();
        addOption(maxSteps);

        // whether or not to fail on reaching max step bound
        Option failOnMaxSteps =
                Option.builder("fms")
                        .longOpt("fail-on-maxsteps")
                        .desc("Consider it a bug if the test hits the specified max-steps")
                        .numberOfArgs(0)
                        .build();
        addOption(failOnMaxSteps);

        // random seed for the search
        Option randomSeed =
                Option.builder()
                        .longOpt("seed")
                        .desc("Specify the random value generator seed")
                        .numberOfArgs(1)
                        .hasArg()
                        .argName("Random Seed (integer)")
                        .build();
        addOption(randomSeed);

        /*
         * Help menu options
         */
        Option help = Option.builder("h").longOpt("help").desc("Show help menu").build();
        addOption(help);

        Option helpAll = Option.builder().longOpt("help-all").desc("Show complete help menu").build();
        addHiddenOption(helpAll);
    }

    private static void addHiddenOption(Option opt) {
        allOptions.addOption(opt);
    }

    private static void addOption(Option opt) {
        allOptions.addOption(opt);
        visibleOptions.addOption(opt);
    }

    private static void optionError(Option opt, String msg) {
        writer.println(msg);
        if (opt != null) {
            Options opts = new Options();
            opts.addOption(opt);
            formatter.printHelp(writer, 100, opt.getLongOpt(), "", opts, 2, 2, "Try --help for details.");
        }
        writer.flush();
        exit(10);
    }

    public static PExplicitConfig ParseCommandlineArgs(String[] args) {
        // Parse the commandline arguments
        CommandLineParser parser = new DefaultParser();
        formatter.setOptionComparator(null);
        CommandLine cmd = null;
        try {
            cmd = parser.parse(allOptions, args);
        } catch (ParseException e) {
            System.out.println(e.getMessage());
            formatter.printUsage(writer, 100, "java -jar <.jar-file>", allOptions);
            writer.flush();
            System.out.println("Try --help for details.");
            exit(10);
        }

        PExplicitConfig config = new PExplicitConfig();

        // Populate the configuration based on the commandline arguments
        for (Option option : cmd.getOptions()) {
            switch (option.getLongOpt()) {
                // basic options
                case "tc":
                case "testcase":
                    config.setTestDriver(option.getValue());
                    break;
                case "projname":
                    config.setProjectName(option.getValue());
                    break;
                case "o":
                case "outdir":
                    config.setOutputFolder(option.getValue());
                    break;
                case "t":
                case "timeout":
                    try {
                        config.setTimeLimit(Double.parseDouble(option.getValue()));
                        if (config.getMaxSchedules() == 1) {
                            config.setMaxSchedules(0);
                        }
                    } catch (NumberFormatException ex) {
                        optionError(
                                option, String.format("Expected a double value, got %s", option.getValue()));
                    }
                    break;
                case "m":
                case "memout":
                    try {
                        config.setMemLimit(Double.parseDouble(option.getValue()) * 1024);
                    } catch (NumberFormatException ex) {
                        optionError(
                                option, String.format("Expected a double value, got %s", option.getValue()));
                    }
                    break;
                case "v":
                case "verbose":
                    try {
                        config.setVerbosity(Integer.parseInt(option.getValue()));
                    } catch (NumberFormatException ex) {
                        optionError(
                                option, String.format("Expected an integer value, got %s", option.getValue()));
                    }
                    break;
                case "st":
                case "strategy":
                    switch (option.getValue()) {
                        case "dfs":
                            config.setToDfs();
                            break;
                        default:
                            optionError(
                                    option,
                                    String.format("Unrecognized strategy of exploration, got %s", option.getValue()));
                    }
                    break;
                // exploration options
                case "s":
                case "schedules":
                    try {
                        config.setMaxSchedules(Integer.parseInt(option.getValue()));
                    } catch (NumberFormatException ex) {
                        optionError(
                                option, String.format("Expected an integer value, got %s", option.getValue()));
                    }
                    break;
                case "ms":
                case "max-steps":
                    try {
                        config.setMaxStepBound(Integer.parseInt(option.getValue()) + 1);
                    } catch (NumberFormatException ex) {
                        optionError(
                                option, String.format("Expected an integer value, got %s", option.getValue()));
                    }
                    break;
                case "fms":
                case "fail-on-maxsteps":
                    config.setFailOnMaxStepBound(true);
                    break;
                case "seed":
                    try {
                        config.setRandomSeed(Long.parseLong(option.getValue()));
                    } catch (NumberFormatException ex) {
                        optionError(
                                option, String.format("Expected an integer value, got %s", option.getValue()));
                    }
                    break;
                case "h":
                case "help":
                    formatter.printHelp(
                            100,
                            "java -jar <.jar-file> [options]",
                            "-----------------------------------\nCommandline options for PSym/PCover\n-----------------------------------",
                            visibleOptions,
                            "See https://p-org.github.io/P/ for details.");
                    exit(0);
                    break;
                case "help-all":
                    formatter.printHelp(
                            100,
                            "java -jar <.jar-file> [options]",
                            "-----------------------------------\nCommandline options for PSym/PCover\n-----------------------------------",
                            allOptions,
                            "See https://p-org.github.io/P/ for details.");
                    exit(0);
                    break;
                default:
                    optionError(option, String.format("Unrecognized option %s", option));
            }
        }

        return config;
    }

}

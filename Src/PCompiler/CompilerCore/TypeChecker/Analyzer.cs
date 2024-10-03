using System;
using System.Collections.Generic;
using System.Linq;
using Antlr4.Runtime.Tree;
using Plang.Compiler.TypeChecker.AST;
using Plang.Compiler.TypeChecker.AST.Declarations;

namespace Plang.Compiler.TypeChecker
{
    public static class Analyzer
    {
        public static Scope AnalyzeCompilationUnit(ICompilerConfiguration config,
            params PParser.ProgramContext[] programUnits)
        {
            var handler = config.Handler;

            // Step 1: Build the global scope of declarations
            var globalScope = BuildGlobalScope(config, programUnits);

            // Step 2: Validate machine specifications
            foreach (var machine in globalScope.Machines)
            {
                MachineChecker.Validate(handler, machine, config);
            }

            // Step 3: Fill function bodies
            var allFunctions = globalScope.GetAllMethods().ToList();
            foreach (var machineFunction in allFunctions)
            {
                FunctionBodyVisitor.PopulateMethod(config, machineFunction);
                FunctionValidator.CheckAllPathsReturn(handler, machineFunction);
            }

            // Step 2: Validate no static handlers
            foreach (var machine in globalScope.Machines)
            {
                MachineChecker.ValidateNoStaticHandlers(handler, machine);
            }

            // Step 4: Propagate purity properties
            ApplyPropagations(allFunctions,
                CreatePropagation(fn => fn.CanRaiseEvent, (fn, value) => fn.CanRaiseEvent = value,
                    true),
                CreatePropagation(fn => fn.CanReceive, (fn, value) => fn.CanReceive = value,
                    true),
                CreatePropagation(fn => fn.CanChangeState, (fn, value) => fn.CanChangeState = value,
                    true),
                CreatePropagation(fn => fn.CanCreate, (fn, value) => fn.CanCreate = value,
                    true),
                CreatePropagation(fn => fn.CanSend, (fn, value) => fn.CanSend = value,
                    true),
                CreatePropagation(fn => fn.IsNondeterministic, (fn, value) => fn.IsNondeterministic = value,
                    true)
            );

            // Step 5: Verify capability restrictions
            foreach (var function in allFunctions)
            {
                // This can been checked before but just doing it again for safety!
                if (function.Owner?.IsSpec == true && (function.IsNondeterministic || function.CanCreate || function.CanSend || function.CanReceive))
                {
                    throw handler.IllegalFunctionUsedInSpecMachine(function, function.Owner);
                }

                // A static function if it has side effects or is non-deterministic then it cannot be called from a spec machine
                if (function.Owner == null && (function.IsNondeterministic || function.CanCreate || function.CanSend|| function.CanReceive))
                {
                    foreach (var caller in function.Callers)
                    {
                        if (caller.Owner?.IsSpec == true)
                        {
                            throw handler.IllegalFunctionUsedInSpecMachine(function, caller.Owner);
                        }
                    }
                }
                if ((function.CanChangeState || function.CanRaiseEvent) &&
                    (function.Role.HasFlag(FunctionRole.TransitionFunction) ||
                     function.Role.HasFlag(FunctionRole.ExitHandler)))
                {
                    throw handler.ChangedStateMidTransition(function.SourceLocation, function);
                }
            }

            // Step 6: Check control flow well-formedness
            ControlFlowChecker.AnalyzeMethods(handler, allFunctions);

            // Step 7: Infer the creates set for each machine.
            foreach (var machine in globalScope.Machines)
            {
                InferMachineCreates.Populate(machine, handler);
            }

            // Step 8: Fill the module expressions
            ModuleSystemDeclarations.PopulateAllModuleExprs(handler, globalScope);

            var moduleTypeChecker = new ModuleSystemTypeChecker(handler, globalScope);
            // Step 9: Check that all module expressions are well-formed
            foreach (var moduleExpr in AllModuleExprs(globalScope))
            {
                moduleTypeChecker.CheckWellFormedness(moduleExpr);
            }

            // Step 10: Check the test and implementation declarations
            foreach (var impl in globalScope.Implementations)
            {
                moduleTypeChecker.CheckImplementationDecl(impl);
            }

            foreach (var test in globalScope.SafetyTests)
            {
                moduleTypeChecker.CheckSafetyTest(test);
            }

            foreach (var test in globalScope.RefinementTests)
            {
                moduleTypeChecker.CheckRefinementTest(test);
            }

            return globalScope;
        }

        private static Propagation<T> CreatePropagation<T>(Func<Function, T> getter, Action<Function, T> setter,
            T value)
        {
            return new Propagation<T>
            {
                Getter = getter,
                Setter = setter,
                ActiveValue = value
            };
        }

        private static void ApplyPropagations<T>(IEnumerable<Function> functions, params Propagation<T>[] propagations)
        {
            foreach (var function in functions)
            {
                foreach (var propagation in propagations)
                {
                    if (propagation.Getter(function).Equals(propagation.ActiveValue))
                    {
                        propagation.PropertyStack.Push(function);
                    }
                }
            }

            foreach (var propagation in propagations)
            {
                while (propagation.PropertyStack.Any())
                {
                    foreach (var caller in propagation.PropertyStack.Pop().Callers)
                    {
                        if (!propagation.Getter(caller).Equals(propagation.ActiveValue))
                        {
                            propagation.Setter(caller, propagation.ActiveValue);
                            propagation.PropertyStack.Push(caller);
                        }
                    }
                }
            }
        }

        private static Scope BuildGlobalScope(ICompilerConfiguration config, PParser.ProgramContext[] programUnits)
        {
            var globalScope = Scope.CreateGlobalScope(config);
            var nodesToDeclarations = new ParseTreeProperty<IPDecl>();

            // Add built-in events to the table.
            globalScope.Put("null", (PParser.EventDeclContext)null);
            globalScope.Put("halt", (PParser.EventDeclContext)null);

            // Step 1: Create mapping of names to declaration stubs
            foreach (var programUnit in programUnits)
            {
                DeclarationStubVisitor.PopulateStubs(globalScope, programUnit, nodesToDeclarations);
            }

            // Step 2: Validate declarations and fill with types
            foreach (var programUnit in programUnits)
            {
                DeclarationVisitor.PopulateDeclarations(config.Handler, globalScope, programUnit, nodesToDeclarations);
            }

            // Step 3: Assign param types for scenario events. We have do this after all events are initialized.
            foreach (var function in globalScope.Functions)
            {
                ScenarioEventVisitor.PopulateEventTypes(function);
            }


            return globalScope;
        }

        private static IEnumerable<IPModuleExpr> AllModuleExprs(Scope globalScope)
        {
            // first do all the named modules
            foreach (var mod in globalScope.NamedModules)
            {
                yield return mod.ModExpr;
            }

            // all the test declarations
            foreach (var test in globalScope.SafetyTests)
            {
                yield return test.ModExpr;
            }

            foreach (var test in globalScope.RefinementTests)
            {
                yield return test.LeftModExpr;
            }

            foreach (var test in globalScope.RefinementTests)
            {
                yield return test.RightModExpr;
            }

            // all the implementations
            foreach (var impl in globalScope.Implementations)
            {
                yield return impl.ModExpr;
            }
        }

        private class Propagation<T>
        {
            public Stack<Function> PropertyStack { get; } = new Stack<Function>();
            public Func<Function, T> Getter { get; set; }
            public Action<Function, T> Setter { get; set; }
            public T ActiveValue { get; set; }
        }
    }
}
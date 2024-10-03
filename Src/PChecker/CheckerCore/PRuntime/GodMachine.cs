using System;
using PChecker.Actors;
using PChecker.Actors.Events;

namespace PChecker.PRuntime
{
    public class _GodMachine : StateMachine
    {
        private void InitOnEntry(Event e)
        {
            var mainMachine = (e as Config).MainMachine;
            CreateActor(mainMachine, mainMachine.Name,
                new PMachine.InitializeParametersEvent(
                    new PMachine.InitializeParameters("I_" + mainMachine.Name, null)));
        }

        public class Config : Event
        {
            public Type MainMachine;

            public Config(Type main)
            {
                MainMachine = main;
            }
        }

        [Start]
        [OnEntry(nameof(InitOnEntry))]
        private class Init : State
        {
        }
    }
}
using System.Collections.Generic;
using System.Linq;
using PChecker.Actors.Events;
using PChecker.Actors.Logging;
using PChecker.Specifications.Monitors;

namespace PChecker.PRuntime
{
    public class PMonitor : Monitor
    {
        public static List<string> observes = new List<string>();

        public object gotoPayload;

        public void TryRaiseEvent(Event ev, object payload = null)
        {
            Assert(!(ev is DefaultEvent), "Monitor cannot raise a null event");
            var oneArgConstructor = ev.GetType().GetConstructors().First(x => x.GetParameters().Length > 0);
            var @event = (Event)oneArgConstructor.Invoke(new[] { payload });
            RaiseEvent(@event);
        }

        public void TryGotoState<T>(object payload = null) where T : State
        {
            gotoPayload = payload;
            RaiseGotoStateEvent<T>();
        }

        public void TryAssert(bool predicate)
        {
            Assert(predicate);
        }

        public void TryAssert(bool predicate, string s, params object[] args)
        {
            Assert(predicate, s, args);
        }

        public void LogLine(string message)
        {
            Logger.WriteLine($"<PrintLog> {message}");

            // Log message to JSON output
            JsonLogger.AddLogType(JsonWriter.LogType.Print);
            JsonLogger.AddLog(message);
            JsonLogger.AddToLogs(updateVcMap: false);
        }

        public void Log(string message)
        {
            Logger.Write($"{message}");
        }
    }
}
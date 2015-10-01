using System;
using System.Runtime.Remoting.Messaging;

namespace T4Scaffolding.NuGetServices.Threading
{
    /// <summary>
    /// Provides a mechanism to track the depth of a call stack in terms of the number of nested "using(new CallStackDepthCounter) { ... }" statements
    /// Uses a combination of techniques to track the depth across thread/appdomain boundaries:
    ///  * Stores data using CallContext.LogicalGet/SetData to survive normal dispatches to another thread in the same appdomain
    ///  * Exposes a "Data" property that you can manually copy when using a dispatching mechanism that doesn't respect CallContext (e.g., Microsoft.VisualStudio.Shell.ThreadHelper)
    ///  * Works after dispatch to a different appdomain, because the "Data" property is a MarshalByRefObject
    /// </summary>
    internal class CallStackDepthCounter : IDisposable
    {
        private static string callContextKey = Guid.NewGuid().ToString();

        public class CallContextData : MarshalByRefObject
        {
            internal int CallStackDepth { get; set; }
        }

        public static CallContextData Data
        {
            get {
                if (CallContext.LogicalGetData(callContextKey) == null)
                    CallContext.LogicalSetData(callContextKey, new CallContextData { CallStackDepth = -1 });  // Start at negative one so the outer frame has depth zero
                return (CallContextData)CallContext.LogicalGetData(callContextKey);
            } 
            set {
                CallContext.LogicalSetData(callContextKey, value);
            }
        }        

        public CallStackDepthCounter() { Data.CallStackDepth++; }
        public void Dispose() { Data.CallStackDepth--; }

        public int Depth
        {
            get { return Data.CallStackDepth; }
        }
    }
}
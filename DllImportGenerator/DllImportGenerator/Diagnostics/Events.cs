using System;
using System.Diagnostics.Tracing;

namespace Microsoft.Interop.Diagnostics
{
    [EventSource(Name = "Microsoft-Interop-Events")]
    internal sealed class Events : EventSource
    {
        public static class Keywords
        {
            public const EventKeywords SourceGeneration = (EventKeywords)1;
        }

        public static readonly Events Logger = new Events();

        private const int StartSourceGenerationEventId = 1;
        private const int StopSourceGenerationEventId = StartSourceGenerationEventId + 1;

        private Events()
        { }

        [NonEvent]
        public static IDisposable SourceGenerationStartStop(int methodCount)
        {
            return new StartStopEvent(methodCount);
        }

        [Event(StartSourceGenerationEventId, Level = EventLevel.Informational, Keywords = Keywords.SourceGeneration)]
        public void SourceGenerationStart(int methodCount)
        {
            this.WriteEvent(StartSourceGenerationEventId, methodCount);
        }

        [Event(StopSourceGenerationEventId, Level = EventLevel.Informational, Keywords = Keywords.SourceGeneration)]
        public void SourceGenerationStop()
        {
            this.WriteEvent(StopSourceGenerationEventId);
        }

        private class StartStopEvent : IDisposable
        {
            public StartStopEvent(int methodCount) => Logger.SourceGenerationStart(methodCount);
            public void Dispose() => Logger.SourceGenerationStop();
        }
    }
}

using System;
using Prism.Events;

namespace SubtitleEditor.Common.Events
{
    public class SeekVideoEvent : PubSubEvent<TimeSpan>
    {
    }
} 
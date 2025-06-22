using System;
using Prism.Events;

namespace SubtitleEditor.Common.Events
{
    public class PlayerTimeChangedEvent : PubSubEvent<TimeSpan>
    {
    }
} 
using Prism.Events;

namespace SubtitleEditor.Common.Events
{
    public class SaveSubtitleWithOptionsEvent : PubSubEvent<SaveSubtitleOptions>
    {
    }

    public class SaveSubtitleOptions
    {
        public string FilePath { get; set; } = string.Empty;
        public bool IncludeSpeaker { get; set; }
    }
} 
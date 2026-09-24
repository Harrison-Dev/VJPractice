// Test-only shape needed by JizuraProject.FromLyricDocument.
// Unity compiles against the actual VJPractice.Stage.LyricDocument.
using System.Collections.Generic;
namespace VJPractice.Stage
{
    public sealed class LyricCue
    {
        public string text = "", translation = "";
        public float startTime = -1;
    }
    public sealed class LyricDocument
    {
        public string title = "", artist = "";
        public float offsetSeconds;
        public List<LyricCue> lines = new List<LyricCue>();
    }
}

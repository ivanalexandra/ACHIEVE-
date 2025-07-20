namespace Achieve_Plus.Models
{
    public class LessonProgressDto
    {
        public int lessonID { get; set; }
        public byte progress { get; set; }
        public int timeSpent { get; set; }
        public bool finished { get; set; }
    }
}

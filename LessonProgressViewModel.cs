namespace Achieve_Plus.Models
{
    public class LessonProgressViewModel
    {
        public int LessonID { get; set; }
        public string LessonTitle { get; set; }
        public string LessonContent { get; set; }
        public byte Progress { get; set; }
        public string Status { get; set; }
        public int Time_Spent { get; set; }
        public DateTime? Start_Date { get; set; }
        public DateTime? Finish_Date { get; set; }
        public int Attempts { get; set; }
        public DateTime? Last_Accessed { get; set; }
        public int ModuleID { get; set; }

        public List<string> Tags { get; set; } = new List<string>();
        public string ModuleName { get; set; }
        public bool IsTeacher { get; set; }


    }
}

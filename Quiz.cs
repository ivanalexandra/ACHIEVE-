using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Achieve_Plus.Models;
using NuGet.Protocol.Plugins;

namespace Achieve_Plus.Models
{
    [Table("QUIZZES")]
    public class Quiz
    {
        [Key]
        public int quizID { get; set; }
        [Required]
        public int lessonID { get; set; }
        [Required]
        [StringLength(200)]
        public string title { get; set; }
        public string description { get; set; }
        public int? duration_minutes { get; set; }
        public DateTime? created_at { get; set; }
        public string? quiz_type { get; set; } // regular / midterm / final

        [ForeignKey("lessonID")]
        public virtual Lesson Lesson { get; set; }
        public virtual ICollection<QuizItem> QuizItems { get; set; }
        public virtual QuizResult QuizResult { get; set; }

    }
}
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.Identity.Client;

namespace Achieve_Plus.Models
{

    [Table("LESSONS")]
    public class Lesson
    {
        [Key]
        public int lessonID { get; set; }

        [Required]
        public int moduleID { get; set; }

        [Required]
        [StringLength(255)]
        public string lesson_title { get; set; }
        public string lesson_content { get; set; }

        public DateTime created_at { get; set; }

        [ValidateNever]
        [ForeignKey("moduleID")]
        public virtual Module Module { get; set; }

        [ValidateNever]
        public virtual ICollection<LessonsUsers> LessonsUsers { get; set; }
        [ValidateNever]
        public virtual ICollection<LessonTag> LessonTags { get; set; }
        public virtual ICollection<Quiz> Quizzes { get; set; }
        public virtual ICollection<Assignment> Assignments { get; set; }
        
    }
}

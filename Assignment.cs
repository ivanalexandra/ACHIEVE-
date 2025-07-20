using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Achieve_Plus.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation; 

namespace Achieve_Plus.Models
{
    [Table("ASSIGNMENTS")]
    public class Assignment
    {
        [Key]
        public int assignmentID { get; set; }
        [StringLength(255)]
        public string title { get; set; }
        public string description { get; set; }
        public DateTime? start_date { get; set; }
        public DateTime? due_date { get; set; }

        [Required]
        public int max_score { get; set; }
        [ValidateNever]
        public int lessonID { get; set; }
        [ValidateNever]
        [ForeignKey("lessonID")]
        public virtual Lesson Lesson { get; set; }
        [ValidateNever]
        public virtual ICollection<UserSubmission> Submissions { get; set; }


    }
}
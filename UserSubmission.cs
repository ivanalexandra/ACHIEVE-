using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Achieve_Plus.Models;

namespace Achieve_Plus.Models
{
    [Table("USER_SUBMISSIONS")]
    public class UserSubmission
    {
        [Key]
        public int submissionID { get; set; }
        [Required]
        public int userID { get; set; }
        [Required]
        public int assignmentID { get; set; }
        public DateTime? submission_date { get; set; }
        [Range(0, 100)]
        public decimal? grade { get; set; }
        [StringLength(500)]
        public string? file_path { get; set; }
        public string? comments { get; set; }

        [StringLength(20)]
        [RegularExpression("pending|graded|late", ErrorMessage = "Status must be 'pending', 'graded', or 'late'. ")]
        public string? status { get; set; } = "pending";
        [ForeignKey("userID")]
        public virtual User User { get; set; }
        [ForeignKey("assignmentID")]
        public virtual Assignment Assignment { get; set; }

    }
}
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Achieve_Plus.Models
{
    [Table("QUIZ_RESULTS")]
    public class QuizResult
    {
        [Key]
        public int resultID { get; set; }
        [Required]
        public int userID { get; set; }
        [Required]
        public int quizID { get; set; }
        [Required]
        public int itemID { get; set; }
        public bool? is_correct { get; set; }
        public DateTime? attempt_date { get; set; }
        [ForeignKey("userID")]
        public virtual User User { get; set; }
        [ForeignKey("quizID")]
        public virtual Quiz Quiz { get; set; }
        [ForeignKey("itemID")]
        public virtual Item Item { get; set; }
    }
}
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Achieve_Plus.Models
{
    [Table("QUIZ_ITEMS")]
    public class QuizItem
    {
        [Key, Column(Order = 0)]
        public int quizID { get; set; }
        [Key, Column(Order = 1)]
        public int itemID { get; set; }
        [Required]
        public int position { get; set; }
        [ForeignKey("quizID")]
        public virtual Quiz Quiz { get; set; }
        [ForeignKey("itemID")]
        public virtual Item Item { get; set; }
    }
}
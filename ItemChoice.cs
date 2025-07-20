using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Achieve_Plus.Models
{
    [Table("ITEMCHOICES")]
    public class ItemChoice
    {
        [Key]
        public int itemchoicesID { get; set; }
        [Required]
        public int itemID { get; set; }
        [Required]
        public string content { get; set; }
        [Required]
        public bool is_correct { get; set; } = false;
        [ForeignKey("itemID")]
        public virtual Item Item { get; set; }
    }
}
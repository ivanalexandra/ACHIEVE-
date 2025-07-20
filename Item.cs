using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Achieve_Plus.Models
{
    [Table("ITEMS")]
    public class Item
    {
        [Key]
        public int itemID { get; set; }
        [Required]
        public string content { get; set; }
        [Required]
        [StringLength(50)]
        public string type { get; set; }
        public int? difficulty { get; set; }
        public DateTime created_at { get; set; }
        public virtual ICollection<ItemChoice> ItemChoices { get; set; }
    }
}
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Achieve_Plus.Models
{
    [Table("ITEM_TAGS")]
    public class ItemTag
    {
        [Key, Column(Order = 0)]
        public int itemID { get; set; }
        [Key, Column(Order = 1)]
        public int tagID { get; set; }
        [ForeignKey("itemID")]
        public virtual Item Item { get; set; }
        [ForeignKey("tagID")]
        public virtual Tag Tag { get; set; }
    }
}
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Achieve_Plus.Models
{
    [Table("TAGS")]
    public class Tag
    {
        [Key]
        public int tagID { get; set; }
        [Required]
        [StringLength(255)]
        public string name { get; set; }

        public virtual ICollection<LessonTag> LessonTags { get; set; }
    }
}
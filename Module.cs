using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Achieve_Plus.Models; 

namespace Achieve_Plus.Models
{
    public class Module
    {
        [Key]
        public int moduleID { get; set; }

        [Required]
        [StringLength(255)]
        public string module_name { get; set; }
        public string module_description { get; set; }

        public DateTime created_at { get; set; }
        public virtual ICollection<Lesson> Lessons { get; set; }

    }
}
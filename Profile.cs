using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Achieve_Plus.Models
{
    [Table("PROFILES")]
    public class Profile
    {
        [Key]
        public int profileID { get; set; }
        [Required]
        public int userID { get; set; }
        [StringLength(100)]
        public string full_name { get; set; }
        [StringLength(500)]
        public string avatar_url { get; set; }
        [ForeignKey("userID")]
        public virtual User User { get; set; }
    }
}
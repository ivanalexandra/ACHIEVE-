using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema; 
namespace Achieve_Plus.Models
{
    public class Badge
    {
        [Key]
        public int badgeID { get; set; }
        [StringLength(100)]
        public string name { get; set; }
        [StringLength(500)]
        public string description { get; set; }
        [StringLength(255)]
        public string? icon_path { get; set; }
        public DateTime? created_at { get; set; }
    }
}
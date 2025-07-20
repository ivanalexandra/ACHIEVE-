using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Achieve_Plus.Models
{
    [Table("USER_BADGES")]
    public class UserBadge
    {
        [Key, Column(Order = 0)]
        public int userID { get; set; }
        [Key, Column(Order = 1)]
        public int badgeID { get; set; }
        public DateTime? awarded_at { get; set; }
        [ForeignKey("userID")]
        public virtual User User { get; set; }
        [ForeignKey("badgeID")]
        public virtual Badge Badge { get; set; }

    }
}
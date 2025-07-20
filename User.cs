using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Achieve_Plus.Models
{
    public class User
    {
        [Key]
        public int userID { get; set; } //cheia primara

        [Required]
        [StringLength(50)]
        public string username { get; set; }

        [Required]
        [StringLength(255)]
        public string password_hash { get; set; }

        [Required]
        [EmailAddress] //trebuie sa respecte formatul de email
        [StringLength(255)]
        public string email { get; set; }

        [Required]
        [RegularExpression("Student|Profesor", ErrorMessage = "Rolul trebuie să fie student sau profesor")]
        //trebuie sa fie exact student sau profesor, validare pe baza unui regex
        public string role { get; set; }

        public DateTime created_at { get; set; }

        public DateTime? last_login { get; set; }
        [ValidateNever]
        public int LoginCount { get; set; }
        [ValidateNever]
        public virtual ICollection<LessonsUsers> LessonsUsers { get; set; }
        [ValidateNever]
        public virtual Profile Profile { get; set; } // relatie 1-la-1
        [ValidateNever]
        public virtual ICollection<QuizResult> QuizResults { get; set; }
        [ValidateNever]
        public virtual ICollection<UserSubmission> UserSubmissions { get; set; }
        [ValidateNever]
        public virtual ICollection<UserBadge> UserBadges { get; set; }

    }
}
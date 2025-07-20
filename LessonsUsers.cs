using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Achieve_Plus.Models; 

[Table("LESSONS_USERS")]
public class LessonsUsers {
    [Key]
    public int lessons_users_ID {get; set;}

    [Required]
    public int userID {get; set;}

    [Required]
    public int lessonID {get; set;}

    public DateTime? start_date {get; set;}
    public DateTime? finish_date {get; set;}
    [Range(0,100)]
    public byte progress {get; set;} = 0;
    public string status {get; set;} = "not_started";
    public DateTime? last_accessed {get; set;}
    public int time_spent {get; set;} = 0;
    public int attempts {get; set;} = 0;
    [ForeignKey("userID")]
    public virtual User User {get; set;}
    [ForeignKey("lessonID")]
    public virtual Lesson Lesson {get; set;}


}
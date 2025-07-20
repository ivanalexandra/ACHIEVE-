using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Achieve_Plus.Models;

[Table("LESSON_TAGS")]
public class LessonTag
{
    [Key, Column(Order = 0)]
    public int lessonID { get; set; }
    [Key, Column(Order = 1)]
    public int tagID { get; set; }
    [ForeignKey("lessonID")]
    public virtual Lesson Lesson { get; set; }
    [ForeignKey("tagID")]
    public virtual Tag Tag { get; set; }
}
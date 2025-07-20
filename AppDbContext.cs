using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;




namespace Achieve_Plus.Models
{

    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> User { get; set; }
        public DbSet<Module> Modules { get; set; }
        public DbSet<Lesson> Lesson { get; set; }
        public DbSet<LessonsUsers> LessonsUsers { get; set; }
        public DbSet<Assignment> Assignments { get; set; }
        public DbSet<Profile> Profile { get; set; }
        public DbSet<UserBadge> UserBadges { get; set; }
        public DbSet<UserSubmission> UserSubmissions { get; set; }
        public DbSet<Quiz> Quizzes { get; set; }
        public DbSet<LessonTag> LessonTags { get; set; }
        public DbSet<Tag> Tags { get; set; }
        public DbSet<QuizItem> QuizItems { get; set; }
        public DbSet<QuizResult> QuizResults { get; set; }
        public DbSet<Badge> Badges { get; set; }
        public DbSet<ItemChoice> ItemChoices { get; set; }
        public DbSet<Item> Items { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<User>().ToTable("USERS", "dbo");
            modelBuilder.Entity<LessonsUsers>().ToTable("LESSONS_USERS");
            modelBuilder.Entity<UserBadge>()
                .HasKey(ub => new { ub.userID, ub.badgeID }); //cheie compusa
            modelBuilder.Entity<LessonTag>()
                .HasKey(lt => new { lt.lessonID, lt.tagID });
            modelBuilder.Entity<QuizResult>()
                .HasOne<QuizItem>()
                .WithMany()
                .HasForeignKey(qr => new { qr.quizID, qr.itemID });
            modelBuilder.Entity<QuizItem>()
                .HasKey(qi => new { qi.quizID, qi.itemID });
            modelBuilder.Entity<ItemTag>()
                .HasKey(it => new { it.itemID, it.tagID });
            modelBuilder.Entity<UserSubmission>()
        .Property(us => us.grade)
        .HasPrecision(5, 2); // decimal(5,2) => Ex: 99.99

        }         
    }
}

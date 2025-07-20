using Achieve_Plus.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Achieve_Plus.Services
{
    public class BadgeService
    {
        private readonly AppDbContext _context;

        public BadgeService(AppDbContext context)
        {
            _context = context;
        }

        public async Task CheckAndAwardBadges(int userId)
        {
            await CheckDedicatedBeginner(userId);
            await CheckKnowledgeExplorer(userId);
            await CheckQuizMaster(userId);
            await CheckModelStudent(userId);
            await CheckAcademicPerseverance(userId);
            await CheckLightSpeed(userId);

        }

        private async Task CheckDedicatedBeginner(int userId)
        {
            const int badgeId = 7;

            if (await HasBadge(userId, badgeId)) return;

            var completedLessons = await _context.LessonsUsers
                .Where(lu => lu.userID == userId && lu.status == "completed")
                .CountAsync();

            if (completedLessons >= 5)
            {
                await AwardBadge(userId, badgeId);
            }
        }

        private async Task CheckKnowledgeExplorer(int userId)
        {

            const int badgeId = 8;

            if (await HasBadge(userId, badgeId)) return;

            var allCategories = await _context.Tags.Select(t => t.tagID).ToListAsync();
            var userCategories = await _context.LessonsUsers
                .Where(lu => lu.userID == userId && lu.status == "completed")
                .Join(_context.LessonTags,
                    lu => lu.lessonID,
                    lt => lt.lessonID,
                    (lu, lt) => lt.tagID)
                .Distinct()
                .ToListAsync();

            if (allCategories.All(c => userCategories.Contains(c)))
            {
                await AwardBadge(userId, badgeId);
            }
        }

        private async Task CheckQuizMaster(int userId)
        {

            const int badgeId = 9;

            if (await HasBadge(userId, badgeId)) return;

            var quizAttempts = await _context.QuizResults
                .Where(qr => qr.userID == userId)
                .GroupBy(qr => qr.quizID)
                .Select(g => new
                {
                    QuizId = g.Key,
                    AttemptDate = g.Max(qr => qr.attempt_date),
                    IsCorrect = g.All(qr => qr.is_correct == true)
                })
                .OrderBy(a => a.AttemptDate)
                .ToListAsync();

            int consecutiveCorrect = 0;
            foreach (var attempt in quizAttempts)
            {
                if (attempt.IsCorrect)
                {
                    consecutiveCorrect++;
                    if (consecutiveCorrect >= 10)
                    {
                        await AwardBadge(userId, badgeId);
                        break;
                    }
                }
                else
                {
                    consecutiveCorrect = 0;
                }
            }
        }

        private async Task CheckModelStudent(int userId)
        {

            const int badgeId = 10;

            if (await HasBadge(userId, badgeId)) return;

            var oneMonthAgo = DateTime.Now.AddMonths(-1);
            var quizResults = await _context.QuizResults
                .Where(qr => qr.userID == userId && qr.attempt_date >= oneMonthAgo)
                .GroupBy(qr => qr.quizID)
                .Select(g => new
                {
                    CorrectAnswers = g.Count(qr => qr.is_correct == true),
                    TotalAnswers = g.Count()
                })
                .ToListAsync();

            if (quizResults.Count > 0)
            {
                var totalCorrect = quizResults.Sum(q => q.CorrectAnswers);
                var totalQuestions = quizResults.Sum(q => q.TotalAnswers);
                var averageScore = (totalQuestions > 0) ? (totalCorrect * 100.0 / totalQuestions) : 0;

                if (averageScore >= 85)
                {
                    await AwardBadge(userId, badgeId);
                }
            }
        }

        private async Task CheckAcademicPerseverance(int userId)
        {

            const int badgeId = 11;

            if (await HasBadge(userId, badgeId)) return;

            var studyDates = await _context.LessonsUsers
                .Where(lu => lu.userID == userId && lu.status == "completed")
                .Select(lu => lu.finish_date.Value.Date)
                .Distinct()
                .OrderBy(d => d)
                .ToListAsync();

            int consecutiveDays = 1;
            for (int i = 1; i < studyDates.Count; i++)
            {
                if ((studyDates[i] - studyDates[i - 1]).TotalDays == 1)
                {
                    consecutiveDays++;
                    if (consecutiveDays >= 10)
                    {
                        await AwardBadge(userId, badgeId);
                        break;
                    }
                }
                else
                {
                    consecutiveDays = 1;
                }
            }
        }

        private async Task CheckLightSpeed(int userId)
        {

            const int badgeId = 12;

            if (await HasBadge(userId, badgeId)) return;

            var fastLessons = _context.LessonsUsers
                .Where(lu => lu.userID == userId &&
                             lu.status == "completed" &&
                             lu.finish_date.HasValue &&
                             lu.start_date.HasValue)
                .AsEnumerable() 
                .Any(lu => (lu.finish_date.Value - lu.start_date.Value).TotalMinutes < 30); 

            if (fastLessons)
            {
                await AwardBadge(userId, badgeId);
            }
        }


        private async Task<bool> HasBadge(int userId, int badgeId)
        {
            return await _context.UserBadges
                .AnyAsync(ub => ub.userID == userId && ub.badgeID == badgeId);
        }

        private async Task AwardBadge(int userId, int badgeId)
        {
            if (!await HasBadge(userId, badgeId))
            {
                _context.UserBadges.Add(new UserBadge
                {
                    userID = userId,
                    badgeID = badgeId,
                    awarded_at = DateTime.Now
                });
                await _context.SaveChangesAsync();
            }
        }
    }
    
}
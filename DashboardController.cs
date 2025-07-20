using Achieve_Plus.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;

namespace Achieve_Plus.Controllers
{
    public class DashboardController : Controller
    {
        private readonly AppDbContext _context;

        public DashboardController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Dashboard()
        {
            var username = HttpContext.Session.GetString("username");
            if (string.IsNullOrEmpty(username))
            {
                Console.WriteLine("User not logged in. Redirecting to Login.");
                return RedirectToAction("Login", "Account");
            }

            var user = await _context.User
                .Include(u => u.Profile)
                .FirstOrDefaultAsync(u => u.username == username);

            if (user == null)
            {
                Console.WriteLine($"User '{username}' not found in database. Redirecting to Login.");
                return RedirectToAction("Login", "Account");
            }

            ViewBag.Username = user.username;
            ViewBag.AvatarUrl = !string.IsNullOrEmpty(user.Profile?.avatar_url)
                ? user.Profile.avatar_url
                : "/images/default-avatar.png";

            var startedLessonsUsers = await _context.LessonsUsers
                .Include(lu => lu.Lesson)
                    .ThenInclude(l => l.LessonTags)
                    .ThenInclude(lt => lt.Tag)
                .Where(lu => lu.userID == user.userID)
                .ToListAsync();

            Console.WriteLine($"User {username} has started {startedLessonsUsers.Count} lessons.");

            int totalLessons = startedLessonsUsers.Count;
            int completedLessons = startedLessonsUsers.Count(lu => lu.progress == 100);
            double averageProgress = totalLessons > 0 ? startedLessonsUsers.Average(lu => lu.progress) : 0;

            ViewBag.TotalLessons = totalLessons;
            ViewBag.CompletedLessons = completedLessons;
            ViewBag.GeneralProgress = (int)Math.Round(averageProgress);

            int quizPassThreshold = 1;

            var startedLessonIds = startedLessonsUsers.Select(lu => lu.lessonID).ToList();

            int totalQuizzes = await _context.Quizzes
                .CountAsync(q => startedLessonIds.Contains(q.lessonID));

            var userQuizResults = await _context.QuizResults
                .Include(qr => qr.Quiz)
                .Where(qr => qr.userID == user.userID && startedLessonIds.Contains(qr.Quiz.lessonID))
                .ToListAsync();

            var quizzesGrouped = userQuizResults
                .GroupBy(qr => qr.quizID)
                .ToList();

            int passedQuizzes = 0;

            foreach (var quizGroup in quizzesGrouped)
            {
                int correctAnswers = quizGroup.Count(qr => qr.is_correct == true);

                if (correctAnswers >= quizPassThreshold)
                {
                    passedQuizzes++;
                }
            }

            ViewBag.TotalQuizzes = totalQuizzes;
            ViewBag.PassedQuizzes = passedQuizzes;

            var allTags = await _context.Tags.Select(t => t.tagID).ToListAsync();
            var tagIndexMap = allTags.Select((tagId, index) => new { tagId, index })
                                     .ToDictionary(x => x.tagId, x => x.index);

            int vectorLength = allTags.Count;
            var userVector = new double[vectorLength];

            foreach (var lu in startedLessonsUsers)
            {
                if (lu.progress > 0)
                {
                    foreach (var lt in lu.Lesson.LessonTags)
                    {
                        if (lt.Tag != null && tagIndexMap.TryGetValue(lt.Tag.tagID, out int idx))
                        {
                            userVector[idx] += 1;
                        }
                    }
                }
            }

            Normalize(userVector);


            var startedLessonIDs = startedLessonsUsers.Select(lu => lu.lessonID).ToHashSet();

            var unstartedLessons = await _context.Lesson
                .Include(l => l.LessonTags)
                    .ThenInclude(lt => lt.Tag)
                .Where(l => !startedLessonIDs.Contains(l.lessonID))
                .ToListAsync();

            var recommendations = new List<(Lesson lesson, double score)>();

            foreach (var lesson in unstartedLessons)
            {
                var lessonVector = new double[vectorLength];

                foreach (var lt in lesson.LessonTags)
                {
                    if (lt.Tag != null && tagIndexMap.TryGetValue(lt.Tag.tagID, out int idx))
                    {
                        lessonVector[idx] = 1;
                    }
                }

                Normalize(lessonVector);
                var similarity = CosineSimilarity(userVector, lessonVector);

                if (similarity > 0)
                {
                    recommendations.Add((lesson, similarity));
                }
            }

            List<LessonProgressViewModel> topRecommendations;

            if (recommendations.Count > 0)
            {
                topRecommendations = recommendations
                    .OrderByDescending(r => r.score)
                    .Take(5)
                    .Select(r => new LessonProgressViewModel
                    {
                        LessonID = r.lesson.lessonID,
                        LessonTitle = r.lesson.lesson_title,
                        LessonContent = r.lesson.lesson_content,
                        Progress = 0,
                        Status = "not_started",
                        Tags = r.lesson.LessonTags
                            .Where(lt => lt.Tag != null)
                            .Select(lt => lt.Tag.name)
                            .ToList(),
                        ModuleID = r.lesson.moduleID
                    })
                    .ToList();
            }
            else
            {
                topRecommendations = unstartedLessons
                    .Take(5)
                    .Select(l => new LessonProgressViewModel
                    {
                        LessonID = l.lessonID,
                        LessonTitle = l.lesson_title,
                        LessonContent = l.lesson_content,
                        Progress = 0,
                        Status = "not_started",
                        Tags = l.LessonTags
                            .Where(lt => lt.Tag != null)
                            .Select(lt => lt.Tag.name)
                            .ToList(),
                        ModuleID = l.moduleID
                    })
                    .ToList();
            }

            ViewBag.RecommendedLessons = topRecommendations;

            var startedLessonModels = startedLessonsUsers
                .OrderByDescending(lu => lu.last_accessed)
                .Take(4)
                .Select(lu => new LessonProgressViewModel
                {
                    LessonID = lu.lessonID,
                    LessonTitle = lu.Lesson.lesson_title,
                    LessonContent = lu.Lesson.lesson_content,
                    Progress = lu.progress,
                    Status = lu.status,
                    Time_Spent = lu.time_spent,
                    Start_Date = lu.start_date,
                    Finish_Date = lu.finish_date,
                    Attempts = lu.attempts,
                    Last_Accessed = lu.last_accessed,
                    ModuleID = lu.Lesson.moduleID,
                    Tags = lu.Lesson.LessonTags
                        .Where(lt => lt.Tag != null)
                        .Select(lt => lt.Tag.name)
                        .ToList()
                }).ToList();

            return View(startedLessonModels);
        }

        [HttpGet]
        public async Task<IActionResult> Search(string query)
        {
            if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            {
                return Json(new List<object>());
            }

            var searchResults = new List<object>();
            var normalizedQuery = query.ToLower();

            var lessons = await _context.Lesson
                .Where(l => l.lesson_title.ToLower().Contains(normalizedQuery) || 
                   l.lesson_content.ToLower().Contains(normalizedQuery))
                .Take(5)
                .Select(l => new
                {
                    id = l.lessonID,
                    title = l.lesson_title,
                    type = "lesson",
                    url = Url.Action("Lesson", "Lessons", new { id = l.lessonID })
                })
                .ToListAsync();

            searchResults.AddRange(lessons);

            var quizzes = await _context.Quizzes
                .Where(q => q.title.ToLower().Contains(normalizedQuery) || 
                   q.description.ToLower().Contains(normalizedQuery))
                .Take(5)
                .Select(q => new
                {
                    id = q.quizID,
                    title = q.title,
                    type = "quiz",
                    url = Url.Action("Quiz", "Quizzes", new { id = q.quizID })
                })
                .ToListAsync();

            searchResults.AddRange(quizzes);

            var modules = await _context.Modules
                .Where(m => m.module_name.ToLower().Contains(normalizedQuery) || 
                   m.module_description.ToLower().Contains(normalizedQuery))
                .Take(5)
                .Select(m => new
                {
                    id = m.moduleID,
                    title = m.module_name,
                    type = "module",
                    url = Url.Action("Module", "Modules", new { id = m.moduleID })
                })
                .ToListAsync();

            searchResults.AddRange(modules);

            return Json(searchResults);
        }

        public async Task<IActionResult> TeacherDashboard()
        {
            var username = HttpContext.Session.GetString("username");
            if (string.IsNullOrEmpty(username))
            {
                return RedirectToAction("Login", "Account");
            }

            var user = await _context.User
                .Include(u => u.Profile)
                .FirstOrDefaultAsync(u => u.username == username);

            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            ViewBag.Username = user.username;
            ViewBag.AvatarUrl = !string.IsNullOrEmpty(user.Profile?.avatar_url)
                ? user.Profile.avatar_url
                : "/images/default-avatar.png";

            var allLessons = await _context.Lesson
                .Include(l => l.LessonTags)
                    .ThenInclude(lt => lt.Tag)
                .OrderByDescending(l => l.created_at)
                .Take(5)
                .ToListAsync();

            var availableQuizzes = await _context.Quizzes
                .OrderByDescending(q => q.created_at)
                .Take(5)
                .ToListAsync();

            var availableModules = await _context.Modules
                .OrderByDescending(m => m.created_at)
                .Take(5)
                .ToListAsync();

            var recentSubmissions = await _context.UserSubmissions
                .Include(s => s.Assignment)
                .Include(s => s.User)
                    .ThenInclude(u => u.Profile) 
                .OrderByDescending(s => s.submission_date)
                .Take(5)
                .Select(s => new 
                {
                    SubmissionID = s.submissionID,
                    User = s.User,
                    Assignment = s.Assignment,
                    SubmissionDate = s.submission_date,
                    Grade = s.grade,
                    Status = s.status,
                    FilePath = s.file_path,
                    Comments = s.comments
                })
                .ToListAsync();

            ViewBag.AllLessons = allLessons;
            ViewBag.AvailableQuizzes = availableQuizzes;
            ViewBag.AvailableModules = availableModules;
            ViewBag.RecentSubmissions = recentSubmissions;

            return View();
        }

        private void Normalize(double[] vector)
        {
            var norm = Math.Sqrt(vector.Sum(x => x * x));
            if (norm > 0)
            {
                for (int i = 0; i < vector.Length; i++)
                {
                    vector[i] /= norm;
                }
            }
        }

        private double CosineSimilarity(double[] vec1, double[] vec2)
        {
            double dotProduct = 0;
            double normA = 0;
            double normB = 0;

            for (int i = 0; i < vec1.Length; i++)
            {
                dotProduct += vec1[i] * vec2[i];
                normA += vec1[i] * vec1[i];
                normB += vec2[i] * vec2[i];
            }

            if (normA == 0 || normB == 0)
                return 0;

            return dotProduct / (Math.Sqrt(normA) * Math.Sqrt(normB));
        }
    }
}
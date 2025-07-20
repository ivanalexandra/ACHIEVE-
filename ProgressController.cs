using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Achieve_Plus.Models;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Text;

namespace Achieve_Plus.Controllers
{
    public class ProgressController : Controller
    {
        private readonly AppDbContext _context;
        private readonly HttpClient _httpClient;
        private readonly ILogger<ProgressController> _logger;

        public ProgressController(AppDbContext context,
                               IHttpClientFactory httpClientFactory,
                               ILogger<ProgressController> logger)
        {
            _context = context;
            _httpClient = httpClientFactory.CreateClient();
            _logger = logger;
        }

        // GET: /Progress/Predict
        public async Task<IActionResult> Predicts()
        {
            try
            {
                _logger.LogInformation("Starting Predicts action");

                var username = HttpContext.Session.GetString("username");
                if (string.IsNullOrEmpty(username))
                {
                    _logger.LogWarning("User not authenticated");
                    return Unauthorized("User not authenticated or session expired.");
                }

                var user = await _context.User
                    .Include(u => u.Profile)
                    .FirstOrDefaultAsync(u => u.username == username);

                if (user == null)
                {
                    _logger.LogWarning($"User not found: {username}");
                    return NotFound("User not found.");
                }

                _logger.LogInformation($"Processing predictions for user: {user.userID}");

                ViewBag.AvatarUrl = user.Profile?.avatar_url ?? "https://randomuser.me/api/portraits/lego/1.jpg";
                ViewBag.Username = user.username;
                ViewBag.CurrentUserId = user.userID;

                int userId = user.userID;

                var features = await BuildFeaturesFromDb(userId);
                if (features == null || features.Count == 0)
                {
                    _logger.LogError("Failed to build features for prediction");
                    return StatusCode(500, "Failed to gather prediction data");
                }

                var predictionRequest = new { features = features };

                HttpResponseMessage response;
                try
                {
                    _logger.LogInformation("Sending prediction request to Flask API");
                    response = await _httpClient.PostAsJsonAsync("http://localhost:8000/predict", predictionRequest);

                    if (response == null)
                    {
                        _logger.LogError("No response from prediction service");
                        throw new Exception("Prediction service unavailable");
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        _logger.LogError($"Prediction service error: {response.StatusCode} - {errorContent}");
                        throw new Exception($"Prediction service error: {response.StatusCode}");
                    }
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogError($"HTTP Request failed: {ex.Message}");
                    return StatusCode(503, "Prediction service unavailable");
                }

                var prediction = await response.Content.ReadFromJsonAsync<PredictionResponse>();
                if (prediction == null)
                {
                    _logger.LogError("Invalid prediction response format");
                    return StatusCode(500, "Invalid prediction response");
                }

                var modulePredictions = await PredictModuleGrades(userId);
                if (modulePredictions == null)
                {
                    _logger.LogError("Failed to generate module predictions");
                    modulePredictions = new Dictionary<string, double>();
                }

                var progressStats = await GetProgressStats(userId);
                if (progressStats == null)
                {
                    _logger.LogError("Failed to gather progress statistics");
                    progressStats = new ProgressStats();
                }

                var model = new ProgressViewModel
                {
                    Prediction = prediction,
                    ModulePredictions = modulePredictions,
                    LessonsCompleted = progressStats.LessonsCompleted,
                    QuizzesPassed = progressStats.QuizzesPassed,
                    AverageScore = progressStats.AverageScore,
                    TotalStudyTime = progressStats.TotalStudyTime,
                    ProgressOverTime = progressStats.ProgressOverTime,
                    EarnedBadges = progressStats.EarnedBadges
                };

                _logger.LogInformation("Successfully generated predictions");
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Predicts action");
                return StatusCode(500, "An error occurred while processing your request");
            }
        }

        private async Task<List<object>> BuildFeaturesFromDb(int userId)
        {
            _logger.LogInformation($"Building features for user {userId}");
            var features = new List<object>();
            const double threshold = 100.0;

            try
            {
                int loginCount = await _context.User
                    .Where(u => u.userID == userId)
                    .Select(u => u.LoginCount)
                    .FirstOrDefaultAsync();
                features.Add((double)loginCount);

                double totalAttempts = await _context.LessonsUsers
                    .Where(lu => lu.userID == userId)
                    .SumAsync(lu => lu.attempts);
                features.Add(totalAttempts);

                var avgQuizAttempts = await _context.QuizResults
                    .Where(qr => qr.userID == userId)
                    .GroupBy(qr => qr.quizID)
                    .Select(g => (double)g.Count())
                    .DefaultIfEmpty()
                    .AverageAsync();
                features.Add(avgQuizAttempts);

                for (int i = 16; i <= 18; i++)
                {
                    var assignment = await _context.Assignments
                        .FirstOrDefaultAsync(a => a.assignmentID == i);

                    var submission = await _context.UserSubmissions
                        .Where(us => us.userID == userId && us.assignmentID == i)
                        .OrderBy(us => us.submission_date)
                        .FirstOrDefaultAsync();

                    double lateness = 0;
                    if (assignment != null && submission != null &&
                        assignment.due_date.HasValue && submission.submission_date.HasValue)
                    {
                        lateness = submission.submission_date.Value > assignment.due_date.Value ? 1 : 0;
                    }
                    features.Add(lateness);
                }

                for (int i = 16; i <= 18; i++)
                {
                    var assignment = await _context.Assignments
                        .FirstOrDefaultAsync(a => a.assignmentID == i);

                    var submission = await _context.UserSubmissions
                        .Where(us => us.userID == userId && us.assignmentID == i)
                        .OrderBy(us => us.submission_date)
                        .FirstOrDefaultAsync();

                    double duration = 0;
                    if (assignment != null && submission != null &&
                        assignment.start_date.HasValue && submission.submission_date.HasValue)
                    {
                        duration = (submission.submission_date.Value - assignment.start_date.Value).TotalHours;
                    }
                    features.Add(duration);
                }

                var durations = features.Skip(6).Take(3).Cast<double>().ToList();
                double avgDuration = durations.Any() ? durations.Average() : 0;
                features.Add(avgDuration);

                double totalTimeSpent = await _context.LessonsUsers
                    .Where(lu => lu.userID == userId)
                    .SumAsync(lu => lu.time_spent);
                double engagementScore = 0.4 * totalAttempts + 0.4 * totalTimeSpent + 0.2 * loginCount;
                string engagementLevelHL = engagementScore >= threshold ? "H" : "L";
                features.Add(engagementLevelHL);

                var quizScore = await _context.QuizResults
                    .Include(qr => qr.Quiz)
                    .Where(qr => qr.userID == userId && qr.Quiz != null && qr.Quiz.quiz_type.ToLower() == "regular")
                    .GroupBy(qr => qr.quizID)
                    .Select(g => (double)g.Count(qr => qr.is_correct == true) / g.Count() * 100)
                    .FirstOrDefaultAsync();
                features.Add(quizScore);

                for (int i = 16; i <= 18; i++)
                {
                    var grade = await _context.UserSubmissions
                        .Where(us => us.userID == userId && us.assignmentID == i && us.grade.HasValue)
                        .OrderByDescending(us => us.submission_date)
                        .Select(us => (double)us.grade.Value)
                        .FirstOrDefaultAsync();
                    features.Add(grade);
                }

                var midtermExamScore = await _context.QuizResults
                    .Include(qr => qr.Quiz)
                    .Where(qr => qr.userID == userId && qr.Quiz != null && qr.Quiz.quiz_type.ToLower() == "midterm")
                    .GroupBy(qr => qr.quizID)
                    .Select(g => (double)g.Count(qr => qr.is_correct == true) / g.Count() * 100)
                    .FirstOrDefaultAsync();
                features.Add(midtermExamScore);

                var finalExamScore = await _context.QuizResults
                    .Include(qr => qr.Quiz)
                    .Where(qr => qr.userID == userId && qr.Quiz != null && qr.Quiz.quiz_type.ToLower() == "exam")
                    .GroupBy(qr => qr.quizID)
                    .Select(g => (double)g.Count(qr => qr.is_correct == true) / g.Count() * 100)
                    .FirstOrDefaultAsync();
                features.Add(finalExamScore);

                _logger.LogInformation($"Successfully built {features.Count} features");
                return features;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error building features from database");
                return new List<object>();
            }
        }

        private async Task<Dictionary<string, double>> PredictModuleGrades(int userId)
        {
            _logger.LogInformation($"Predicting module grades for user {userId}");
            var modulePredictions = new Dictionary<string, double>();

            try
            {

                var modules = await _context.Modules.ToListAsync();
                if (modules == null || !modules.Any())
                {
                    _logger.LogWarning("No modules found in database");
                    return modulePredictions;
                }

                foreach (var module in modules)
                {
                    _logger.LogInformation($"Processing module: {module.module_name}");

                    var lessons = await _context.Lesson
                        .Where(l => l.moduleID == module.moduleID)
                        .ToListAsync();

                    if (lessons == null || !lessons.Any())
                    {
                        _logger.LogWarning($"No lessons found for module {module.module_name}");
                        modulePredictions.Add(module.module_name, 0);
                        continue;
                    }

                    var quizzes = await _context.Quizzes
                        .Include(q => q.QuizItems)
                        .Where(q => lessons.Select(l => l.lessonID).Contains(q.lessonID))
                        .ToListAsync();

                    var assignments = await _context.Assignments
                        .Include(a => a.Submissions)
                        .Where(a => lessons.Select(l => l.lessonID).Contains(a.lessonID))
                        .ToListAsync();

                    double moduleScore = 0;
                    int scoreCount = 0;

                    foreach (var quizType in new[] { "regular", "midterm", "exam" })
                    {
                        var quiz = quizzes?.FirstOrDefault(q =>
                            q.quiz_type != null &&
                            q.quiz_type.ToLower() == quizType);

                        if (quiz != null)
                        {
                            var quizResults = await _context.QuizResults
                                .Where(qr => qr.userID == userId && qr.quizID == quiz.quizID)
                                .ToListAsync();

                            if (quizResults != null && quizResults.Any())
                            {
                                double quizScore = (double)quizResults.Count(qr => qr.is_correct == true) / quizResults.Count * 100;

                                moduleScore += quizScore;
                                scoreCount++;
                                _logger.LogDebug($"Added {quizType} quiz score: {quizScore} for module {module.module_name}");
                            }
                        }
                    }

                    foreach (var assignment in assignments?.Take(3) ?? Enumerable.Empty<Assignment>())
                    {
                        var submission = await _context.UserSubmissions
                            .Where(us => us.userID == userId &&
                                        us.assignmentID == assignment.assignmentID &&
                                        us.grade.HasValue)
                            .OrderByDescending(us => us.submission_date)
                            .FirstOrDefaultAsync();

                        if (submission != null)
                        {
                            moduleScore += (double)submission.grade.Value;
                            scoreCount++;
                            _logger.LogDebug($"Added assignment score: {submission.grade.Value} for module {module.module_name}");
                        }
                    }


                    double averageScore = scoreCount > 0 ? moduleScore / scoreCount : 0;
                    modulePredictions.Add(module.module_name, Math.Round(averageScore, 2));
                    _logger.LogInformation($"Final score for {module.module_name}: {averageScore}");
                }

                return modulePredictions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error predicting module grades");
                return new Dictionary<string, double>();
            }
        }

        private async Task<ProgressStats> GetProgressStats(int userId)
        {
            _logger.LogInformation($"Getting progress stats for user {userId}");
            var stats = new ProgressStats();

            try
            {
 
                stats.LessonsCompleted = await _context.LessonsUsers
                    .Where(lu => lu.userID == userId && lu.progress == 100)
                    .CountAsync();


                stats.QuizzesPassed = await _context.QuizResults
                    .Where(qr => qr.userID == userId)
                    .GroupBy(qr => qr.quizID)
                    .Select(g => new
                    {
                        ScorePercent = (double)g.Count(qr => qr.is_correct == true) / g.Count() * 100
                    })
                    .CountAsync(q => q.ScorePercent >= 70);

                var quizScores = await _context.QuizResults
                    .Where(qr => qr.userID == userId)
                    .GroupBy(qr => qr.quizID)
                    .Select(g => new
                    {
                        ScorePercent = (double)g.Count(qr => qr.is_correct == true) / g.Count() * 100
                    })
                    .ToListAsync();
                stats.AverageScore = quizScores.Any() ? quizScores.Average(q => q.ScorePercent) : 0;

                var totalStudyTime = await _context.LessonsUsers
                    .Where(lu => lu.userID == userId)
                    .SumAsync(lu => lu.time_spent);
                stats.TotalStudyTime = totalStudyTime / 60.0;

                stats.ProgressOverTime = await _context.LessonsUsers
                    .Where(lu => lu.userID == userId)
                    .GroupBy(lu => lu.last_accessed.HasValue ? lu.last_accessed.Value.Date : DateTime.MinValue)
                    .OrderBy(g => g.Key)
                    .Select(g => new ProgressDataPoint
                    {
                        Date = g.Key,
                        CompletedLessons = g.Count(lu => lu.progress == 100)
                    })
                    .ToListAsync();

                stats.EarnedBadges = await _context.UserBadges
                    .Where(ub => ub.userID == userId)
                    .Include(ub => ub.Badge)
                    .Select(ub => new BadgeDto
                    {
                        Name = ub.Badge.name,
                        Description = ub.Badge.description,
                        Icon = ub.Badge.icon_path ?? "bx bxs-trophy"
                    })
                    .ToListAsync();

                _logger.LogInformation($"Successfully gathered stats for user {userId}");
                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting progress stats");
                return new ProgressStats();
            }
        }

        [HttpGet]
        public async Task<IActionResult> DownloadReport(int userId)
        {
            try
            {
                _logger.LogInformation($"Generating report for user {userId}");

                var user = await _context.User
                    .Include(u => u.Profile)
                    .FirstOrDefaultAsync(u => u.userID == userId);

                if (user == null)
                {
                    return NotFound("User not found.");
                }

                var features = await BuildFeaturesFromDb(userId);
                var predictionRequest = new { features = features };
                var response = await _httpClient.PostAsJsonAsync("http://localhost:8000/predict", predictionRequest);
                var prediction = await response.Content.ReadFromJsonAsync<PredictionResponse>();

                var modulePredictions = await PredictModuleGrades(userId);

                var progressStats = await GetProgressStats(userId);

                var document = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(2, Unit.Centimetre);
                        page.PageColor(Colors.White);
                        page.DefaultTextStyle(x => x.FontSize(12));

                        page.Header()
                            .AlignCenter()
                            .Text("Student Performance Report")
                            .SemiBold().FontSize(20).FontColor(Colors.Blue.Medium);

                        page.Content()
                            .PaddingVertical(1, Unit.Centimetre)
                            .Column(column =>
                            {
                                column.Spacing(10);

                                column.Item().Text($"Student: {user.username}").SemiBold();
                                column.Item().Text($"Report date: {DateTime.Now:MMMM dd, yyyy}");

                                column.Item().PaddingTop(10).Text("Performance Summary").SemiBold().FontSize(16);
                                column.Item().Text($"Predicted Final Grade: {prediction.predicted_grade:F0}/100");
                                column.Item().Text($"Dropout Risk: {prediction.dropout_probability:P0}");
                                column.Item().Text($"Lessons Completed: {progressStats.LessonsCompleted}");
                                column.Item().Text($"Quizzes Passed: {progressStats.QuizzesPassed}");
                                column.Item().Text($"Average Score: {progressStats.AverageScore:F0}%");

                                column.Item().PaddingTop(10).Text("Module Performance").SemiBold().FontSize(16);
                                foreach (var module in modulePredictions)
                                {
                                    column.Item().Text($"{module.Key}: {module.Value:F0}%");
                                }
                            });

                        page.Footer()
                            .AlignCenter()
                            .Text(x =>
                            {
                                x.Span("Page ");
                                x.CurrentPageNumber();
                            });
                    });
                });

                var fileName = $"PerformanceReport_{user.username}_{DateTime.Now:yyyyMMdd}.pdf";
                return File(document.GeneratePdf(), "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating PDF report");
                return StatusCode(500, "Error generating PDF report");
            }
        }

        [HttpGet]
        [Route("TeacherProgress")] 
        public async Task<IActionResult> TeacherProgress()
        {
            try
            {
                _logger.LogInformation("Starting TeacherProgress action");

                var username = HttpContext.Session.GetString("username");
                if (string.IsNullOrEmpty(username))
                {
                    _logger.LogWarning("User not authenticated");
                    return Unauthorized("User not authenticated or session expired.");
                }

                var user = await _context.User
                    .Include(u => u.Profile)
                    .FirstOrDefaultAsync(u => u.username == username);

                if (user == null)
                {
                    _logger.LogWarning($"User not found: {username}");
                    return NotFound("User not found.");
                }

                if (user.role != "Profesor")
                {
                    _logger.LogWarning($"Access denied for user: {username}");
                    return Forbid("Access denied. Only teachers can view this page.");
                }

                _logger.LogInformation($"Processing teacher progress for user: {user.userID}");

                ViewBag.AvatarUrl = user.Profile?.avatar_url ?? "https://randomuser.me/api/portraits/lego/1.jpg";
                ViewBag.Username = user.username;
                ViewBag.CurrentUserId = user.userID;

                var students = await _context.User
                    .Where(u => u.role == "Student")
                    .Include(u => u.Profile)
                    .Include(u => u.LessonsUsers)
                    .Include(u => u.QuizResults)
                        .ThenInclude(qr => qr.Quiz)
                    .Include(u => u.UserSubmissions)
                    .Include(u => u.UserBadges)
                        .ThenInclude(ub => ub.Badge)
                    .ToListAsync();

                var studentProgressList = new List<StudentProgressViewModel>();

                foreach (var student in students)
                {
                    var progressStats = await GetProgressStats(student.userID);
                    var modulePredictions = await PredictModuleGrades(student.userID);

                    var features = await BuildFeaturesFromDb(student.userID);
                    var predictionRequest = new { features = features };
                    var response = await _httpClient.PostAsJsonAsync("http://localhost:8000/predict", predictionRequest);
                    var prediction = await response.Content.ReadFromJsonAsync<PredictionResponse>();

                    studentProgressList.Add(new StudentProgressViewModel
                    {
                        StudentId = student.userID,
                        Username = student.username,
                        AvatarUrl = student.Profile?.avatar_url ?? "https://randomuser.me/api/portraits/lego/1.jpg",
                        Email = student.email,
                        LastLogin = student.last_login,
                        LessonsCompleted = progressStats.LessonsCompleted,
                        QuizzesPassed = progressStats.QuizzesPassed,
                        AverageScore = progressStats.AverageScore,
                        TotalStudyTime = progressStats.TotalStudyTime,
                        PredictedGrade = prediction?.predicted_grade ?? 0,
                        DropoutRisk = prediction?.dropout_probability ?? 0,
                        ModulePredictions = modulePredictions,
                        EarnedBadges = progressStats.EarnedBadges
                    });
                }

                _logger.LogInformation("Successfully generated teacher progress view");
                return View(studentProgressList);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in TeacherProgress action");
                return StatusCode(500, "An error occurred while processing your request");
            }
        }

        [HttpGet]
        public async Task<IActionResult> DownloadClassReport()
        {
            try
            {
                _logger.LogInformation("Generating class report");

                var students = await _context.User
                    .Where(u => u.role == "Student")
                    .Include(u => u.Profile)
                    .Include(u => u.LessonsUsers)
                    .Include(u => u.QuizResults).ThenInclude(qr => qr.Quiz)
                    .Include(u => u.UserSubmissions)
                    .ToListAsync();

                var reportDataList = new List<StudentReportData>();

                double totalPredictedGrade = 0;
                double totalDropoutRisk = 0;
                double totalLessonsCompleted = 0;
                double totalQuizzesPassed = 0;
                double totalStudyTime = 0;

                foreach (var student in students)
                {
                    var features = await BuildFeaturesFromDb(student.userID);
                    var predictionRequest = new { features };
                    var response = await _httpClient.PostAsJsonAsync("http://localhost:8000/predict", predictionRequest);
                    var prediction = await response.Content.ReadFromJsonAsync<PredictionResponse>();

                    var stats = await GetProgressStats(student.userID);

                    var predictedGrade = prediction?.predicted_grade ?? 0;
                    var dropoutRisk = prediction?.dropout_probability ?? 0;

                    totalPredictedGrade += predictedGrade;
                    totalDropoutRisk += dropoutRisk;
                    totalLessonsCompleted += stats.LessonsCompleted;
                    totalQuizzesPassed += stats.QuizzesPassed;
                    totalStudyTime += stats.TotalStudyTime;

                    reportDataList.Add(new StudentReportData
                    {
                        Username = student.username,
                        PredictedGrade = predictedGrade,
                        DropoutRisk = dropoutRisk,
                        LessonsCompleted = stats.LessonsCompleted,
                        QuizzesPassed = stats.QuizzesPassed,
                        TotalStudyTime = stats.TotalStudyTime
                    });
                }

                double count = students.Count;
                double avgPredictedGrade = totalPredictedGrade / count;
                double avgDropoutRisk = totalDropoutRisk / count;
                double avgLessonsCompleted = totalLessonsCompleted / count;
                double avgQuizzesPassed = totalQuizzesPassed / count;
                double avgStudyTime = totalStudyTime / count;

                var document = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(2, Unit.Centimetre);
                        page.PageColor(Colors.White);
                        page.DefaultTextStyle(x => x.FontSize(10));

                        page.Header()
                            .AlignCenter()
                            .Text("Class Performance Report")
                            .SemiBold().FontSize(20).FontColor(Colors.Blue.Medium);

                        page.Content()
                            .PaddingVertical(1, Unit.Centimetre)
                            .Column(column =>
                            {
                                column.Spacing(10);
                                column.Item().Text($"Report Date: {DateTime.Now:MMMM dd, yyyy}");
                                column.Item().PaddingTop(10).Text("Class Summary").SemiBold().FontSize(16);

                                column.Item().Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.RelativeColumn();
                                        columns.RelativeColumn();
                                        columns.RelativeColumn();
                                        columns.RelativeColumn();
                                    });

                                    table.Header(header =>
                                    {
                                        header.Cell().Text("Avg Predicted Grade").SemiBold();
                                        header.Cell().Text("Avg Dropout Risk").SemiBold();
                                        header.Cell().Text("Avg Lessons Completed").SemiBold();
                                        header.Cell().Text("Avg Study Time (h)").SemiBold();
                                    });

                                    table.Cell().Text($"{avgPredictedGrade:F0}/100");
                                    table.Cell().Text($"{avgDropoutRisk:P0}");
                                    table.Cell().Text($"{avgLessonsCompleted:F0}");
                                    table.Cell().Text($"{avgStudyTime:F1}");
                                });

                                column.Item().PaddingTop(10).Text("Student Performance").SemiBold().FontSize(16);

                                column.Item().Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.RelativeColumn();
                                        columns.RelativeColumn();
                                        columns.RelativeColumn();
                                        columns.RelativeColumn();
                                        columns.RelativeColumn();
                                        columns.RelativeColumn();
                                    });

                                    table.Header(header =>
                                    {
                                        header.Cell().Text("Student").SemiBold();
                                        header.Cell().Text("Predicted Grade").SemiBold();
                                        header.Cell().Text("Dropout Risk").SemiBold();
                                        header.Cell().Text("Lessons").SemiBold();
                                        header.Cell().Text("Quizzes").SemiBold();
                                        header.Cell().Text("Study Time").SemiBold();
                                    });

                                    foreach (var data in reportDataList)
                                    {
                                        table.Cell().Text(data.Username);
                                        table.Cell().Text($"{data.PredictedGrade:F0}/100");
                                        table.Cell().Text($"{data.DropoutRisk:P0}");
                                        table.Cell().Text(data.LessonsCompleted.ToString());
                                        table.Cell().Text(data.QuizzesPassed.ToString());
                                        table.Cell().Text($"{data.TotalStudyTime:F1}h");
                                    }
                                });
                            });

                        page.Footer()
                            .AlignCenter()
                            .Text(x =>
                            {
                                x.Span("Page ");
                                x.CurrentPageNumber();
                            });
                    });
                });

                var fileName = $"ClassPerformanceReport_{DateTime.Now:yyyyMMdd}.pdf";
                return File(document.GeneratePdf(), "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating class PDF report");
                return StatusCode(500, "Error generating PDF report");
            }
        }
    }

    
    public class PredictionResponse
    {
        public double predicted_grade { get; set; }
        public double predicted_dropout { get; set; }
        public double dropout_probability { get; set; }
        public Dictionary<string, double> module_predictions { get; set; }
    }
    public class ProgressDataPoint
    {
        public DateTime Date { get; set; }
        public int CompletedLessons { get; set; }
    }

    public class ProgressViewModel
    {
        public PredictionResponse Prediction { get; set; }
        public Dictionary<string, double> ModulePredictions { get; set; }
        public int LessonsCompleted { get; set; }
        public int QuizzesPassed { get; set; }
        public double AverageScore { get; set; }
        public double TotalStudyTime { get; set; } // ore
        public List<ProgressDataPoint> ProgressOverTime { get; set; }
        public List<BadgeDto> EarnedBadges { get; set; }
    }

    public class BadgeDto
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Icon { get; set; }
    }

    public class ProgressStats
    {
        public int LessonsCompleted { get; set; }
        public int QuizzesPassed { get; set; }
        public double AverageScore { get; set; }
        public double TotalStudyTime { get; set; }
        public List<ProgressDataPoint> ProgressOverTime { get; set; }
        public List<BadgeDto> EarnedBadges { get; set; }

        public ProgressStats()
        {
            ProgressOverTime = new List<ProgressDataPoint>();
            EarnedBadges = new List<BadgeDto>();
        }
    }
    
    public class StudentProgressViewModel
    {
        public int StudentId { get; set; }
        public string Username { get; set; }
        public string AvatarUrl { get; set; }
        public string Email { get; set; }
        public DateTime? LastLogin { get; set; }
        public int LessonsCompleted { get; set; }
        public int QuizzesPassed { get; set; }
        public double AverageScore { get; set; }
        public double TotalStudyTime { get; set; } // ore
        public double PredictedGrade { get; set; }
        public double DropoutRisk { get; set; }
        public Dictionary<string, double> ModulePredictions { get; set; }
        public List<BadgeDto> EarnedBadges { get; set; }
    }

    public class StudentReportData
    {
        public string Username { get; set; }
        public double PredictedGrade { get; set; }
        public double DropoutRisk { get; set; }
        public int LessonsCompleted { get; set; }
        public int QuizzesPassed { get; set; }
        public double TotalStudyTime { get; set; }
    }
}
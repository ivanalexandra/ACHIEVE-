using Achieve_Plus.Models;
using Achieve_Plus.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Achieve_Plus.Controllers
{
    public class QuizzesController : Controller
    {
        private readonly AppDbContext _context;
        private readonly BadgeService _badgeService;

        public QuizzesController(AppDbContext context, BadgeService badgeService)
        {
            _context = context;
            _badgeService = badgeService;
        }

        public async Task<IActionResult> Quizzes()
        {
            var username = HttpContext.Session.GetString("username");
            var user = await _context.User
                .Include(u => u.Profile)
                .FirstOrDefaultAsync(u => u.username == username);

            ViewBag.AvatarUrl = user?.Profile?.avatar_url ?? "https://randomuser.me/api/portraits/lego/1.jpg";
            ViewBag.Username = user?.username ?? "Utilizator";
            ViewBag.IsProfessor = user?.role == "Profesor";
            return View("Quizzes");
        }

        public async Task<IActionResult> Quiz(int id)
        {
            var username = HttpContext.Session.GetString("username");
            var user = await _context.User
                .Include(u => u.Profile)
                .FirstOrDefaultAsync(u => u.username == username);

            if (user == null) return RedirectToAction("Login", "Login");

            ViewBag.AvatarUrl = user?.Profile?.avatar_url ?? "https://randomuser.me/api/portraits/lego/1.jpg";
            ViewBag.Username = user?.username ?? "Utilizator";
            ViewBag.QuizId = id;

            return View("Quiz");
        }

        public async Task<IActionResult> Create()
        {
            var username = HttpContext.Session.GetString("username");
            var user = await _context.User
                .Include(u => u.Profile)
                .FirstOrDefaultAsync(u => u.username == username);

            if (user?.role != "Profesor")
                return RedirectToAction("Quizzes");

            ViewBag.AvatarUrl = user?.Profile?.avatar_url ?? "https://randomuser.me/api/portraits/lego/1.jpg";
            ViewBag.Username = user?.username ?? "Utilizator";
            return View("Create");
        }

        public async Task<IActionResult> Edit(int id)
        {
            var username = HttpContext.Session.GetString("username");
            var user = await _context.User
                .Include(u => u.Profile)
                .FirstOrDefaultAsync(u => u.username == username);

            if (user?.role != "Profesor")
                return RedirectToAction("Quizzes");

            var quiz = await _context.Quizzes
                .Include(q => q.QuizItems)
                .ThenInclude(qi => qi.Item)
                .ThenInclude(i => i.ItemChoices)
                .Include(q => q.Lesson)
                .FirstOrDefaultAsync(q => q.quizID == id);

            if (quiz == null)
                return RedirectToAction("Quizzes");

            ViewBag.AvatarUrl = user?.Profile?.avatar_url ?? "https://randomuser.me/api/portraits/lego/1.jpg";
            ViewBag.Username = user?.username ?? "Utilizator";

            return View("Edit", quiz);
        }

        [HttpGet]
        public async Task<IActionResult> GetQuizzes()
        {
            var username = HttpContext.Session.GetString("username");
            var user = await _context.User.FirstOrDefaultAsync(u => u.username == username);

            if (user == null)
                return Unauthorized();

            if (user.role == "Profesor")
            {

                var quizzes = await _context.Quizzes
                    .Include(q => q.Lesson)
                    .Include(q => q.QuizItems)
                    .ThenInclude(qi => qi.Item)
                    .ToListAsync();

                var quizDtos = quizzes.Select(q => new
                {
                    id = q.quizID,
                    title = q.title,
                    description = q.description,
                    lesson = q.Lesson?.lesson_title ?? "Necunoscut",
                    questionsCount = q.QuizItems.Count,
                    timeLimit = q.duration_minutes ?? 20,
                    maxScore = 100,
                    status = "professor",
                    quizType = q.quiz_type ?? "regular",
                    score = 0,
                    attemptsLeft = 0,
                    lessonId = q.lessonID
                });

                return Json(quizDtos);
            }
            else
            {

                int userId = user.userID;

                var startedLessons = await _context.LessonsUsers
                    .Where(ul => ul.userID == userId && ul.status != "not_started")
                    .Select(ul => ul.lessonID)
                    .ToListAsync();

                var quizzes = await _context.Quizzes
                    .Include(q => q.Lesson)
                    .Include(q => q.QuizItems)
                    .ThenInclude(qi => qi.Item)
                    .Where(q => startedLessons.Contains(q.lessonID))
                    .ToListAsync();

                var results = await _context.QuizResults
                    .Where(r => r.userID == userId)
                    .ToListAsync();

                var quizDtos = quizzes.Select(q =>
                {
                    var quizResults = results.Where(r => r.quizID == q.quizID).ToList();
                    var totalQuestions = q.QuizItems.Count;
                    var answeredItems = quizResults.Select(r => r.itemID).Distinct().Count();
                    var correctAnswers = quizResults.Count(r => r.is_correct == true);

                    string status;
                    int? score = null;

                    if (answeredItems == 0)
                    {
                        status = "not_started";
                    }
                    else if (answeredItems < totalQuestions)
                    {
                        status = "in_progress";
                    }
                    else
                    {
                        status = "completed";
                        score = (int)((double)correctAnswers / totalQuestions * 100);
                    }

                    int attempts = quizResults
                        .Select(r => r.attempt_date?.ToString("yyyyMMddHHmmss"))
                        .Distinct()
                        .Count();

                    int maxAttempts = 3;
                    int attemptsLeft = maxAttempts - attempts;
                    if (attemptsLeft < 0) attemptsLeft = 0;

                    return new
                    {
                        id = q.quizID,
                        title = q.title,
                        description = q.description,
                        lesson = q.Lesson?.lesson_title ?? "Necunoscut",
                        questionsCount = totalQuestions,
                        timeLimit = q.duration_minutes ?? 20,
                        maxScore = 100,
                        status,
                        quizType = q.quiz_type ?? "regular",
                        score,
                        attemptsLeft,
                        lessonId = q.lessonID
                    };
                });

                return Json(quizDtos);
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetQuizDetails(int quizId)
        {
            var quiz = await _context.Quizzes
                .Include(q => q.QuizItems)
                    .ThenInclude(qi => qi.Item)
                        .ThenInclude(i => i.ItemChoices)
                .FirstOrDefaultAsync(q => q.quizID == quizId);

            if (quiz == null) return NotFound();

            var quizDetails = new
            {
                id = quiz.quizID,
                title = quiz.title,
                description = quiz.description,
                timeLimit = quiz.duration_minutes ?? 20,
                quizType = quiz.quiz_type ?? "regular",
                items = quiz.QuizItems
                    .OrderBy(qi => qi.position)
                    .Select(qi => new
                    {
                        id = qi.Item.itemID,
                        content = qi.Item.content,
                        type = qi.Item.type,
                        difficulty = qi.Item.difficulty,
                        choices = qi.Item.ItemChoices.Select(c => new
                        {
                            id = c.itemchoicesID,
                            content = c.content
                        })
                    })
            };

            return Json(quizDetails);
        }

        [HttpGet]
        public async Task<IActionResult> GetQuiz(int id)
        {
            var quiz = await _context.Quizzes
                .Include(q => q.QuizItems)
                    .ThenInclude(qi => qi.Item)
                        .ThenInclude(i => i.ItemChoices)
                .FirstOrDefaultAsync(q => q.quizID == id);

            if (quiz == null)
                return NotFound();

            var result = new
            {
                id = quiz.quizID,
                title = quiz.title,
                description = quiz.description,
                quizType = quiz.quiz_type ?? "regular",
                questions = quiz.QuizItems
                    .OrderBy(qi => qi.position)
                    .Select(qi => new
                    {
                        id = qi.Item.itemID,
                        content = qi.Item.content,
                        type = qi.Item.type,
                        choices = qi.Item.ItemChoices.Select(c => new
                        {
                            id = c.itemchoicesID,
                            content = c.content,
                            is_correct = c.is_correct
                        })
                    })
            };

            return Json(result);
        }

        [HttpPost]
        public async Task<IActionResult> SubmitResults([FromBody] object submission)
        {
            var username = HttpContext.Session.GetString("username");
            var user = await _context.User.FirstOrDefaultAsync(u => u.username == username);

            if (user == null)
                return Unauthorized();

            int userId = user.userID;

            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(submission.ToString());
                var root = doc.RootElement;

                int quizID = root.GetProperty("quizID").GetInt32();
                var results = root.GetProperty("results");

                DateTime now = DateTime.UtcNow;

                foreach (var result in results.EnumerateArray())
                {
                    int itemID = result.GetProperty("itemID").GetInt32();
                    int choiceID = result.GetProperty("choiceID").GetInt32();

                    var choice = await _context.ItemChoices
                        .FirstOrDefaultAsync(c => c.itemchoicesID == choiceID && c.itemID == itemID);

                    if (choice == null) continue;

                    bool isCorrect = choice.is_correct;

                    var quizResult = new QuizResult
                    {
                        userID = userId,
                        quizID = quizID,
                        itemID = itemID,
                        is_correct = isCorrect,
                        attempt_date = now
                    };

                    _context.QuizResults.Add(quizResult);
                }

                await _context.SaveChangesAsync();
                await _badgeService.CheckAndAwardBadges(user.userID);

                return Ok(new { message = "Rezultatele au fost salvate cu succes!" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = "Date invalide sau eroare la salvare.", details = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> CheckLessonAccess(int quizId)
        {
            var username = HttpContext.Session.GetString("username");
            var user = await _context.User.FirstOrDefaultAsync(u => u.username == username);

            if (user == null)
                return Unauthorized();

            var quiz = await _context.Quizzes
                .FirstOrDefaultAsync(q => q.quizID == quizId);

            if (quiz == null)
                return NotFound();

            var hasAccess = await _context.LessonsUsers
                .AnyAsync(ul => ul.userID == user.userID &&
                               ul.lessonID == quiz.lessonID &&
                               ul.status != "not_started");

            return Json(new { hasAccess });
        }

        [HttpGet]
        public async Task<IActionResult> GetAllQuestions()
        {
            var questions = await _context.Items
                .Include(i => i.ItemChoices)
                .Select(i => new
                {
                    id = i.itemID,
                    content = i.content,
                    type = i.type,
                    difficulty = i.difficulty,
                    choices = i.ItemChoices.Select(c => new
                    {
                        id = c.itemchoicesID,
                        content = c.content,
                        is_correct = c.is_correct
                    })
                }).ToListAsync();

            return Json(questions);
        }

        [HttpGet]
        public async Task<IActionResult> GetAllLessons()
        {
            var lessons = await _context.Lesson
                .Select(l => new
                {
                    id = l.lessonID,
                    title = l.lesson_title,
                    moduleId = l.moduleID
                }).ToListAsync();

            return Json(lessons);
        }

        [HttpPost]
        public async Task<IActionResult> CreateQuiz([FromBody] QuizCreateModel model)
        {
            var username = HttpContext.Session.GetString("username");
            var user = await _context.User.FirstOrDefaultAsync(u => u.username == username);

            if (user?.role != "Profesor")
                return Unauthorized();

            if (string.IsNullOrWhiteSpace(model.Title))
                return BadRequest("Titlul testului este obligatoriu");

            if (model.LessonId <= 0)
                return BadRequest("Lecția asociată este obligatorie");

            if (model.QuestionIds.Count == 0 && model.NewQuestions.Count == 0)
                return BadRequest("Quizul trebuie să conțină cel puțin o întrebare");

            int requiredQuestions = model.QuizType switch
            {
                "regular" => 5,
                "midterm" => 10,
                "exam" => 15,
                _ => 5
            };

            int totalQuestions = model.QuestionIds.Count + model.NewQuestions.Count;
            if (totalQuestions != requiredQuestions)
                return BadRequest($"Acest tip de quiz necesită exact {requiredQuestions} întrebări");

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var quiz = new Quiz
                {
                    lessonID = model.LessonId,
                    title = model.Title,
                    description = model.Description,
                    duration_minutes = model.DurationMinutes,
                    created_at = DateTime.UtcNow,
                    quiz_type = model.QuizType
                };

                _context.Quizzes.Add(quiz);
                await _context.SaveChangesAsync();

                int position = 1;

                foreach (var questionId in model.QuestionIds)
                {
                    var quizItem = new QuizItem
                    {
                        quizID = quiz.quizID,
                        itemID = questionId,
                        position = position++
                    };
                    _context.QuizItems.Add(quizItem);
                }

                foreach (var newQuestion in model.NewQuestions)
                {
                    if (string.IsNullOrWhiteSpace(newQuestion.Content))
                        throw new Exception("Conținutul întrebării nu poate fi gol");

                    if (newQuestion.Choices.Count < 2)
                        throw new Exception("Fiecare întrebare trebuie să aibă cel puțin 2 opțiuni");

                    if (newQuestion.Choices.Count(c => c.IsCorrect) != 1)
                        throw new Exception("Fiecare întrebare trebuie să aibă exact o opțiune corectă");

                    var item = new Item
                    {
                        content = newQuestion.Content,
                        type = "multiple_choice",
                        difficulty = newQuestion.Difficulty,
                        created_at = DateTime.UtcNow
                    };

                    _context.Items.Add(item);
                    await _context.SaveChangesAsync();

                    foreach (var choice in newQuestion.Choices)
                    {
                        var itemChoice = new ItemChoice
                        {
                            itemID = item.itemID,
                            content = choice.Content,
                            is_correct = choice.IsCorrect
                        };
                        _context.ItemChoices.Add(itemChoice);
                    }

                    var newQuizItem = new QuizItem
                    {
                        quizID = quiz.quizID,
                        itemID = item.itemID,
                        position = position++
                    };
                    _context.QuizItems.Add(newQuizItem);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new
                {
                    message = "Quiz creat cu succes!",
                    quizId = quiz.quizID,
                    redirectUrl = Url.Action("Quizzes", "Quizzes")
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new
                {
                    error = "Eroare la crearea quiz-ului",
                    details = ex.Message
                });
            }
        }

        [HttpPut]
        public async Task<IActionResult> UpdateQuiz(int id, [FromBody] QuizCreateModel model)
        {
            var username = HttpContext.Session.GetString("username");
            var user = await _context.User.FirstOrDefaultAsync(u => u.username == username);

            if (user?.role != "Profesor")
                return Unauthorized();

            var quiz = await _context.Quizzes.FindAsync(id);
            if (quiz == null)
                return NotFound();

            if (string.IsNullOrWhiteSpace(model.Title))
                return BadRequest("Titlul testului este obligatoriu");

            if (model.LessonId <= 0)
                return BadRequest("Lecția asociată este obligatorie");

            int requiredQuestions = model.QuizType switch
            {
                "regular" => 5,
                "midterm" => 10,
                "exam" => 15,
                _ => 5
            };

            int totalQuestions = model.QuestionIds.Count + model.NewQuestions.Count;
            if (totalQuestions != requiredQuestions)
                return BadRequest($"Acest tip de quiz necesită exact {requiredQuestions} întrebări");

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                quiz.title = model.Title;
                quiz.description = model.Description;
                quiz.duration_minutes = model.DurationMinutes;
                quiz.quiz_type = model.QuizType;

                var existingItems = await _context.QuizItems.Where(qi => qi.quizID == id).ToListAsync();
                _context.QuizItems.RemoveRange(existingItems);

                int position = 1;

                foreach (var questionId in model.QuestionIds)
                {
                    var quizItem = new QuizItem
                    {
                        quizID = quiz.quizID,
                        itemID = questionId,
                        position = position++
                    };
                    _context.QuizItems.Add(quizItem);
                }

                foreach (var newQuestion in model.NewQuestions)
                {
                    if (string.IsNullOrWhiteSpace(newQuestion.Content))
                        throw new Exception("Conținutul întrebării nu poate fi gol");

                    if (newQuestion.Choices.Count < 2)
                        throw new Exception("Fiecare întrebare trebuie să aibă cel puțin 2 opțiuni");

                    if (newQuestion.Choices.Count(c => c.IsCorrect) != 1)
                        throw new Exception("Fiecare întrebare trebuie să aibă exact o opțiune corectă");

                    var item = new Item
                    {
                        content = newQuestion.Content,
                        type = "multiple_choice",
                        difficulty = newQuestion.Difficulty,
                        created_at = DateTime.UtcNow
                    };

                    _context.Items.Add(item);
                    await _context.SaveChangesAsync();

                    foreach (var choice in newQuestion.Choices)
                    {
                        var itemChoice = new ItemChoice
                        {
                            itemID = item.itemID,
                            content = choice.Content,
                            is_correct = choice.IsCorrect
                        };
                        _context.ItemChoices.Add(itemChoice);
                    }

                    var newQuizItem = new QuizItem
                    {
                        quizID = quiz.quizID,
                        itemID = item.itemID,
                        position = position++
                    };
                    _context.QuizItems.Add(newQuizItem);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { message = "Quiz actualizat cu succes!" });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new
                {
                    error = "Eroare la actualizarea quiz-ului",
                    details = ex.Message
                });
            }
        }

        public async Task<IActionResult> DeleteQuiz(int id)
        {
            var username = HttpContext.Session.GetString("username");
            var user = await _context.User
                .Include(u => u.Profile)
                .FirstOrDefaultAsync(u => u.username == username);

            if (user?.role != "Profesor")
                return RedirectToAction("Quizzes");

            var quiz = await _context.Quizzes
                .Include(q => q.Lesson)
                .FirstOrDefaultAsync(q => q.quizID == id);

            if (quiz == null)
                return RedirectToAction("Quizzes");

            ViewBag.AvatarUrl = user?.Profile?.avatar_url ?? "https://randomuser.me/api/portraits/lego/1.jpg";
            ViewBag.Username = user?.username ?? "Utilizator";

            return View("DeleteQuiz", quiz);
        }

        [HttpGet]
        public async Task<IActionResult> DeleteQuizConfirmed(int id)
        {
            var username = HttpContext.Session.GetString("username");
            var user = await _context.User.FirstOrDefaultAsync(u => u.username == username);

            if (user?.role != "Profesor")
                return RedirectToAction("Quizzes");

            var quiz = await _context.Quizzes
                .Include(q => q.QuizItems)
                .FirstOrDefaultAsync(q => q.quizID == id);

            if (quiz == null)
                return RedirectToAction("Quizzes");

            try
            {
                _context.QuizItems.RemoveRange(quiz.QuizItems);
                _context.Quizzes.Remove(quiz);
                await _context.SaveChangesAsync();

                return RedirectToAction("Quizzes");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Eroare la ștergere: " + ex.Message;
                return RedirectToAction("DeleteQuiz", new { id });
            }
        }

    }

    public class QuizCreateModel
    {
        public int LessonId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public int DurationMinutes { get; set; } = 20;
        public string QuizType { get; set; } = "regular";
        public List<int> QuestionIds { get; set; } = new List<int>();
        public List<NewQuestionModel> NewQuestions { get; set; } = new List<NewQuestionModel>();
    }

    public class NewQuestionModel
    {
        public string Content { get; set; }
        public int Difficulty { get; set; } = 1;
        public List<ChoiceModel> Choices { get; set; } = new List<ChoiceModel>();
    }

    public class ChoiceModel
    {
        public string Content { get; set; }
        public bool IsCorrect { get; set; }
    }
    
}
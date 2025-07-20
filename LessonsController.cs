using Achieve_Plus.Models;
using Achieve_Plus.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Achieve_Plus.Controllers
{
    public class LessonsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly BadgeService _badgeService;

        public LessonsController(AppDbContext context, BadgeService badgeService)
        {
            _context = context;
            _badgeService = badgeService;
        }

        private async Task<bool> IsUserProfesor()
        {
            var username = HttpContext.Session.GetString("username");
            if (string.IsNullOrEmpty(username))
                return false;

            var user = await _context.User
                .FirstOrDefaultAsync(u => u.username == username);

            return user?.role == "Profesor";
        }

        // GET: Lessons
        public async Task<IActionResult> Lessons()
        {
            var username = HttpContext.Session.GetString("username");
            if (string.IsNullOrEmpty(username))
                return RedirectToAction("Login", "Account");

            var user = await _context.User
                .Include(u => u.Profile)
                .FirstOrDefaultAsync(u => u.username == username);

            if (user == null)
                return RedirectToAction("Login", "Account");

            ViewBag.AvatarUrl = user?.Profile?.avatar_url ?? "https://randomuser.me/api/portraits/lego/1.jpg";
            ViewBag.Username = user?.username ?? "Utilizator";
            ViewBag.IsTeacher = await IsUserProfesor();

            var lessonsWithModules = await _context.Lesson
                .Include(l => l.Module)
                .ToListAsync();

            var userLessons = await _context.LessonsUsers
                .Where(lu => lu.userID == user.userID)
                .ToListAsync();

            var model = lessonsWithModules.Select(l =>
            {
                var ul = userLessons.FirstOrDefault(ul => ul.lessonID == l.lessonID);
                return new LessonProgressViewModel
                {
                    LessonID = l.lessonID,
                    LessonTitle = l.lesson_title,
                    LessonContent = l.lesson_content,
                    Progress = ul?.progress ?? 0,
                    Status = ul?.status ?? "not_started",
                    Start_Date = ul?.start_date,
                    Finish_Date = ul?.finish_date,
                    Time_Spent = ul?.time_spent ?? 0,
                    Attempts = ul?.attempts ?? 0,
                    Last_Accessed = ul?.last_accessed,
                    ModuleName = l.Module?.module_name ?? "N/A",
                    ModuleID = l.moduleID
                };
            }).ToList();

            return View(model);
        }

        public async Task<IActionResult> Lesson(int id)
        {
            var username = HttpContext.Session.GetString("username");
            if (string.IsNullOrEmpty(username))
                return RedirectToAction("Login", "Account");

            var user = await _context.User
                .Include(u => u.Profile)
                .FirstOrDefaultAsync(u => u.username == username);

            if (user == null)
                return RedirectToAction("Login", "Account");

            // Actualizăm last_accessed indiferent dacă utilizatorul continuă sau începe lecția
            var lessonUser = await _context.LessonsUsers
                .FirstOrDefaultAsync(lu => lu.lessonID == id && lu.userID == user.userID);

            if (lessonUser != null)
            {
                lessonUser.last_accessed = DateTime.Now;
                await _context.SaveChangesAsync();
            }
            else
            {
                // Dacă lecția nu a fost începută, o inițializăm
                lessonUser = new LessonsUsers
                {
                    userID = user.userID,
                    lessonID = id,
                    status = "started",
                    start_date = DateTime.Now,
                    progress = 0,
                    attempts = 1,
                    time_spent = 0,
                    last_accessed = DateTime.Now
                };
                _context.LessonsUsers.Add(lessonUser);
                await _context.SaveChangesAsync();
            }

            var lesson = await _context.Lesson
                .Include(l => l.LessonTags)
                    .ThenInclude(lt => lt.Tag)
                .FirstOrDefaultAsync(l => l.lessonID == id);

            if (lesson == null)
                return NotFound();

            var moduleId = lesson.moduleID;

            var lessonsInModule = await _context.Lesson
                .Where(l => l.moduleID == moduleId)
                .OrderBy(l => l.lessonID)
                .ToListAsync();

            var currentIndex = lessonsInModule.FindIndex(l => l.lessonID == id);

            int? previousLessonId = currentIndex > 0 ? lessonsInModule[currentIndex - 1].lessonID : (int?)null;
            int? nextLessonId = currentIndex < lessonsInModule.Count - 1 ? lessonsInModule[currentIndex + 1].lessonID : (int?)null;

            var model = new LessonProgressViewModel
            {
                LessonID = lesson.lessonID,
                LessonTitle = lesson.lesson_title,
                LessonContent = lesson.lesson_content,
                Progress = lessonUser?.progress ?? 0,
                Status = lessonUser?.status ?? "not_started",
                Start_Date = lessonUser?.start_date,
                Finish_Date = lessonUser?.finish_date,
                Time_Spent = lessonUser?.time_spent ?? 0,
                Attempts = lessonUser?.attempts ?? 0,
                Last_Accessed = lessonUser?.last_accessed,
                ModuleID = lesson.moduleID,
                Tags = lesson.LessonTags.Select(lt => lt.Tag.name).ToList(),
                IsTeacher = await IsUserProfesor()
            };

            ViewBag.PreviousLessonId = previousLessonId;
            ViewBag.NextLessonId = nextLessonId;
            ViewBag.AvatarUrl = user?.Profile?.avatar_url ?? "https://randomuser.me/api/portraits/lego/1.jpg";
            ViewBag.Username = user?.username ?? "Utilizator";

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> StartLesson(int lessonID)
        {
            var username = HttpContext.Session.GetString("username");
            if (string.IsNullOrEmpty(username))
                return RedirectToAction("Login", "Account");

            var user = await _context.User.FirstOrDefaultAsync(u => u.username == username);
            if (user == null)
                return RedirectToAction("Login", "Account");

            var existing = await _context.LessonsUsers
                .FirstOrDefaultAsync(lu => lu.userID == user.userID && lu.lessonID == lessonID);

            if (existing == null)
            {
                var lessonUser = new LessonsUsers
                {
                    userID = user.userID,
                    lessonID = lessonID,
                    status = "started",
                    start_date = DateTime.Now,
                    progress = 0,
                    attempts = 1,
                    time_spent = 0,
                    last_accessed = DateTime.Now
                };
                _context.LessonsUsers.Add(lessonUser);
            }
            else
            {
                // Nu resetăm progresul, doar actualizăm data de acces
                existing.last_accessed = DateTime.Now;
                existing.attempts += 1;
            }

            await _context.SaveChangesAsync();
            await _badgeService.CheckAndAwardBadges(user.userID);

            return RedirectToAction("Lesson", new { id = lessonID });
        }

        [HttpPost]
        public async Task<IActionResult> UpdateLessonProgress([FromBody] LessonProgressDto data)
        {
            var username = HttpContext.Session.GetString("username");
            if (string.IsNullOrEmpty(username))
                return Unauthorized();

            var user = await _context.User.FirstOrDefaultAsync(u => u.username == username);
            if (user == null)
                return Unauthorized();

            var existing = await _context.LessonsUsers
                .FirstOrDefaultAsync(lu => lu.userID == user.userID && lu.lessonID == data.lessonID);

            if (existing != null)
            {
                if (data.progress <= existing.progress && !data.finished)
                {
                    return Ok(new
                    {
                        message = "Progresul a fost păstrat",
                        currentProgress = existing.progress
                    });
                }

                existing.progress = Math.Max(existing.progress, data.progress);
                existing.time_spent += data.timeSpent;
                existing.last_accessed = DateTime.Now;

                if (data.finished && existing.progress >= 100)
                {
                    existing.status = "completed";
                    existing.finish_date = existing.finish_date ?? DateTime.Now;
                    existing.progress = 100;
                }
            }
            else
            {
                var lessonUser = new LessonsUsers
                {
                    userID = user.userID,
                    lessonID = data.lessonID,
                    progress = data.progress,
                    time_spent = data.timeSpent,
                    last_accessed = DateTime.Now,
                    status = data.finished ? "completed" : "started",
                    start_date = DateTime.Now,
                    finish_date = data.finished ? DateTime.Now : null,
                    attempts = 1
                };
                _context.LessonsUsers.Add(lessonUser);
            }

            await _context.SaveChangesAsync();
            await _badgeService.CheckAndAwardBadges(user.userID);
            return Ok(new { message = "Progres actualizat cu succes" });
        }

        [HttpPost]
        public async Task<IActionResult> RestartLesson(int lessonID)
        {
            var username = HttpContext.Session.GetString("username");
            if (string.IsNullOrEmpty(username))
                return RedirectToAction("Login", "Account");

            var user = await _context.User.FirstOrDefaultAsync(u => u.username == username);
            if (user == null)
                return RedirectToAction("Login", "Account");

            var lessonUser = await _context.LessonsUsers
                .FirstOrDefaultAsync(lu => lu.userID == user.userID && lu.lessonID == lessonID);

            if (lessonUser == null)
            {
                lessonUser = new LessonsUsers
                {
                    userID = user.userID,
                    lessonID = lessonID,
                    status = "started",
                    start_date = DateTime.Now,
                    progress = 0,
                    attempts = 1,
                    time_spent = 0,
                    last_accessed = DateTime.Now
                };
                _context.LessonsUsers.Add(lessonUser);
            }
            else
            {
                lessonUser.progress = 0;
                lessonUser.status = "started";
                lessonUser.finish_date = null;
                lessonUser.start_date = DateTime.Now;
                lessonUser.last_accessed = DateTime.Now;
                lessonUser.attempts += 1;
                lessonUser.time_spent = 0;
            }

            await _context.SaveChangesAsync();
            await _badgeService.CheckAndAwardBadges(user.userID);

            return RedirectToAction("Lesson", new { id = lessonID });
        }

        public async Task<IActionResult> Create()
        {
            if (!await IsUserProfesor())
            {
                TempData["ErrorMessage"] = "Nu aveți permisiunea de a crea lecții";
                return RedirectToAction("Lessons");
            }

            ViewBag.Modules = await _context.Modules.ToListAsync();
            ViewBag.AvatarUrl = HttpContext.Session.GetString("avatarUrl") ?? "https://randomuser.me/api/portraits/lego/1.jpg";
            ViewBag.Username = HttpContext.Session.GetString("username") ?? "Utilizator";

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("lesson_title,lesson_content,moduleID")] Lesson lesson)
        {
            if (!await IsUserProfesor())
            {
                TempData["ErrorMessage"] = "Nu aveți permisiunea de a crea lecții";
                return RedirectToAction("Lessons");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    lesson.created_at = DateTime.Now;
                    _context.Add(lesson);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Lecția a fost creată cu succes!";
                    return RedirectToAction(nameof(Lessons));
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"A apărut o eroare: {ex.Message}");
                    Console.WriteLine(ex);
                }
            }

            ViewBag.Modules = await _context.Modules.ToListAsync();
            ViewBag.AvatarUrl = HttpContext.Session.GetString("avatarUrl") ?? "https://randomuser.me/api/portraits/lego/1.jpg";
            ViewBag.Username = HttpContext.Session.GetString("username") ?? "Utilizator";

            return View(lesson);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (!await IsUserProfesor())
            {
                TempData["ErrorMessage"] = "Nu aveți permisiunea de a edita lecții";
                return RedirectToAction("Lessons");
            }

            if (id == null)
            {
                return NotFound();
            }

            var lesson = await _context.Lesson.FindAsync(id);
            if (lesson == null)
            {
                return NotFound();
            }

            ViewBag.Modules = await _context.Modules.ToListAsync();
            ViewBag.AvatarUrl = HttpContext.Session.GetString("avatarUrl") ?? "https://randomuser.me/api/portraits/lego/1.jpg";
            ViewBag.Username = HttpContext.Session.GetString("username") ?? "Utilizator";

            return View(lesson);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("lessonID,lesson_title,lesson_content,moduleID,created_at")] Lesson lesson)
        {
            if (!await IsUserProfesor())
            {
                TempData["ErrorMessage"] = "Nu aveți permisiunea de a edita lecții";
                return RedirectToAction("Lessons");
            }

            if (id != lesson.lessonID)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(lesson);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Lecția a fost actualizată cu succes!";
                    return RedirectToAction(nameof(Lessons));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!LessonExists(lesson.lessonID))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            ViewBag.Modules = await _context.Modules.ToListAsync();
            ViewBag.AvatarUrl = HttpContext.Session.GetString("avatarUrl") ?? "https://randomuser.me/api/portraits/lego/1.jpg";
            ViewBag.Username = HttpContext.Session.GetString("username") ?? "Utilizator";

            return View(lesson);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (!await IsUserProfesor())
            {
                TempData["ErrorMessage"] = "Nu aveți permisiunea de a șterge lecții";
                return RedirectToAction("Lessons");
            }

            if (id == null)
            {
                return NotFound();
            }

            var lesson = await _context.Lesson
                .Include(l => l.Module)
                .Include(l => l.LessonTags)
                .Include(l => l.LessonsUsers)
                .FirstOrDefaultAsync(m => m.lessonID == id);

            if (lesson == null)
            {
                return NotFound();
            }

            ViewBag.HasDependencies = lesson.LessonsUsers.Any() || lesson.LessonTags.Any();
            ViewBag.DependentUsersCount = lesson.LessonsUsers.Count;
            ViewBag.DependentTagsCount = lesson.LessonTags.Count;

            ViewBag.AvatarUrl = HttpContext.Session.GetString("avatarUrl") ?? "https://randomuser.me/api/portraits/lego/1.jpg";
            ViewBag.Username = HttpContext.Session.GetString("username") ?? "Utilizator";

            return View(lesson);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (!await IsUserProfesor())
            {
                TempData["ErrorMessage"] = "Nu aveți permisiunea de a șterge lecții";
                return RedirectToAction("Lessons");
            }

            var lesson = await _context.Lesson
                .Include(l => l.LessonTags)
                .Include(l => l.LessonsUsers)
                .FirstOrDefaultAsync(l => l.lessonID == id);

            if (lesson == null)
            {
                TempData["ErrorMessage"] = "Lecția nu a fost găsită";
                return RedirectToAction("Lessons");
            }

            try
            {
                if (lesson.LessonTags.Any())
                {
                    _context.LessonTags.RemoveRange(lesson.LessonTags);
                }

                if (lesson.LessonsUsers.Any())
                {
                    _context.LessonsUsers.RemoveRange(lesson.LessonsUsers);
                }

                _context.Lesson.Remove(lesson);

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Lecția a fost ștearsă cu succes!";
                return RedirectToAction(nameof(Lessons));
            }
            catch (DbUpdateException ex)
            {
                Console.WriteLine($"Eroare la ștergere: {ex.InnerException?.Message ?? ex.Message}");
                TempData["ErrorMessage"] = $"A apărut o eroare la ștergere: {ex.InnerException?.Message ?? ex.Message}";
                return RedirectToAction(nameof(Delete), new { id });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Eroare neașteptată: {ex.Message}");
                TempData["ErrorMessage"] = $"A apărut o eroare neașteptată: {ex.Message}";
                return RedirectToAction(nameof(Delete), new { id });
            }
        }

        [HttpGet]
        public async Task<IActionResult> AllAccessedLessons()
        {
            var username = HttpContext.Session.GetString("username");
            if (string.IsNullOrEmpty(username))
                return RedirectToAction("Login", "Account");

            var user = await _context.User
                .Include(u => u.Profile)
                .FirstOrDefaultAsync(u => u.username == username);

            if (user == null)
                return RedirectToAction("Login", "Account");

            ViewBag.AvatarUrl = user?.Profile?.avatar_url ?? "https://randomuser.me/api/portraits/lego/1.jpg";
            ViewBag.Username = user?.username ?? "Utilizator";

            var accessedLessons = await _context.LessonsUsers
                .Where(lu => lu.userID == user.userID)
                .Include(lu => lu.Lesson)
                    .ThenInclude(l => l.Module)
                .Select(lu => new LessonProgressViewModel
                {
                    LessonID = lu.Lesson.lessonID,
                    LessonTitle = lu.Lesson.lesson_title,
                    LessonContent = lu.Lesson.lesson_content,
                    Progress = lu.progress,
                    Status = lu.status,
                    Start_Date = lu.start_date,
                    Finish_Date = lu.finish_date,
                    Time_Spent = lu.time_spent,
                    Attempts = lu.attempts,
                    Last_Accessed = lu.last_accessed,
                    ModuleName = lu.Lesson.Module.module_name,
                    ModuleID = lu.Lesson.moduleID
                })
                .ToListAsync();

            return View(accessedLessons);
        }

        private bool LessonExists(int id)
        {
            return _context.Lesson.Any(e => e.lessonID == id);
        }
    }
}
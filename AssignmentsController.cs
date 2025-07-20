using Achieve_Plus.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Achieve_Plus.Controllers
{
    public class AssignmentsController : Controller
    {
        private readonly AppDbContext _context;

        public AssignmentsController(AppDbContext context)
        {
            _context = context;
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

        private async Task<bool> HasUserStartedLesson(int userId, int lessonId)
        {
            var lessonUser = await _context.LessonsUsers
                .FirstOrDefaultAsync(lu => lu.userID == userId && lu.lessonID == lessonId);

            return lessonUser != null && lessonUser.status != "not_started";
        }

        public async Task<IActionResult> Assignments()
        {
            var username = HttpContext.Session.GetString("username");
            var user = await _context.User
                .Include(u => u.Profile)
                .FirstOrDefaultAsync(u => u.username == username);

            ViewBag.AvatarUrl = user?.Profile?.avatar_url ?? "https://randomuser.me/api/portraits/lego/1.jpg";
            ViewBag.Username = user?.username ?? "Utilizator";
            ViewBag.IsTeacher = await IsUserProfesor();

            var userId = await GetCurrentUserIdAsync();
            ViewBag.CurrentUserId = userId;

            var assignmentsQuery = _context.Assignments
                .Include(a => a.Lesson)
                .Include(a => a.Submissions)
                    .ThenInclude(s => s.User)
                .AsQueryable();

            if (!await IsUserProfesor() && userId.HasValue)
            {
                assignmentsQuery = assignmentsQuery.Where(a => 
                    _context.LessonsUsers.Any(lu => 
                        lu.userID == userId.Value && 
                        lu.lessonID == a.lessonID && 
                        lu.status != "not_started"));
            }

            var filteredAssignments = await assignmentsQuery.ToListAsync();

            return View(filteredAssignments);
        }

        public async Task<IActionResult> Create()
        {
            if (!await IsUserProfesor())
            {
                TempData["ErrorMessage"] = "Nu aveți permisiunea de a crea teme";
                return RedirectToAction("Assignments");
            }

            ViewBag.Lessons = await _context.Lesson.ToListAsync();
            ViewBag.AvatarUrl = HttpContext.Session.GetString("avatarUrl") ?? "https://randomuser.me/api/portraits/lego/1.jpg";
            ViewBag.Username = HttpContext.Session.GetString("username") ?? "Utilizator";

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("title,description,start_date,due_date,max_score,lessonID")] Assignment assignment)
        {
            if (!await IsUserProfesor())
            {
                TempData["ErrorMessage"] = "Nu aveți permisiunea de a crea teme";
                return RedirectToAction("Assignments");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Add(assignment);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Tema a fost creată cu succes!";
                    return RedirectToAction(nameof(Assignments));
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"A apărut o eroare: {ex.Message}");
                    Console.WriteLine(ex);
                }
            }

            ViewBag.Lessons = await _context.Lesson.ToListAsync();
            ViewBag.AvatarUrl = HttpContext.Session.GetString("avatarUrl") ?? "https://randomuser.me/api/portraits/lego/1.jpg";
            ViewBag.Username = HttpContext.Session.GetString("username") ?? "Utilizator";

            return View(assignment);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (!await IsUserProfesor())
            {
                TempData["ErrorMessage"] = "Nu aveți permisiunea de a edita teme";
                return RedirectToAction("Assignments");
            }

            if (id == null)
            {
                return NotFound();
            }

            var assignment = await _context.Assignments.FindAsync(id);
            if (assignment == null)
            {
                return NotFound();
            }

            ViewBag.Lessons = await _context.Lesson.ToListAsync();
            ViewBag.AvatarUrl = HttpContext.Session.GetString("avatarUrl") ?? "https://randomuser.me/api/portraits/lego/1.jpg";
            ViewBag.Username = HttpContext.Session.GetString("username") ?? "Utilizator";

            return View(assignment);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("assignmentID,title,description,start_date,due_date,max_score,lessonID,created_at")] Assignment assignment)
        {
            if (!await IsUserProfesor())
            {
                TempData["ErrorMessage"] = "Nu aveți permisiunea de a edita teme";
                return RedirectToAction("Assignments");
            }

            if (id != assignment.assignmentID)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(assignment);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Tema a fost actualizată cu succes!";
                    return RedirectToAction(nameof(Assignments));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!AssignmentExists(assignment.assignmentID))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            ViewBag.Lessons = await _context.Lesson.ToListAsync();
            ViewBag.AvatarUrl = HttpContext.Session.GetString("avatarUrl") ?? "https://randomuser.me/api/portraits/lego/1.jpg";
            ViewBag.Username = HttpContext.Session.GetString("username") ?? "Utilizator";

            return View(assignment);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (!await IsUserProfesor())
            {
                TempData["ErrorMessage"] = "Nu aveți permisiunea de a șterge teme";
                return RedirectToAction("Assignments");
            }

            if (id == null)
            {
                return NotFound();
            }

            var assignment = await _context.Assignments
                .Include(a => a.Lesson)
                .FirstOrDefaultAsync(m => m.assignmentID == id);

            if (assignment == null)
            {
                return NotFound();
            }

            ViewBag.AvatarUrl = HttpContext.Session.GetString("avatarUrl") ?? "https://randomuser.me/api/portraits/lego/1.jpg";
            ViewBag.Username = HttpContext.Session.GetString("username") ?? "Utilizator";

            return View(assignment);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (!await IsUserProfesor())
            {
                TempData["ErrorMessage"] = "Nu aveți permisiunea de a șterge teme";
                return RedirectToAction("Assignments");
            }

            var assignment = await _context.Assignments.FindAsync(id);
            if (assignment != null)
            {
                try
                {
                    _context.Assignments.Remove(assignment);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Tema a fost ștearsă cu succes!";
                }
                catch (DbUpdateException ex)
                {
                    TempData["ErrorMessage"] = "Nu se poate șterge această temă deoarece are trimiteri asociate. Ștergeți mai întâi trimiterile.";
                    Console.WriteLine(ex.Message);
                    return RedirectToAction(nameof(Delete), new { id = id });
                }
            }

            return RedirectToAction(nameof(Assignments));
        }

        public async Task<IActionResult> Assignment(int id)
        {
            var username = HttpContext.Session.GetString("username");
            var user = await _context.User
                .Include(u => u.Profile)
                .FirstOrDefaultAsync(u => u.username == username);

            ViewBag.AvatarUrl = user?.Profile?.avatar_url ?? "https://randomuser.me/api/portraits/lego/1.jpg";
            ViewBag.Username = user?.username ?? "Utilizator";
            ViewBag.IsTeacher = await IsUserProfesor();

            var userId = await GetCurrentUserIdAsync();
            ViewBag.CurrentUserId = userId;

            var assignment = await _context.Assignments
                .Include(a => a.Lesson)
                .Include(a => a.Submissions)
                    .ThenInclude(s => s.User)
                .FirstOrDefaultAsync(a => a.assignmentID == id);

            if (assignment == null)
            {
                return NotFound();
            }

            if (!await IsUserProfesor() && userId.HasValue && 
                !await HasUserStartedLesson(userId.Value, assignment.lessonID))
            {
                TempData["ErrorMessage"] = "Nu puteți accesa această temă deoarece nu ați început lecția asociată.";
                return RedirectToAction("Assignments");
            }

            var mySubmission = assignment.Submissions.FirstOrDefault(s => s.userID == userId);
            ViewBag.MySubmission = mySubmission;

            return View(assignment);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadSubmission(int assignmentId, IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return Json(new { success = false, message = "Vă rugăm să selectați un fișier valid." });
                }

                if (file.Length > 5 * 1024 * 1024) // 5MB max
                {
                    return Json(new { success = false, message = "Fișierul este prea mare (maxim 5MB permis)." });
                }

                var allowedExtensions = new[] { ".pdf", ".doc", ".docx", ".txt", ".zip", ".png", ".jpg", ".jpeg" };
                var fileExtension = Path.GetExtension(file.FileName).ToLower();

                if (!allowedExtensions.Contains(fileExtension))
                {
                    return Json(new
                    {
                        success = false,
                        message = "Format fișier neacceptat. Formate permise: PDF, DOC, DOCX, TXT, ZIP, PNG, JPG, JPEG."
                    });
                }

                var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Uploads");
                if (!Directory.Exists(uploadsPath))
                {
                    Directory.CreateDirectory(uploadsPath);
                }

                var uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
                var filePath = Path.Combine(uploadsPath, uniqueFileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                var userId = await GetCurrentUserIdAsync();
                if (userId == null)
                {
                    return Json(new { success = false, message = "Utilizatorul nu a fost găsit." });
                }

                var existingSubmission = await _context.UserSubmissions
                    .FirstOrDefaultAsync(s => s.assignmentID == assignmentId && s.userID == userId);

                if (existingSubmission != null)
                {
                    existingSubmission.submission_date = DateTime.Now;
                    existingSubmission.file_path = uniqueFileName;
                    existingSubmission.status = "pending";
                }
                else
                {
                    var submission = new UserSubmission
                    {
                        userID = userId.Value,
                        assignmentID = assignmentId,
                        submission_date = DateTime.Now,
                        file_path = uniqueFileName,
                        status = "pending"
                    };

                    _context.UserSubmissions.Add(submission);
                }

                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Tema a fost încărcată cu succes!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Eroare la încărcare: {ex.Message}" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> GradeSubmission([FromBody] GradeSubmissionModel model)
        {
            if (!await IsUserProfesor())
            {
                return Json(new { success = false, message = "Nu aveți permisiunea de a evalua teme" });
            }

            try
            {
                if (model.SubmissionId <= 0)
                {
                    return Json(new
                    {
                        success = false,
                        message = "ID-ul trimiterii este invalid"
                    });
                }

                var submission = await _context.UserSubmissions
                    .Include(s => s.Assignment)
                    .FirstOrDefaultAsync(s => s.submissionID == model.SubmissionId);

                if (submission == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = $"Trimiterea cu ID-ul {model.SubmissionId} nu a fost găsită"
                    });
                }

                if (model.Grade < 0 || model.Grade > submission.Assignment.max_score)
                {
                    return Json(new
                    {
                        success = false,
                        message = $"Nota trebuie să fie între 0 și {submission.Assignment.max_score}"
                    });
                }

                submission.grade = model.Grade;
                submission.comments = model.Feedback;
                submission.status = "graded";

                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = "Tema a fost evaluată cu succes!",
                    submissionId = submission.submissionID
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = $"Eroare la evaluare: {ex.Message}"
                });
            }
        }

        public class GradeSubmissionModel
        {
            public int SubmissionId { get; set; }
            public decimal Grade { get; set; }
            public string Feedback { get; set; }
        }

        private bool AssignmentExists(int id)
        {
            return _context.Assignments.Any(e => e.assignmentID == id);
        }

        private async Task<int?> GetCurrentUserIdAsync()
        {
            var username = HttpContext.Session.GetString("username");
            var user = await _context.User.FirstOrDefaultAsync(u => u.username == username);
            return user?.userID;
        }
    }
}
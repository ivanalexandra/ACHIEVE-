using Microsoft.AspNetCore.Mvc;
using Achieve_Plus.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using System;
using System.Linq;
using System.IO;

namespace Achieve_Plus.Controllers
{
    public class ProfileController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _hostingEnvironment;

        public ProfileController(AppDbContext context, IWebHostEnvironment hostingEnvironment)
        {
            _context = context;
            _hostingEnvironment = hostingEnvironment;
        }

        [HttpGet]
        public IActionResult Profile()
        {
            var username = HttpContext.Session.GetString("username");

            if (string.IsNullOrEmpty(username))
            {
                return RedirectToAction("Login", "Login");
            }

            var user = _context.User
                        .Include(u => u.LessonsUsers)
                        .Include(u => u.Profile)
                        .FirstOrDefault(u => u.username == username);

            if (user == null)
            {
                return NotFound("Utilizatorul nu a fost găsit.");
            }

            return View(user);
        }

        [HttpPost]
        public IActionResult UpdateFullName([FromForm] string fullName)
        {
            var username = HttpContext.Session.GetString("username");
            if (string.IsNullOrEmpty(username))
            {
                return Unauthorized("Nu ești autentificat.");
            }

            if (string.IsNullOrWhiteSpace(fullName) || fullName.Length > 100)
            {
                return BadRequest("Numele este invalid sau prea lung.");
            }

            var user = _context.User.Include(u => u.Profile).FirstOrDefault(u => u.username == username);
            if (user == null)
            {
                return NotFound("Utilizatorul nu a fost găsit.");
            }

            if (user.Profile == null)
            {
                user.Profile = new Profile
                {
                    userID = user.userID,
                    full_name = fullName
                };
                _context.Profile.Add(user.Profile);
            }
            else
            {
                user.Profile.full_name = fullName;
            }

            _context.SaveChanges();

            return Ok(new { message = "Numele a fost actualizat cu succes.", fullName = fullName });
        }

        [HttpPost]
        public IActionResult UpdateAvatar()
        {
            var username = HttpContext.Session.GetString("username");
            if (string.IsNullOrEmpty(username))
            {
                return Unauthorized("Nu ești autentificat.");
            }

            var file = Request.Form.Files["avatar"];
            if (file == null || file.Length == 0)
            {
                return BadRequest("Nu a fost selectată nicio imagine.");
            }

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
            {
                return BadRequest("Format de fișier neacceptat. Folosește JPG, JPEG, PNG sau GIF.");
            }

            if (file.Length > 5 * 1024 * 1024)
            {
                return BadRequest("Fișierul este prea mare. Dimensiunea maximă permisă este 5MB.");
            }

            var user = _context.User.Include(u => u.Profile).FirstOrDefault(u => u.username == username);
            if (user == null)
            {
                return NotFound("Utilizatorul nu a fost găsit.");
            }

            if (user.Profile == null)
            {
                user.Profile = new Profile { userID = user.userID };
                _context.Profile.Add(user.Profile);
            }

            var uploadsFolder = Path.Combine(_hostingEnvironment.WebRootPath, "images", "avatars");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var uniqueFileName = $"{user.userID}_{DateTime.Now:yyyyMMddHHmmss}{extension}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                file.CopyTo(fileStream);
            }

            var avatarUrl = $"/images/avatars/{uniqueFileName}";
            user.Profile.avatar_url = avatarUrl;
            _context.SaveChanges();

            return Ok(new { avatarUrl = avatarUrl });
        }

        [HttpPost]
        public IActionResult DeleteAccount()
        {
            var username = HttpContext.Session.GetString("username");
            if (string.IsNullOrEmpty(username))
            {
                return Unauthorized("Nu ești autentificat.");
            }

            var user = _context.User.Include(u => u.Profile).FirstOrDefault(u => u.username == username);
            if (user == null)
            {
                return NotFound("Utilizatorul nu a fost găsit.");
            }

            if (user.Profile != null)
            {
                _context.Profile.Remove(user.Profile);
            }

            _context.User.Remove(user);
            _context.SaveChanges();

            HttpContext.Session.Clear();
            return Ok(new { redirectUrl = Url.Action("Login", "Login") });
        }
    }
}

using Microsoft.AspNetCore.Mvc;
using BCrypt.Net;
using Achieve_Plus.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using Microsoft.AspNetCore.Http;

namespace Achieve_Plus.Controllers
{
    public class LoginController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ILogger<LoginController> _logger;

        public LoginController(AppDbContext context, ILogger<LoginController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public IActionResult RegisterAjax([FromForm] User user)
        {
            try
            {
                _logger.LogInformation("Start RegisterAjax");

                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors)
                                                .Select(e => e.ErrorMessage)
                                                .ToList();
                    _logger.LogWarning("ModelState invalid: {errors}", string.Join(" | ", errors));
                    return Json(new { success = false, message = "Datele introduse nu sunt valide: " + string.Join(" | ", errors) });
                }


                if (string.IsNullOrEmpty(user.username) || user.username.Length > 25 ||
                    !System.Text.RegularExpressions.Regex.IsMatch(user.username, @"^[a-zA-Z0-9_]+$"))
                {
                    _logger.LogWarning("Username invalid: {username}", user.username);
                    return Json(new { success = false, message = "Username invalid. Folosește doar litere, cifre sau '_' și maximum 25 de caractere." });
                }

                if (string.IsNullOrEmpty(user.email) ||
                    !System.Text.RegularExpressions.Regex.IsMatch(user.email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                {
                    _logger.LogWarning("Email invalid: {email}", user.email);
                    return Json(new { success = false, message = "Emailul introdus nu este valid." });
                }

                if (string.IsNullOrEmpty(user.password_hash) || user.password_hash.Length > 50 || user.password_hash.Length < 3)
                {
                    _logger.LogWarning("Parola invalidă: lungime {length}", user.password_hash?.Length);
                    return Json(new { success = false, message = "Parola nu este valida." });
                }

                var existingUser = _context.User.FirstOrDefault(u =>
                                       u.username == user.username || u.email == user.email);

                if (existingUser != null)
                {
                    _logger.LogWarning("User existent cu același username/email");
                    return Json(new { success = false, message = "Username sau email deja utilizat" });
                }

                user.password_hash = BCrypt.Net.BCrypt.HashPassword(user.password_hash);
                user.created_at = DateTime.Now;

                if (string.IsNullOrEmpty(user.role)) user.role = "Student";

                _context.User.Add(user);
                _context.SaveChanges();
               
                var profile = new Profile
                {
                    userID = user.userID,
                    full_name = "",
                    avatar_url = "/images/default-avatar.png"
                };

                _context.Profile.Add(profile);
                _context.SaveChanges();
               
                HttpContext.Session.SetString("username", user.username);
                HttpContext.Session.SetString("role", user.role);

                _logger.LogInformation("Utilizator și profil înregistrate: {username}", user.username);

                string? redirectUrl = user.role == "Profesor"
                    ? Url.Action("TeacherDashboard", "Dashboard")
                    : Url.Action("Dashboard", "Dashboard");

                return Json(new
                {
                    success = true,
                    message = "Înregistrare cu succes!",
                    redirectUrl = redirectUrl
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Excepție în RegisterAjax");
                return Json(new
                {
                    success = false,
                    message = "Eroare la înregistrare: " + ex.Message
                });
            }
        }

        [HttpPost]

        public IActionResult LoginAjax([FromForm] string username, [FromForm] string password)
        {
            try
            {
                _logger.LogInformation("Start LoginAjax");

                if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                {
                    return Json(new
                    {
                        success = false,
                        message = "Username și parola sunt obligatorii."
                    });
                }

                if (username.Length > 25 || password.Length > 50)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Username sau parola invalida."
                    });
                }

                var user = _context.User.FirstOrDefault(u => u.username == username);

                if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.password_hash))
                {
                    _logger.LogWarning("Autentificare eșuată pentru: {username}", username);
                    return Json(new
                    {
                        success = false,
                        message = "Username sau parolă incorectă."
                    });
                }

                user.last_login = DateTime.Now;

                user.LoginCount++; 

                _context.SaveChanges();

    
                HttpContext.Session.SetString("username", user.username);
                HttpContext.Session.SetString("role", user.role);

                _logger.LogInformation("Autentificare reușită: {username}. Login count: {count}", username, user.LoginCount);

                string? redirectUrl = user.role == "Profesor"
                    ? Url.Action("TeacherDashboard", "Dashboard")
                    : Url.Action("Dashboard", "Dashboard");

                return Json(new
                {
                    success = true,
                    message = "Autentificare reușită!",
                    redirectUrl = redirectUrl
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Eroare la actualizarea autentificării");
                return Json(new
                {
                    success = false,
                    message = "Eroare la autentificare: " + ex.Message
                });
            }
        }

        [HttpPost]
        public IActionResult ResetPasswordAjax([FromForm] string email, [FromForm] string newPassword, [FromForm] string confirmPassword)
        {
            try
            {
                _logger.LogInformation("Start ResetPasswordAjax");

                if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(newPassword) || string.IsNullOrEmpty(confirmPassword))
                {
                    return Json(new
                    {
                        success = false,
                        message = "Toate câmpurile sunt obligatorii."
                    });
                }

                if (!System.Text.RegularExpressions.Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                {
                    return Json(new
                    {
                        success = false,
                        message = "Emailul nu este valid."
                    });
                }

                if (newPassword != confirmPassword)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Parolele nu se potrivesc"
                    });
                }

                if (newPassword.Length > 50)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Parola este prea lungă. Maximum 50 caractere."
                    });
                }

                if (newPassword.Length < 3)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Parola este prea scurtă. Minimum 3 caractere."
                    });
                }

                var user = _context.User.FirstOrDefault(u => u.email == email);
                if (user == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Nu există un utilizator cu acest email."
                    });
                }

                user.password_hash = BCrypt.Net.BCrypt.HashPassword(newPassword);
                _context.SaveChanges();

                _logger.LogInformation("Parolă resetată pentru: {email}", email);
                return Json(new
                {
                    success = true,
                    message = "Parola a fost resetată cu succes!",
                    redirectUrl = "/Login"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Eroare la resetarea parolei");
                return Json(new
                {
                    success = false,
                    message = "Eroare la resetarea parolei: " + ex.Message
                });
            }
        }

        [HttpPost]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return Json(new { success = true, redirectUrl = "/Login" });
        }
    }
    
}
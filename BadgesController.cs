using Achieve_Plus.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace Achieve_Plus.Controllers
{
    public class BadgesController : Controller
    {
        private readonly AppDbContext _context;

        public BadgesController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Badges()
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

            ViewBag.AvatarUrl = !string.IsNullOrEmpty(user.Profile?.avatar_url) 
                ? user.Profile.avatar_url 
                : "/images/default-avatar.png";
            ViewBag.Username = user.username;

            var allBadges = await _context.Badges
                .Select(b => new Badge 
                {
                    badgeID = b.badgeID,
                    name = b.name,
                    description = b.description,
                    created_at = b.created_at,
                    icon_path = b.icon_path.StartsWith("http") || b.icon_path.StartsWith("/") 
                               ? b.icon_path 
                               : $"/images/badges/{b.icon_path}"
                })
                .ToListAsync();

            var earnedBadgeIds = await _context.UserBadges
                .Where(ub => ub.userID == user.userID)
                .Select(ub => ub.badgeID)
                .ToListAsync();

            ViewBag.EarnedBadgeIds = earnedBadgeIds;

            return View(allBadges);
        }
    }
}
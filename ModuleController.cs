using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Threading.Tasks;
using Achieve_Plus.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;

public class ModulesController : Controller
{
    private readonly AppDbContext _context;

    public ModulesController(AppDbContext context)
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

    public async Task<IActionResult> Modules()
    {
        var username = HttpContext.Session.GetString("username");
        var user = await _context.User
                .Include(u => u.Profile)
                .FirstOrDefaultAsync(u => u.username == username);
        
        ViewBag.AvatarUrl = user?.Profile?.avatar_url ?? "https://randomuser.me/api/portraits/lego/1.jpg";
        ViewBag.Username = user?.username ?? "Utilizator";
        ViewBag.IsTeacher = await IsUserProfesor();

        var modules = await _context.Modules
            .Include(m => m.Lessons)
            .ToListAsync();

        if (!await IsUserProfesor() && user != null)
        {
            var userProgress = await _context.LessonsUsers
                .Where(ul => ul.userID == user.userID)
                .ToListAsync();

            ViewBag.UserProgress = userProgress;
        }

        return View(modules);
    }

    public async Task<IActionResult> Module(int? id)
    {
        var username = HttpContext.Session.GetString("username");
        var user = await _context.User
                .Include(u => u.Profile)
                .FirstOrDefaultAsync(u => u.username == username);
        ViewBag.AvatarUrl = user?.Profile?.avatar_url ?? "https://randomuser.me/api/portraits/lego/1.jpg";
        ViewBag.Username = user?.username ?? "Utilizator";
        ViewBag.IsTeacher = await IsUserProfesor();
        
        if (id == null)
        {
            return NotFound();
        }

        var module = await _context.Modules
            .Include(m => m.Lessons)
            .FirstOrDefaultAsync(m => m.moduleID == id);

        if (module == null)
        {
            return NotFound();
        }

        return View(module);
    }

    public async Task<IActionResult> Create()
    {
        if (!await IsUserProfesor())
        {
            TempData["ErrorMessage"] = "Nu aveți permisiunea de a accesa această pagină";
            return RedirectToAction("Modules");
        }
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("module_name,module_description")] Module module)
    {
        if (!await IsUserProfesor())
        {
            TempData["ErrorMessage"] = "Nu aveți permisiunea de a crea module";
            return RedirectToAction("Modules");
        }

        if (ModelState.IsValid)
        {
            try
            {
                module.created_at = DateTime.Now;
                _context.Add(module);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Modul creat cu succes!";
                return RedirectToAction(nameof(Modules));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"A apărut o eroare: {ex.Message}");
                Console.WriteLine(ex);
            }
        }

        return View(module);
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (!await IsUserProfesor())
        {
            TempData["ErrorMessage"] = "Nu aveți permisiunea de a edita module";
            return RedirectToAction("Modules");
        }

        if (id == null)
        {
            return NotFound();
        }

        var module = await _context.Modules.FindAsync(id);
        if (module == null)
        {
            return NotFound();
        }
        return View(module);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("moduleID,module_name,module_description,created_at")] Module module)
    {
        if (!await IsUserProfesor())
        {
            TempData["ErrorMessage"] = "Nu aveți permisiunea de a edita module";
            return RedirectToAction("Modules");
        }

        if (id != module.moduleID)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(module);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Modul actualizat cu succes!";
                return RedirectToAction(nameof(Modules));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ModuleExists(module.moduleID))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
        }
        return View(module);
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (!await IsUserProfesor())
        {
            TempData["ErrorMessage"] = "Nu aveți permisiunea de a șterge module";
            return RedirectToAction("Modules");
        }

        if (id == null)
        {
            return NotFound();
        }

        var module = await _context.Modules
            .FirstOrDefaultAsync(m => m.moduleID == id);
        if (module == null)
        {
            return NotFound();
        }

        return View(module);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        if (!await IsUserProfesor())
        {
            TempData["ErrorMessage"] = "Nu aveți permisiunea de a șterge module";
            return RedirectToAction("Modules");
        }

        var module = await _context.Modules.FindAsync(id);
        if (module != null)
        {
            try
            {
                _context.Modules.Remove(module);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Modul a fost șters cu succes!";
            }
            catch (DbUpdateException ex)
            {
                TempData["ErrorMessage"] = "Nu se poate șterge acest modul deoarece are lecții asociate. Ștergeți mai întâi lecțiile.";
                Console.WriteLine(ex.Message);
                return RedirectToAction(nameof(Delete), new { id = id });
            }
        }
        return RedirectToAction(nameof(Modules));
    }

    private bool ModuleExists(int id)
    {
        return _context.Modules.Any(e => e.moduleID == id);
    }
}
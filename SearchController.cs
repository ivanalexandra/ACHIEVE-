using Microsoft.AspNetCore.Mvc;
using Achieve_Plus.Models; 
using System.Linq;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.EntityFrameworkCore;

namespace Achieve_Plus.Controllers
{
    public class SearchController : Controller
    {
        private readonly AppDbContext _context;

        public SearchController(AppDbContext context)
        {
            _context = context;
        }

        public IActionResult Search(string query)
        {
            var lessons = _context.Lesson
                .Where(l => l.lesson_title.Contains(query))
                .ToList();

            var modules = _context.Modules
                .Where(m => m.module_name.Contains(query))
                .Include(m => m.Lessons) 
                .ToList();

            var model = new SearchResultsViewModel
            {
                SearchTerm = query,
                Lessons = lessons,
                Modules = modules
            };

            return View(model);
        }


    }
}

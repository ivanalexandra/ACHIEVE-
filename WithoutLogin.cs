using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Achieve_Plus.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
public class WithoutLoginController : Controller
{
    private readonly AppDbContext _context;

    public WithoutLoginController(AppDbContext context)
    {
        _context = context;
    }
    public async Task<IActionResult> Lessons()
    {
        var lessons = await _context.Lesson.ToListAsync();
        return View(lessons);
    }

    public async Task<IActionResult> Modules()
    {
         var modules = _context.Modules
        .Include(m => m.Lessons) 
        .ToList();
        return View(modules);
    }
}
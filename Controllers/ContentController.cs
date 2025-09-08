using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VideoLibrary.Models;

namespace YourApp.Controllers
{
    public class ContentController : Controller
    {
        private readonly AppDbContext _context; // Replace with your actual DbContext class name

        public ContentController(AppDbContext context)
        {
            _context = context;
        }

        // GET: Content
        public async Task<IActionResult> Index()
        {
            var contents = await _context.Contents
                .OrderByDescending(c => c.DateLastUpdated)
                .ToListAsync();

            return View(contents);
        }

        // GET: Content/View/5
        public async Task<IActionResult> View(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var content = await _context.Contents.FindAsync(id);
            if (content == null)
            {
                return NotFound();
            }

            return View(content);
        }

        // GET: Content/Create
        public IActionResult Create()
        {
            return View();
        }

        // GET: Content/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var content = await _context.Contents.FindAsync(id);
            if (content == null)
            {
                return NotFound();
            }

            return View(content);
        }

        // POST: Content/Save
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(Content content)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    if (content.Id == 0)
                    {
                        // New content
                        content.DateCreated = DateTime.Now;
                        content.DateLastUpdated = DateTime.Now;
                        _context.Add(content);
                        TempData["SuccessMessage"] = "Content created successfully.";
                    }
                    else
                    {
                        // Existing content
                        var existingContent = await _context.Contents.FindAsync(content.Id);
                        if (existingContent == null)
                        {
                            return NotFound();
                        }

                        existingContent.Title = content.Title;
                        existingContent.ContentText = content.ContentText;
                        existingContent.DateLastUpdated = DateTime.Now;

                        _context.Update(existingContent);
                        TempData["SuccessMessage"] = "Content updated successfully.";
                    }

                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = "An error occurred while saving the content.";
                    // Log the exception here if you have logging configured
                }
            }

            // If we got this far, something failed, redisplay form
            if (content.Id == 0)
            {
                return View("Create", content);
            }
            else
            {
                return View("Edit", content);
            }
        }

        // POST: Content/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var content = await _context.Contents.FindAsync(id);
                if (content != null)
                {
                    _context.Contents.Remove(content);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Content deleted successfully.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Content not found.";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred while deleting the content.";
                // Log the exception here if you have logging configured
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
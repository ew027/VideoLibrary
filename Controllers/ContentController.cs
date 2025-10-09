using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VideoLibrary.Models;
using VideoLibrary.Models.ViewModels;

namespace YourApp.Controllers
{
    [Authorize]
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
                .Include(c => c.ContentTags)
                .ThenInclude(ct => ct.Tag)
                .OrderByDescending(c => c.DateLastUpdated)
                .ToListAsync();

            return View(contents);
        }

        // GET: Content/View/5
        public async Task<IActionResult> View(int? id, int? tagId)
        {
            if (id == null)
            {
                return NotFound();
            }

            var content = await _context.Contents.Include(c => c.ContentTags)
                                                 .ThenInclude(ct => ct.Tag)
                                                 .FirstOrDefaultAsync(m => m.Id == id);


            if (content == null)
            {
                return NotFound();
            }

            Tag? tag = null;

            if (tagId != null)
            {
                tag = await _context.Tags.FindAsync(tagId);
            }

            return View(new ContentViewModel { Content = content, Tag = tag });
        }

        public async Task<IActionResult> EmbeddedView(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var content = await _context.Contents.FirstOrDefaultAsync(m => m.Id == id);

            if (content == null)
            {
                return NotFound();
            }

            return PartialView("_EmbeddedView", content);
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

            var content = await _context.Contents
                .Include(x => x.ContentTags)
                .ThenInclude(x => x.Tag)
                .FirstOrDefaultAsync(x => x.Id == id);

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

        [HttpPost]
        public async Task<IActionResult> UpdateTags(int contentId, string selectedTags, string newTags)
        {
            List<int> tagList = new List<int>();
            List<string> newTagList = new List<string>();

            if (!string.IsNullOrEmpty(selectedTags))
            {
                try
                {
                    tagList = selectedTags.Split(',').Select(t => Convert.ToInt32(t)).ToList();
                }
                catch (FormatException)
                {
                    return BadRequest("Invalid tag format in selected tags");
                }
            }

            if (!string.IsNullOrEmpty(newTags))
            {
                // Split new tags by comma, trim whitespace, and filter out empty strings
                newTagList = newTags.Split(',').Select(t => t.Trim()).Where(t => !string.IsNullOrWhiteSpace(t)).ToList();
            }

            var content = await _context.Contents
                .Include(v => v.ContentTags)
                .FirstOrDefaultAsync(v => v.Id == contentId);

            if (content == null)
            {
                return NotFound();
            }

            // deal with any new tags
            foreach (var newTag in newTagList)
            {
                var existingTag = await _context.Tags.FirstOrDefaultAsync(t => t.Name == newTag);
                if (existingTag == null)
                {
                    existingTag = new Tag { Name = newTag };
                    _context.Tags.Add(existingTag);
                    await _context.SaveChangesAsync();
                }

                tagList.Add(existingTag.Id);
            }

            // Remove existing tags
            _context.ContentTags.RemoveRange(content.ContentTags);

            // Add new tags
            foreach (var tagId in tagList)
            {
                content.ContentTags.Add(new ContentTag { ContentId = contentId, TagId = tagId });
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(View), new { id = contentId });
        }

        public async Task<IActionResult> Search(string q, int contextLength = 100)
        {
            var contents = await _context.Contents
                .Where(c => EF.Functions.ILike(c.ContentText, $"%{q}%"))
                .ToListAsync();

            var results = contents.Select(c => new ContentWithContextViewModel
            {
                Content = c,
                MatchingSnippets = ExtractSnippets(c.ContentText, q, contextLength)
            }).ToList();

            ViewBag.SearchTerm = q;

            return View("SearchResults", results);
        }

        private List<string> ExtractSnippets(string text, string searchTerm, int contextLength)
        {
            var snippets = new List<string>();
            var lowerText = text.ToLower();
            var lowerSearchTerm = searchTerm.ToLower();
            var index = 0;

            while ((index = lowerText.IndexOf(lowerSearchTerm, index)) != -1)
            {
                var start = Math.Max(0, index - contextLength / 2);
                var end = Math.Min(text.Length, index + searchTerm.Length + contextLength / 2);

                var snippet = text.Substring(start, end - start);
                if (start > 0) snippet = "..." + snippet;
                if (end < text.Length) snippet = snippet + "...";

                snippets.Add(snippet);
                index += searchTerm.Length;
            }

            return snippets.Distinct().Take(3).ToList(); // Limit to 3 snippets per video
        }
    }
}
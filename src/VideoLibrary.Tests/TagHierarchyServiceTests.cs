using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using VideoLibrary.Models;
using VideoLibrary.Services;
using Xunit;

namespace VideoLibrary.Tests.Services
{
    /// <summary>
    /// Unit tests for TagHierarchyService using EF Core InMemory provider
    /// </summary>
    public class TagHierarchyServiceTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly TagHierarchyService _service;
        private readonly ILogger<TagHierarchyService> _logger;

        public TagHierarchyServiceTests()
        {
            // Setup InMemory database with unique name for each test
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new AppDbContext(options);

            // Setup mock logger
            var mockLogger = new Mock<ILogger<TagHierarchyService>>();
            _logger = mockLogger.Object;

            _service = new TagHierarchyService(_context, _logger);
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        #region Insert Tests

        [Fact]
        public async Task InsertTagAsync_CreatesRootTag_WithCorrectValues()
        {
            // Act
            var tag = await _service.InsertTagAsync("Movies", null);

            // Assert
            Assert.NotNull(tag);
            Assert.Equal("Movies", tag.Name);
            Assert.Equal(1, tag.Left);
            Assert.Equal(2, tag.Right);
            Assert.Equal(0, tag.Level);
            Assert.Null(tag.ParentId);
        }

        [Fact]
        public async Task InsertTagAsync_CreatesChildTag_WithCorrectValues()
        {
            // Arrange
            var parent = await _service.InsertTagAsync("Movies", null);

            // Act
            var child = await _service.InsertTagAsync("Action", parent.Id);

            // Assert
            Assert.NotNull(child);
            Assert.Equal("Action", child.Name);
            Assert.Equal(2, child.Left);
            Assert.Equal(3, child.Right);
            Assert.Equal(1, child.Level);
            Assert.Equal(parent.Id, child.ParentId);

            // Verify parent was updated
            var updatedParent = await _context.Tags.FindAsync(parent.Id);
            Assert.Equal(1, updatedParent.Left);
            Assert.Equal(4, updatedParent.Right);
        }

        [Fact]
        public async Task InsertTagAsync_CreatesMultipleRootTags_InSequence()
        {
            // Act
            var movies = await _service.InsertTagAsync("Movies", null);
            var tv = await _service.InsertTagAsync("TV", null);
            var shorts = await _service.InsertTagAsync("Shorts", null);

            // Assert
            Assert.Equal(1, movies.Left);
            Assert.Equal(2, movies.Right);

            Assert.Equal(3, tv.Left);
            Assert.Equal(4, tv.Right);

            Assert.Equal(5, shorts.Left);
            Assert.Equal(6, shorts.Right);
        }

        [Fact]
        public async Task InsertTagAsync_ThrowsException_WhenParentNotFound()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                async () => await _service.InsertTagAsync("Action", 999));
        }

        [Fact]
        public async Task InsertTagAsync_CreatesDeepHierarchy_WithCorrectNesting()
        {
            // Arrange
            var movies = await _service.InsertTagAsync("Movies", null);
            var action = await _service.InsertTagAsync("Action", movies.Id);
            var superhero = await _service.InsertTagAsync("Superhero", action.Id);

            // Assert
            Assert.Equal(0, movies.Level);
            Assert.Equal(1, action.Level);
            Assert.Equal(2, superhero.Level);

            // Verify nesting
            Assert.True(superhero.Left > action.Left);
            Assert.True(superhero.Right < action.Right);
            Assert.True(action.Left > movies.Left);
            Assert.True(action.Right < movies.Right);
        }

        #endregion

        #region Query Tests

        [Fact]
        public async Task GetDescendantsAsync_ReturnsAllDescendants()
        {
            // Arrange
            var movies = await _service.InsertTagAsync("Movies", null);
            var action = await _service.InsertTagAsync("Action", movies.Id);
            var comedy = await _service.InsertTagAsync("Comedy", movies.Id);
            var superhero = await _service.InsertTagAsync("Superhero", action.Id);

            // Act
            var descendants = await _service.GetDescendantsAsync(movies.Id, includeSelf: true);

            // Assert
            Assert.Equal(4, descendants.Count);
            Assert.Contains(descendants, t => t.Name == "Movies");
            Assert.Contains(descendants, t => t.Name == "Action");
            Assert.Contains(descendants, t => t.Name == "Comedy");
            Assert.Contains(descendants, t => t.Name == "Superhero");
        }

        [Fact]
        public async Task GetDescendantsAsync_ExcludesSelf_WhenRequested()
        {
            // Arrange
            var movies = await _service.InsertTagAsync("Movies", null);
            var action = await _service.InsertTagAsync("Action", movies.Id);

            // Act
            var descendants = await _service.GetDescendantsAsync(movies.Id, includeSelf: false);

            // Assert
            Assert.Single(descendants);
            Assert.Equal("Action", descendants[0].Name);
        }

        [Fact]
        public async Task GetDescendantIdsAsync_ReturnsCorrectIds()
        {
            // Arrange
            var movies = await _service.InsertTagAsync("Movies", null);
            var action = await _service.InsertTagAsync("Action", movies.Id);
            var comedy = await _service.InsertTagAsync("Comedy", movies.Id);

            // Act
            var ids = await _service.GetDescendantIdsAsync(movies.Id, includeSelf: true);

            // Assert
            Assert.Equal(3, ids.Count);
            Assert.Contains(movies.Id, ids);
            Assert.Contains(action.Id, ids);
            Assert.Contains(comedy.Id, ids);
        }

        [Fact]
        public async Task GetAncestorsAsync_ReturnsPathToRoot()
        {
            // Arrange
            var movies = await _service.InsertTagAsync("Movies", null);
            var action = await _service.InsertTagAsync("Action", movies.Id);
            var superhero = await _service.InsertTagAsync("Superhero", action.Id);

            // Act
            var ancestors = await _service.GetAncestorsAsync(superhero.Id, includeSelf: false);

            // Assert
            Assert.Equal(2, ancestors.Count);
            Assert.Equal("Movies", ancestors[0].Name);
            Assert.Equal("Action", ancestors[1].Name);
        }

        [Fact]
        public async Task GetAncestorsAsync_IncludesSelf_WhenRequested()
        {
            // Arrange
            var movies = await _service.InsertTagAsync("Movies", null);
            var action = await _service.InsertTagAsync("Action", movies.Id);

            // Act
            var ancestors = await _service.GetAncestorsAsync(action.Id, includeSelf: true);

            // Assert
            Assert.Equal(2, ancestors.Count);
            Assert.Contains(ancestors, t => t.Name == "Movies");
            Assert.Contains(ancestors, t => t.Name == "Action");
        }

        [Fact]
        public async Task GetChildrenAsync_ReturnsImmediateChildren()
        {
            // Arrange
            var movies = await _service.InsertTagAsync("Movies", null);
            var action = await _service.InsertTagAsync("Action", movies.Id);
            var comedy = await _service.InsertTagAsync("Comedy", movies.Id);
            var superhero = await _service.InsertTagAsync("Superhero", action.Id);

            // Act
            var children = await _service.GetChildrenAsync(movies.Id);

            // Assert
            Assert.Equal(2, children.Count);
            Assert.Contains(children, t => t.Name == "Action");
            Assert.Contains(children, t => t.Name == "Comedy");
            Assert.DoesNotContain(children, t => t.Name == "Superhero");
        }

        [Fact]
        public async Task GetRootTagsAsync_ReturnsOnlyRootTags()
        {
            // Arrange
            var movies = await _service.InsertTagAsync("Movies", null);
            var tv = await _service.InsertTagAsync("TV", null);
            var action = await _service.InsertTagAsync("Action", movies.Id);

            // Act
            var roots = await _service.GetRootTagsAsync();

            // Assert
            Assert.Equal(2, roots.Count);
            Assert.Contains(roots, t => t.Name == "Movies");
            Assert.Contains(roots, t => t.Name == "TV");
            Assert.DoesNotContain(roots, t => t.Name == "Action");
        }

        [Fact]
        public async Task GetSiblingsAsync_ReturnsSameLevelTags()
        {
            // Arrange
            var movies = await _service.InsertTagAsync("Movies", null);
            var action = await _service.InsertTagAsync("Action", movies.Id);
            var comedy = await _service.InsertTagAsync("Comedy", movies.Id);
            var drama = await _service.InsertTagAsync("Drama", movies.Id);

            // Act
            var siblings = await _service.GetSiblingsAsync(action.Id, includeSelf: false);

            // Assert
            Assert.Equal(2, siblings.Count);
            Assert.Contains(siblings, t => t.Name == "Comedy");
            Assert.Contains(siblings, t => t.Name == "Drama");
            Assert.DoesNotContain(siblings, t => t.Name == "Action");
        }

        [Fact]
        public async Task IsAncestorOfAsync_ReturnsTrue_WhenIsAncestor()
        {
            // Arrange
            var movies = await _service.InsertTagAsync("Movies", null);
            var action = await _service.InsertTagAsync("Action", movies.Id);
            var superhero = await _service.InsertTagAsync("Superhero", action.Id);

            // Act
            var result = await _service.IsAncestorOfAsync(movies.Id, superhero.Id);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task IsAncestorOfAsync_ReturnsFalse_WhenNotAncestor()
        {
            // Arrange
            var movies = await _service.InsertTagAsync("Movies", null);
            var tv = await _service.InsertTagAsync("TV", null);

            // Act
            var result = await _service.IsAncestorOfAsync(movies.Id, tv.Id);

            // Assert
            Assert.False(result);
        }

        #endregion

        #region Move Tests (Simplified Rebuild Approach)

        [Fact]
        public async Task MoveTagAsync_MovesTagToNewParent()
        {
            // Arrange
            var movies = await _service.InsertTagAsync("Movies", null);
            var tv = await _service.InsertTagAsync("TV", null);
            var drama = await _service.InsertTagAsync("Drama", movies.Id);

            // Act
            await _service.MoveTagAsync(drama.Id, tv.Id);

            // Assert
            var movedTag = await _context.Tags.FindAsync(drama.Id);
            Assert.Equal(tv.Id, movedTag.ParentId);
            Assert.Equal(1, movedTag.Level); // Should be child of TV now
        }

        [Fact]
        public async Task MoveTagAsync_MovesEntireSubtree()
        {
            // Arrange
            var movies = await _service.InsertTagAsync("Movies", null);
            var action = await _service.InsertTagAsync("Action", movies.Id);
            var superhero = await _service.InsertTagAsync("Superhero", action.Id);
            var tv = await _service.InsertTagAsync("TV", null);

            // Act - Move Action (and its child Superhero) under TV
            await _service.MoveTagAsync(action.Id, tv.Id);

            // Assert
            var movedAction = await _context.Tags.FindAsync(action.Id);
            var movedSuperhero = await _context.Tags.FindAsync(superhero.Id);

            Assert.Equal(tv.Id, movedAction.ParentId);
            Assert.Equal(1, movedAction.Level);
            Assert.Equal(2, movedSuperhero.Level); // Should maintain parent relationship
            Assert.Equal(action.Id, movedSuperhero.ParentId);
        }

        [Fact]
        public async Task MoveTagAsync_MaintainsTreeIntegrity()
        {
            // Arrange
            var movies = await _service.InsertTagAsync("Movies", null);
            var action = await _service.InsertTagAsync("Action", movies.Id);
            var comedy = await _service.InsertTagAsync("Comedy", movies.Id);
            var tv = await _service.InsertTagAsync("TV", null);

            // Act
            await _service.MoveTagAsync(action.Id, tv.Id);

            // Assert - Validate tree integrity
            var errors = await _service.ValidateTreeAsync();
            Assert.Empty(errors);
        }

        [Fact]
        public async Task MoveTagAsync_ThrowsException_WhenMovingToOwnDescendant()
        {
            // Arrange
            var movies = await _service.InsertTagAsync("Movies", null);
            var action = await _service.InsertTagAsync("Action", movies.Id);
            var superhero = await _service.InsertTagAsync("Superhero", action.Id);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _service.MoveTagAsync(movies.Id, superhero.Id));
        }

        [Fact]
        public async Task MoveTagAsync_ThrowsException_WhenMovingToSelf()
        {
            // Arrange
            var movies = await _service.InsertTagAsync("Movies", null);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _service.MoveTagAsync(movies.Id, movies.Id));
        }

        [Fact]
        public async Task MoveTagAsync_MovesToRoot_WhenParentIsNull()
        {
            // Arrange
            var movies = await _service.InsertTagAsync("Movies", null);
            var action = await _service.InsertTagAsync("Action", movies.Id);

            // Act
            await _service.MoveTagAsync(action.Id, null);

            // Assert
            var movedTag = await _context.Tags.FindAsync(action.Id);
            Assert.Null(movedTag.ParentId);
            Assert.Equal(0, movedTag.Level);
        }

        [Fact]
        public async Task MoveTagAsync_UpdatesLeftRightValues_Correctly()
        {
            // Arrange
            var movies = await _service.InsertTagAsync("Movies", null);
            var action = await _service.InsertTagAsync("Action", movies.Id);
            var comedy = await _service.InsertTagAsync("Comedy", movies.Id);
            var tv = await _service.InsertTagAsync("TV", null);

            // Act
            await _service.MoveTagAsync(action.Id, tv.Id);

            // Assert
            var allTags = await _context.Tags.OrderBy(t => t.Left).ToListAsync();

            // Check no duplicate left values
            var leftValues = allTags.Select(t => t.Left).ToList();
            Assert.Equal(leftValues.Count, leftValues.Distinct().Count());

            // Check no duplicate right values
            var rightValues = allTags.Select(t => t.Right).ToList();
            Assert.Equal(rightValues.Count, rightValues.Distinct().Count());

            // Check all left < right
            foreach (var tag in allTags)
            {
                Assert.True(tag.Left < tag.Right,
                    $"Tag {tag.Name} has Left >= Right ({tag.Left} >= {tag.Right})");
            }
        }

        #endregion

        #region Delete Tests

        [Fact]
        public async Task DeleteTagAsync_RemovesLeafTag()
        {
            // Arrange
            var movies = await _service.InsertTagAsync("Movies", null);
            var action = await _service.InsertTagAsync("Action", movies.Id);

            // Act
            await _service.DeleteTagAsync(action.Id, deleteDescendants: false);

            // Assert
            var deletedTag = await _context.Tags.FindAsync(action.Id);
            Assert.Null(deletedTag);

            var moviesTag = await _context.Tags.FindAsync(movies.Id);
            Assert.NotNull(moviesTag);
        }

        [Fact]
        public async Task DeleteTagAsync_PromotesChildren_WhenNotDeletingDescendants()
        {
            // Arrange
            var movies = await _service.InsertTagAsync("Movies", null);
            var action = await _service.InsertTagAsync("Action", movies.Id);
            var superhero = await _service.InsertTagAsync("Superhero", action.Id);

            // Act
            await _service.DeleteTagAsync(action.Id, deleteDescendants: false);

            // Assert
            var superheroTag = await _context.Tags.FindAsync(superhero.Id);
            Assert.NotNull(superheroTag);
            Assert.Equal(movies.Id, superheroTag.ParentId); // Promoted to Movies
            Assert.Equal(1, superheroTag.Level);
        }

        [Fact]
        public async Task DeleteTagAsync_DeletesEntireSubtree_WhenDeletingDescendants()
        {
            // Arrange
            var movies = await _service.InsertTagAsync("Movies", null);
            var action = await _service.InsertTagAsync("Action", movies.Id);
            var superhero = await _service.InsertTagAsync("Superhero", action.Id);
            var marvel = await _service.InsertTagAsync("Marvel", superhero.Id);

            // Act
            await _service.DeleteTagAsync(action.Id, deleteDescendants: true);

            // Assert
            Assert.Null(await _context.Tags.FindAsync(action.Id));
            Assert.Null(await _context.Tags.FindAsync(superhero.Id));
            Assert.Null(await _context.Tags.FindAsync(marvel.Id));
            Assert.NotNull(await _context.Tags.FindAsync(movies.Id));
        }

        [Fact]
        public async Task DeleteTagAsync_MaintainsTreeIntegrity()
        {
            // Arrange
            var movies = await _service.InsertTagAsync("Movies", null);
            var action = await _service.InsertTagAsync("Action", movies.Id);
            var comedy = await _service.InsertTagAsync("Comedy", movies.Id);

            // Act
            await _service.DeleteTagAsync(action.Id, deleteDescendants: false);

            // Assert
            var errors = await _service.ValidateTreeAsync();
            Assert.Empty(errors);
        }

        #endregion

        #region Rebuild Tests

        [Fact]
        public async Task RebuildTreeAsync_FixesCorruptedTree()
        {
            // Arrange - Create tags and then manually corrupt the tree
            var movies = await _service.InsertTagAsync("Movies", null);
            var action = await _service.InsertTagAsync("Action", movies.Id);

            // Corrupt the tree by setting invalid left/right values
            movies.Left = 100;
            movies.Right = 100;
            action.Left = 100;
            action.Right = 100;
            await _context.SaveChangesAsync();

            // Act
            await _service.RebuildTreeAsync();

            // Assert
            var errors = await _service.ValidateTreeAsync();
            Assert.Empty(errors);

            // Verify correct structure
            var rebuiltMovies = await _context.Tags.FindAsync(movies.Id);
            var rebuiltAction = await _context.Tags.FindAsync(action.Id);

            Assert.True(rebuiltMovies.Left < rebuiltMovies.Right);
            Assert.True(rebuiltAction.Left < rebuiltAction.Right);
            Assert.True(rebuiltAction.Left > rebuiltMovies.Left);
            Assert.True(rebuiltAction.Right < rebuiltMovies.Right);
        }

        [Fact]
        public async Task RebuildTreeAsync_RecalculatesLevels()
        {
            // Arrange
            var movies = await _service.InsertTagAsync("Movies", null);
            var action = await _service.InsertTagAsync("Action", movies.Id);
            var superhero = await _service.InsertTagAsync("Superhero", action.Id);

            // Corrupt levels
            movies.Level = 99;
            action.Level = 99;
            superhero.Level = 99;
            await _context.SaveChangesAsync();

            // Act
            await _service.RebuildTreeAsync();

            // Assert
            var rebuiltMovies = await _context.Tags.FindAsync(movies.Id);
            var rebuiltAction = await _context.Tags.FindAsync(action.Id);
            var rebuiltSuperhero = await _context.Tags.FindAsync(superhero.Id);

            Assert.Equal(0, rebuiltMovies.Level);
            Assert.Equal(1, rebuiltAction.Level);
            Assert.Equal(2, rebuiltSuperhero.Level);
        }

        #endregion

        #region Validation Tests

        [Fact]
        public async Task ValidateTreeAsync_ReturnsEmpty_ForValidTree()
        {
            // Arrange
            var movies = await _service.InsertTagAsync("Movies", null);
            var action = await _service.InsertTagAsync("Action", movies.Id);
            var comedy = await _service.InsertTagAsync("Comedy", movies.Id);

            // Act
            var errors = await _service.ValidateTreeAsync();

            // Assert
            Assert.Empty(errors);
        }

        [Fact]
        public async Task ValidateTreeAsync_DetectsDuplicateLeftValues()
        {
            // Arrange
            var movies = await _service.InsertTagAsync("Movies", null);
            var action = await _service.InsertTagAsync("Action", movies.Id);

            // Corrupt - duplicate left values
            action.Left = movies.Left;
            await _context.SaveChangesAsync();

            // Act
            var errors = await _service.ValidateTreeAsync();

            // Assert
            Assert.NotEmpty(errors);
            Assert.Contains(errors, e => e.Contains("Duplicate left value"));
        }

        [Fact]
        public async Task ValidateTreeAsync_DetectsInvalidLeftRightRelationship()
        {
            // Arrange
            var movies = await _service.InsertTagAsync("Movies", null);

            // Corrupt - left >= right
            movies.Right = movies.Left;
            await _context.SaveChangesAsync();

            // Act
            var errors = await _service.ValidateTreeAsync();

            // Assert
            Assert.NotEmpty(errors);
            Assert.Contains(errors, e => e.Contains("invalid left/right values"));
        }

        [Fact]
        public async Task ValidateTreeAsync_DetectsImproperNesting()
        {
            // Arrange
            var movies = await _service.InsertTagAsync("Movies", null);
            var action = await _service.InsertTagAsync("Action", movies.Id);

            // Corrupt - child not nested within parent
            action.Left = movies.Right + 1;
            action.Right = movies.Right + 2;
            await _context.SaveChangesAsync();

            // Act
            var errors = await _service.ValidateTreeAsync();

            // Assert
            Assert.NotEmpty(errors);
            Assert.Contains(errors, e => e.Contains("not properly nested"));
        }

        #endregion

        #region Helper Method Tests

        [Fact]
        public async Task GetDescendantCount_ReturnsCorrectCount()
        {
            // Arrange
            var movies = await _service.InsertTagAsync("Movies", null);
            var action = await _service.InsertTagAsync("Action", movies.Id);
            var comedy = await _service.InsertTagAsync("Comedy", movies.Id);
            var superhero = await _service.InsertTagAsync("Superhero", action.Id);

            // Act
            var count = movies.GetDescendantCount();

            // Assert
            Assert.Equal(3, count); // Action, Comedy, Superhero
        }

        [Fact]
        public async Task HasChildren_ReturnsTrue_ForParent()
        {
            // Arrange
            var movies = await _service.InsertTagAsync("Movies", null);
            var action = await _service.InsertTagAsync("Action", movies.Id);

            // Act & Assert
            Assert.True(movies.HasChildren());
            Assert.False(action.HasChildren());
        }

        #endregion
    }
}
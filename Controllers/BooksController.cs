using BookstoreAPI.Data;
using BookstoreAPI.DTOs;
using BookstoreAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookstoreAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BooksController : ControllerBase
    {
        private readonly BookstoreContext _context;

        public BooksController(BookstoreContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<BookDto>>> GetBooks([FromQuery] int? categoryId, [FromQuery] string? search)
        {
            var query = _context.Books.Include(b => b.Category).AsQueryable();

            if (categoryId.HasValue)
            {
                query = query.Where(b => b.CategoryId == categoryId.Value);
            }

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(b => b.Title.Contains(search) || b.Author.Contains(search));
            }

            var books = await query.Select(b => new BookDto
            {
                BookId = b.BookId,
                Title = b.Title,
                Author = b.Author,
                ISBN = b.ISBN,
                CategoryId = b.CategoryId,
                CategoryName = b.Category != null ? b.Category.CategoryName : null,
                Description = b.Description,
                Price = b.Price,
                StockQuantity = b.StockQuantity,
                ImageUrl = b.ImageUrl,
                PublicationDate = b.PublicationDate,
                Publisher = b.Publisher,
                CreatedAt = b.CreatedAt,
                UpdatedAt = b.UpdatedAt
            }).ToListAsync();

            return Ok(books);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<BookDto>> GetBook(int id)
        {
            var book = await _context.Books
                .Include(b => b.Category)
                .FirstOrDefaultAsync(b => b.BookId == id);

            if (book == null)
            {
                return NotFound(new { message = "Book not found" });
            }

            var bookDto = new BookDto
            {
                BookId = book.BookId,
                Title = book.Title,
                Author = book.Author,
                ISBN = book.ISBN,
                CategoryId = book.CategoryId,
                CategoryName = book.Category?.CategoryName,
                Description = book.Description,
                Price = book.Price,
                StockQuantity = book.StockQuantity,
                ImageUrl = book.ImageUrl,
                PublicationDate = book.PublicationDate,
                Publisher = book.Publisher,
                CreatedAt = book.CreatedAt,
                UpdatedAt = book.UpdatedAt
            };

            return Ok(bookDto);
        }

        [HttpPost]
        [Authorize(Roles = "admin")]
        public async Task<ActionResult<BookDto>> CreateBook([FromBody] CreateBookDto createBookDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var book = new Book
            {
                Title = createBookDto.Title,
                Author = createBookDto.Author,
                ISBN = createBookDto.ISBN,
                CategoryId = createBookDto.CategoryId,
                Description = createBookDto.Description,
                Price = createBookDto.Price,
                StockQuantity = createBookDto.StockQuantity,
                ImageUrl = createBookDto.ImageUrl,
                PublicationDate = createBookDto.PublicationDate,
                Publisher = createBookDto.Publisher
            };

            _context.Books.Add(book);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetBook), new { id = book.BookId }, await MapToBookDto(book));
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> UpdateBook(int id, [FromBody] UpdateBookDto updateBookDto)
        {
            var book = await _context.Books.FindAsync(id);
            if (book == null)
            {
                return NotFound(new { message = "Book not found" });
            }

            if (!string.IsNullOrEmpty(updateBookDto.Title))
                book.Title = updateBookDto.Title;
            if (!string.IsNullOrEmpty(updateBookDto.Author))
                book.Author = updateBookDto.Author;
            if (updateBookDto.ISBN != null)
                book.ISBN = updateBookDto.ISBN;
            if (updateBookDto.CategoryId.HasValue)
                book.CategoryId = updateBookDto.CategoryId;
            if (updateBookDto.Description != null)
                book.Description = updateBookDto.Description;
            if (updateBookDto.Price.HasValue)
                book.Price = updateBookDto.Price.Value;
            if (updateBookDto.StockQuantity.HasValue)
                book.StockQuantity = updateBookDto.StockQuantity.Value;
            if (updateBookDto.ImageUrl != null)
                book.ImageUrl = updateBookDto.ImageUrl;
            if (updateBookDto.PublicationDate.HasValue)
                book.PublicationDate = updateBookDto.PublicationDate;
            if (!string.IsNullOrEmpty(updateBookDto.Publisher))
                book.Publisher = updateBookDto.Publisher;

            await _context.SaveChangesAsync();
            return Ok(await MapToBookDto(book));
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> DeleteBook(int id)
        {
            var book = await _context.Books.FindAsync(id);
            if (book == null)
            {
                return NotFound(new { message = "Book not found" });
            }

            _context.Books.Remove(book);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Book deleted successfully" });
        }

        private async Task<BookDto> MapToBookDto(Book book)
        {
            var category = book.CategoryId.HasValue 
                ? await _context.Categories.FindAsync(book.CategoryId.Value) 
                : null;

            return new BookDto
            {
                BookId = book.BookId,
                Title = book.Title,
                Author = book.Author,
                ISBN = book.ISBN,
                CategoryId = book.CategoryId,
                CategoryName = category?.CategoryName,
                Description = book.Description,
                Price = book.Price,
                StockQuantity = book.StockQuantity,
                ImageUrl = book.ImageUrl,
                PublicationDate = book.PublicationDate,
                Publisher = book.Publisher,
                CreatedAt = book.CreatedAt,
                UpdatedAt = book.UpdatedAt
            };
        }
    }
}
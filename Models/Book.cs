using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookstoreAPI.Models
{
    [Table("books")]
    public class Book
    {
        [Key]
        [Column("book_id")]
        public int BookId { get; set; }

        [Required]
        [Column("title")]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [Column("author")]
        [StringLength(100)]
        public string Author { get; set; } = string.Empty;

        [Column("isbn")]
        [StringLength(20)]
        public string? ISBN { get; set; }

        [Column("category_id")]
        public int? CategoryId { get; set; }

        [Column("description")]
        public string? Description { get; set; }

        [Required]
        [Column("price", TypeName = "decimal(10,2)")]
        public decimal Price { get; set; }

        [Required]
        [Column("stock_quantity")]
        public int StockQuantity { get; set; } = 0;

        [Column("image_url")]
        [StringLength(500)]
        public string? ImageUrl { get; set; }

        [Column("publication_date")]
        public DateTime? PublicationDate { get; set; }

        [Column("publisher")]
        [StringLength(100)]
        public string? Publisher { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        // Navigation properties
        [ForeignKey("CategoryId")]
        public virtual Category? Category { get; set; }

        public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    }
}
using BookstoreAPI.Data;
using BookstoreAPI.DTOs;
using BookstoreAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BookstoreAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class OrdersController : ControllerBase
    {
        private readonly BookstoreContext _context;

        public OrdersController(BookstoreContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<OrderDto>>> GetOrders([FromQuery] string? status)
        {
            var userId = GetCurrentUserId();
            var userRole = GetCurrentUserRole();

            var query = _context.Orders
                .Include(o => o.User)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Book)
                .AsQueryable();

            // If not admin, only show user's own orders
            if (userRole != "admin")
            {
                query = query.Where(o => o.UserId == userId);
            }

            // Filter by status if provided
            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(o => o.OrderStatus.ToLower() == status.ToLower());
            }

            var orders = await query
                .OrderByDescending(o => o.OrderDate)
                .Select(o => new OrderDto
                {
                    OrderId = o.OrderId,
                    UserId = o.UserId,
                    Username = o.User.Username,
                    OrderDate = o.OrderDate,
                    TotalAmount = o.TotalAmount,
                    OrderStatus = o.OrderStatus,
                    ShippingAddress = o.ShippingAddress,
                    PaymentMethod = o.PaymentMethod,
                    PaymentStatus = o.PaymentStatus,
                    OrderItems = o.OrderItems.Select(oi => new OrderItemDto
                    {
                        OrderItemId = oi.OrderItemId,
                        BookId = oi.BookId,
                        BookTitle = oi.Book.Title,
                        BookAuthor = oi.Book.Author,
                        Quantity = oi.Quantity,
                        UnitPrice = oi.UnitPrice,
                        TotalPrice = oi.TotalPrice
                    }).ToList()
                })
                .ToListAsync();

            return Ok(orders);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<OrderDto>> GetOrder(int id)
        {
            var userId = GetCurrentUserId();
            var userRole = GetCurrentUserRole();

            var order = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Book)
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (order == null)
            {
                return NotFound(new { message = "Order not found" });
            }

            // If not admin, only allow access to own orders
            if (userRole != "admin" && order.UserId != userId)
            {
                return Forbid();
            }

            var orderDto = new OrderDto
            {
                OrderId = order.OrderId,
                UserId = order.UserId,
                Username = order.User.Username,
                OrderDate = order.OrderDate,
                TotalAmount = order.TotalAmount,
                OrderStatus = order.OrderStatus,
                ShippingAddress = order.ShippingAddress,
                PaymentMethod = order.PaymentMethod,
                PaymentStatus = order.PaymentStatus,
                OrderItems = order.OrderItems.Select(oi => new OrderItemDto
                {
                    OrderItemId = oi.OrderItemId,
                    BookId = oi.BookId,
                    BookTitle = oi.Book.Title,
                    BookAuthor = oi.Book.Author,
                    Quantity = oi.Quantity,
                    UnitPrice = oi.UnitPrice,
                    TotalPrice = oi.TotalPrice
                }).ToList()
            };

            return Ok(orderDto);
        }

        [HttpPost]
        public async Task<ActionResult<OrderDto>> CreateOrder([FromBody] CreateOrderDto createOrderDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userId = GetCurrentUserId();

            // Validate that all books exist and have sufficient stock
            var bookIds = createOrderDto.OrderItems.Select(oi => oi.BookId).ToList();
            var books = await _context.Books
                .Where(b => bookIds.Contains(b.BookId))
                .ToListAsync();

            if (books.Count != bookIds.Count)
            {
                return BadRequest(new { message = "One or more books not found" });
            }

            var orderItems = new List<OrderItem>();
            decimal totalAmount = 0;

            // Validate stock and prepare order items
            foreach (var itemDto in createOrderDto.OrderItems)
            {
                var book = books.First(b => b.BookId == itemDto.BookId);
                
                if (book.StockQuantity < itemDto.Quantity)
                {
                    return BadRequest(new { message = $"Insufficient stock for book '{book.Title}'. Available: {book.StockQuantity}, Requested: {itemDto.Quantity}" });
                }

                var orderItem = new OrderItem
                {
                    BookId = itemDto.BookId,
                    Quantity = itemDto.Quantity,
                    UnitPrice = book.Price,
                    TotalPrice = book.Price * itemDto.Quantity
                };

                orderItems.Add(orderItem);
                totalAmount += orderItem.TotalPrice;
            }

            // Create the order
            var order = new Order
            {
                UserId = userId,
                TotalAmount = totalAmount,
                ShippingAddress = createOrderDto.ShippingAddress,
                PaymentMethod = createOrderDto.PaymentMethod,
                OrderItems = orderItems
            };

            _context.Orders.Add(order);

            // Update book stock quantities
            foreach (var itemDto in createOrderDto.OrderItems)
            {
                var book = books.First(b => b.BookId == itemDto.BookId);
                book.StockQuantity -= itemDto.Quantity;
            }

            await _context.SaveChangesAsync();

            // Return the created order
            var createdOrder = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Book)
                .FirstAsync(o => o.OrderId == order.OrderId);

            var orderDto = new OrderDto
            {
                OrderId = createdOrder.OrderId,
                UserId = createdOrder.UserId,
                Username = createdOrder.User.Username,
                OrderDate = createdOrder.OrderDate,
                TotalAmount = createdOrder.TotalAmount,
                OrderStatus = createdOrder.OrderStatus,
                ShippingAddress = createdOrder.ShippingAddress,
                PaymentMethod = createdOrder.PaymentMethod,
                PaymentStatus = createdOrder.PaymentStatus,
                OrderItems = createdOrder.OrderItems.Select(oi => new OrderItemDto
                {
                    OrderItemId = oi.OrderItemId,
                    BookId = oi.BookId,
                    BookTitle = oi.Book.Title,
                    BookAuthor = oi.Book.Author,
                    Quantity = oi.Quantity,
                    UnitPrice = oi.UnitPrice,
                    TotalPrice = oi.TotalPrice
                }).ToList()
            };

            return CreatedAtAction(nameof(GetOrder), new { id = order.OrderId }, orderDto);
        }

        [HttpPut("{id}/status")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> UpdateOrderStatus(int id, [FromBody] UpdateOrderStatusDto updateStatusDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var order = await _context.Orders.FindAsync(id);
            if (order == null)
            {
                return NotFound(new { message = "Order not found" });
            }

            // Validate status values
            var validOrderStatuses = new[] { "pending", "confirmed", "processing", "shipped", "delivered", "cancelled" };
            var validPaymentStatuses = new[] { "pending", "paid", "failed", "refunded" };

            if (!string.IsNullOrEmpty(updateStatusDto.OrderStatus) && 
                !validOrderStatuses.Contains(updateStatusDto.OrderStatus.ToLower()))
            {
                return BadRequest(new { message = "Invalid order status" });
            }

            if (!string.IsNullOrEmpty(updateStatusDto.PaymentStatus) && 
                !validPaymentStatuses.Contains(updateStatusDto.PaymentStatus.ToLower()))
            {
                return BadRequest(new { message = "Invalid payment status" });
            }

            // Update status fields
            if (!string.IsNullOrEmpty(updateStatusDto.OrderStatus))
                order.OrderStatus = updateStatusDto.OrderStatus.ToLower();
            
            if (!string.IsNullOrEmpty(updateStatusDto.PaymentStatus))
                order.PaymentStatus = updateStatusDto.PaymentStatus.ToLower();

            await _context.SaveChangesAsync();

            return Ok(new { message = "Order status updated successfully", orderId = order.OrderId });
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> DeleteOrder(int id)
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (order == null)
            {
                return NotFound(new { message = "Order not found" });
            }

            // Only allow deletion of pending orders
            if (order.OrderStatus.ToLower() != "pending")
            {
                return BadRequest(new { message = "Only pending orders can be deleted" });
            }

            // Restore book stock quantities
            foreach (var orderItem in order.OrderItems)
            {
                var book = await _context.Books.FindAsync(orderItem.BookId);
                if (book != null)
                {
                    book.StockQuantity += orderItem.Quantity;
                }
            }

            _context.Orders.Remove(order);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Order deleted successfully" });
        }

        [HttpPost("{id}/cancel")]
        public async Task<IActionResult> CancelOrder(int id)
        {
            var userId = GetCurrentUserId();
            var userRole = GetCurrentUserRole();

            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (order == null)
            {
                return NotFound(new { message = "Order not found" });
            }

            // If not admin, only allow cancellation of own orders
            if (userRole != "admin" && order.UserId != userId)
            {
                return Forbid();
            }

            // Only allow cancellation of pending or confirmed orders
            if (!new[] { "pending", "confirmed" }.Contains(order.OrderStatus.ToLower()))
            {
                return BadRequest(new { message = "Order cannot be cancelled at this stage" });
            }

            order.OrderStatus = "cancelled";

            // Restore book stock quantities
            foreach (var orderItem in order.OrderItems)
            {
                var book = await _context.Books.FindAsync(orderItem.BookId);
                if (book != null)
                {
                    book.StockQuantity += orderItem.Quantity;
                }
            }

            await _context.SaveChangesAsync();

            return Ok(new { message = "Order cancelled successfully" });
        }

        [HttpGet("my-orders")]
        public async Task<ActionResult<IEnumerable<OrderDto>>> GetMyOrders()
        {
            var userId = GetCurrentUserId();

            var orders = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Book)
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.OrderDate)
                .Select(o => new OrderDto
                {
                    OrderId = o.OrderId,
                    UserId = o.UserId,
                    Username = o.User.Username,
                    OrderDate = o.OrderDate,
                    TotalAmount = o.TotalAmount,
                    OrderStatus = o.OrderStatus,
                    ShippingAddress = o.ShippingAddress,
                    PaymentMethod = o.PaymentMethod,
                    PaymentStatus = o.PaymentStatus,
                    OrderItems = o.OrderItems.Select(oi => new OrderItemDto
                    {
                        OrderItemId = oi.OrderItemId,
                        BookId = oi.BookId,
                        BookTitle = oi.Book.Title,
                        BookAuthor = oi.Book.Author,
                        Quantity = oi.Quantity,
                        UnitPrice = oi.UnitPrice,
                        TotalPrice = oi.TotalPrice
                    }).ToList()
                })
                .ToListAsync();

            return Ok(orders);
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            return userIdClaim != null ? int.Parse(userIdClaim.Value) : 0;
        }

        private string GetCurrentUserRole()
        {
            var roleClaim = User.FindFirst(ClaimTypes.Role);
            return roleClaim?.Value ?? "customer";
        }
    }

    
}
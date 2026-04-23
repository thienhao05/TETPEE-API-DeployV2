using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using TetPee.Repository;
using TetPee.Repository.Entity;

namespace TetPee.Service.Order;
 
public class Service : IService
{
    private readonly AppDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContext;

    public Service(AppDbContext dbContext, IHttpContextAccessor httpContext)
    {
        _dbContext = dbContext;
        _httpContext = httpContext;
    }

    public async Task<Response.CreateOrderResponse> CreateOrder(Request.CreateOrderRequest request)
    {
        var userId = _httpContext.HttpContext.User.Claims.FirstOrDefault(x => x.Type == "UserId")?.Value;

        var userIdGuid = Guid.Parse(userId!);
        
        var productIds = request.Products.Select(x => x.ProductId).Distinct().ToList();

        var query = _dbContext.Products.Where(x => productIds.Contains(x.Id));

        var productCount = await query.CountAsync();

        if (productCount != productIds.Count)
        {
            throw new Exception("Some products not found");
        }

        var result = await query.ToListAsync();

        decimal totalAmount = 0;

        foreach (var prod in result)
        {
            var quantity = request.Products.First(x => 
                x.ProductId == prod.Id).Quantity;
            
            if (quantity <= 0)
            {
                throw new Exception($"Quantity of product {prod.Id} must be greater than 0");
            }
           
            totalAmount += prod.Price * quantity;
        }

        if (totalAmount <= 0)
        {
            throw new Exception("Total amount must be greater than 0");
        }

        var order = new Repository.Entity.Order()
        {
            Id = Guid.NewGuid(),
            UserId = userIdGuid,
            TotalAmount = totalAmount,
            Address = request.Address,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow,
        };
        
        _dbContext.Add(order);
        await _dbContext.SaveChangesAsync();

        List<OrderDetail> orderDetails = new List<OrderDetail>();

        foreach (var prod in result)
        {
            var quantity = request.Products.First(x =>
                x.ProductId == prod.Id).Quantity;

            var orderDetail = new OrderDetail()
            {
                Id = Guid.NewGuid(),
                ProductId = prod.Id,
                OrderId = order.Id,
                Quantity = quantity,
                UnitPrice = prod.Price,
            };
            
            orderDetails.Add(orderDetail);
        }
        
        if (orderDetails.Any())
        {
            _dbContext.AddRange(orderDetails);
            await _dbContext.SaveChangesAsync();
        }
        
        //Trả ra QR cho người dùng quét
        string description = $"TETPEE-{order.Id}";

        var response = new Response.CreateOrderResponse()
        {
            OrderId = order.Id,
            TotalAmount = order.TotalAmount,
            BankName = "TPBank",
            BankAccount = "00005668350",
            Description = description,
            QRCode = "",
        };
        
        string qrCode = $"https://qr.sepay.vn/img?" +
                        $"acc={response.BankAccount}&" +
                        $"bank={response.BankName}&" +
                        $"amount={(int)totalAmount}&" +
                        $"des={description}&" +
                        $"template=qronly";
        
        response.QRCode = qrCode;
        
        return response;
    }

    public async Task SePayWebhookHandler(Request.SepayWebhookRequest request)
    {
        var description = request.Code; // TETPEEORDERID
        
        var raw = description.Replace("TETPEE", ""); // TETPEEORDERID -> ORDERID
        
        Guid? orderId = null;
        
        if (raw.Length == 32)
            // Mặc định Guid có 32 kí tự
            // còn nếu mà có dấu gạch nối thì là 36 kí tự
        {
            // Vì ORDERID theo format là không có dấu gạch ngang
            // Sample: 002da871-503a-4f30-884a-14457bbdaef8
            var formatted = 
                $"{raw.Substring(0, 8)}-" +
                $"{raw.Substring(8, 4)}-" +
                $"{raw.Substring(12, 4)}-" +
                $"{raw.Substring(16, 4)}-" +
                $"{raw.Substring(20, 12)}";
            
            // Id nhưng mà hiện nó đang theo string, chuẩn GUID rồi đó

            if (Guid.TryParse(formatted, out var guid))
            {
                // dùng được
                orderId =  guid;
            }
            
            // orderId = Guid.Parse(formatted);
        }
        else
        {
            // Không dùng được
            throw new Exception("Invalid description format");
        }
        
        if (orderId == null)
        {
            throw new Exception("OrderId not found in description");
        }

        var query = _dbContext.Orders
            .Where(x => x.Id == orderId)
            .Include(x => x.OrderDetails);
        
        // Khi mà thanh toán thành công thì xóa ra khỏi Order
        
        var order = await query.FirstOrDefaultAsync();

        if (order == null)
        {
            throw new Exception("Order not found");
        }

        if (order.Status != "Pending")
        {
            throw new Exception("Order already processed");
        }
        
        if (order.TotalAmount != request.TransferAmount)
        {
            throw new Exception("Invalid transfer amount");
        }
        
        order.Status = "Completed";
        _dbContext.Update(order);
        await _dbContext.SaveChangesAsync();
        
        // Người ta thanh tóa xong rồi thì xóa ra khỏi cart
        
        var productIds = order.OrderDetails.Select(x => x.ProductId).ToList();

        var queryProdCart = _dbContext.CartDetails.Where(x =>
            x.Cart.UserId == order.UserId &&
            productIds.Contains(x.ProductId));
        
        var removeCartDetails = await queryProdCart.ToListAsync();
        _dbContext.RemoveRange(removeCartDetails);
        
        await _dbContext.SaveChangesAsync();

    }
}
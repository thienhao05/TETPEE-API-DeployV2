using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TetPee.Service.Cart;
using TetPee.Service.Models;

namespace TetPee.Api.Controllers;

[Authorize]
[ApiController]
[Route("[controller]")]
public class CartController : ControllerBase
{
    private readonly IService _cartService;
    
    public CartController(IService cartService)
    {
        _cartService = cartService;
    }

    [HttpGet("")]
    public async Task<IActionResult> GetCart()
    {
        // throw new Exception("Cảnh báo giả: Test thử xem Discord có réo tên không nè!");
        var result = await _cartService.GetCart();
        return Ok(ApiResponseFactory.SuccessResponse(result, "Cart Response", HttpContext.TraceIdentifier));
    }
    
    [HttpPost("")]
    public async Task<IActionResult> CreateCart()
    {
        await _cartService.CreateCart();
        return Ok(ApiResponseFactory.SuccessResponse(null, "Cart created", HttpContext.TraceIdentifier));
    }
    
    [HttpPost("product")]
    public async Task<IActionResult> AddProductToCart(Request.AddProductToCartRequest request)
    {
        await _cartService.AddProductToCart(request);
        return Ok(ApiResponseFactory.SuccessResponse("Successfully", "Product Add To Cart", HttpContext.TraceIdentifier));
    }
    
    [HttpDelete("product")]
    public async Task<IActionResult> DeleteProductFromCart(Request.RemoveProductFromCartRequest request)
    {
        await _cartService.RemoveProductFromCart(request);
        return Ok(ApiResponseFactory.SuccessResponse("Successfully", "Product Removed", HttpContext.TraceIdentifier));
    }
    
    
}
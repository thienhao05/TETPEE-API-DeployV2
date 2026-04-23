using CloudinaryDotNet;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Quartz;
using TetPee.Api.Extensions;
using TetPee.Api.Middlewares;
using TetPee.Repository;
using TetPee.Service.BackgroundJobService;
using TetPee.Service.Models;
using UserService = TetPee.Service.User;
using CategoryService = TetPee.Service.Category;
using SellerService = TetPee.Service.Seller;
using IdentityService = TetPee.Service.Identity;
using ProductService = TetPee.Service.Product;
using JwtService = TetPee.Service.JwtService;
using MediaService = TetPee.Service.MediaService;
using CloudinaryService = TetPee.Service.CloudinaryService;
using MailService = TetPee.Service.MailService;
using CartService = TetPee.Service.Cart;
using OrderService = TetPee.Service.Order;
using DiscordService = TetPee.Service.DiscordService;
using GoogleAuthService = TetPee.Service.GoogleAuthService;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")
    )
);

builder.Services.AddJwtServices(builder.Configuration);
builder.Services.AddSwaggerServices();

builder.Services.AddScoped<UserService.IService, UserService.Service>();
builder.Services.AddScoped<CategoryService.IService, CategoryService.Service>();
builder.Services.AddScoped<SellerService.IService, SellerService.Service>();
builder.Services.AddScoped<JwtService.IService, JwtService.Service>();
builder.Services.AddScoped<IdentityService.IService, IdentityService.Service>();
builder.Services.AddScoped<ProductService.IService, ProductService.Service>();
builder.Services.AddScoped<MediaService.IService, CloudinaryService.Service>();
builder.Services.AddScoped<MailService.IService, MailService.Service>();
builder.Services.AddScoped<CartService.IService, CartService.Service>();
builder.Services.AddScoped<OrderService.IService, OrderService.Service>();

//test thử discord
builder.Services.Configure<DiscordService.DiscordAlertOptions>(
    builder.Configuration.GetSection("DiscordAlertOptions"));
builder.Services.AddHttpClient<DiscordService.IService, DiscordService.Service>(); // AddHttpClient là do nó tự gọi API ở bên ngoài
// Cụ thể ở đây của mình là tự gọi API webhook của discord

builder.Services.Configure<GoogleAuthService.GoogleAuthOptions>(
    builder.Configuration.GetSection("GoogleAuthOptions"));
builder.Services.AddScoped<GoogleAuthService.IService, GoogleAuthService.Service>(); // Còn này là mình dùng API của hệ thống luôn rồi

builder.Services.AddQuartz(options =>
{
    var jobKey = new JobKey(nameof(ProcessTransactionPendingJob));
    var daily = new JobKey(nameof(DailyDiscountEmailJob));
    
    options
        .AddJob<ProcessTransactionPendingJob>(jobKey)
        .AddTrigger(trigger =>
                trigger
                    .ForJob(jobKey)
                    .WithSimpleSchedule(schedule => schedule
                            .WithIntervalInMinutes(1) //thời gian job thực thi //2 
                            .RepeatForever() //bắt đầu khi app chạy
                    )
            // cứ 1 phút thì lặp lại 1 lần
        );
    
    options
        .AddJob<DailyDiscountEmailJob>(daily)
        .AddTrigger(opts => opts
                .ForJob(daily)
                .WithIdentity("DailyDiscountEmailTrigger")
                .WithDescription("Fires every day at 9:00 AM")
                .WithCronSchedule("0 0 9 * * ?") // Cron: second minute hour day month weekday //0 * * * * ? -> này để test
            // 1 phút 1 lần gửi mail
        );
    
    
});
builder.Services.AddQuartzHostedService(options =>
{
    options.WaitForJobsToComplete = true;
});


builder.Services.AddTransient<GlobalExceptionHandlerMiddleware>();

var app = builder.Build();

app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
app.UseStatusCodePages(async statusCodeContext =>
{
    var response = statusCodeContext.HttpContext.Response;
    if (!string.IsNullOrWhiteSpace(response.ContentType))
    {
        return;
    }

    response.ContentType = "application/json";

    var message = response.StatusCode switch
    {
        StatusCodes.Status401Unauthorized => "Unauthorized",
        StatusCodes.Status403Forbidden => "Forbidden",
        StatusCodes.Status404NotFound => "Resource not found",
        _ => "Request could not be processed"
    };

    var payload = ApiResponseFactory.ErrorResponse(
        message: message,
        traceId: statusCodeContext.HttpContext.TraceIdentifier);

    await response.WriteAsJsonAsync(payload);
});

if (app.Environment.IsDevelopment())
{
    app.UseSwaggerAPI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

/*
 * Gửi mail => done
Hẹn lịch gửi mail 
Thanh toán tiền thật => chưa deploy đc
Đa ngôn ngữ
Đăng nhập gud gồ => done
Gửi mail lúc 9h sáng(Dùng bot)  => done
Gửi code verify để đổi password => send verify => done
Hashed password => done 
 */
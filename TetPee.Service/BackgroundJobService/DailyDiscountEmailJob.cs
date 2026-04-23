using Microsoft.EntityFrameworkCore;
using Quartz;
using TetPee.Repository;
using TetPee.Service.MailService;

namespace TetPee.Service.BackgroundJobService;

[DisallowConcurrentExecution]
public class DailyDiscountEmailJob : IJob
{
    private readonly IService _mailService;
    private const double DiscountPercentage = 25.5;
    // private readonly AppDbContext _dbContext;


    public DailyDiscountEmailJob(IService mailService)
    {
        _mailService = mailService;
        // _dbContext = dbContext;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        //Gửi 1000 mail
        // var recipients = await _dbContext.Users
        //     // .Where(u => u.IsActive && u.IsSubscribed) // Mở comment dòng này và sửa thuộc tính cho đúng với Entities của bạn
        //     .Select(u => u.Email) // Chỉ Select mỗi cột Email (chuỗi) để tối ưu RAM và tốc độ truy vấn
        //     .ToListAsync();
        //
        // // Kiểm tra nếu không có ai thì dừng luôn job
        // if (recipients.Count == 0)
        // {
        //     Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Không có user nào để gửi email. Kết thúc Job.");
        //     return;
        // }

        var recipients = new List<string>
        {
            "edwardpaul9350@gmail.com", //test 1 email
        };


        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Sending discount emails to {recipients.Count} recipients...");

        var tasks = recipients.Select(email => SendDiscountEmail(email));
        await Task.WhenAll(tasks);

        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] All discount emails dispatched.");
    }

    private async Task SendDiscountEmail(string recipientEmail)
    {
        var mailContent = new MailContent
        {
            To = recipientEmail,
            Subject = $"🎉 Special Offer: Get {DiscountPercentage}% Off Today!",
            Body = $@"
                <html>
                <body style='font-family: Arial, sans-serif; color: #333;'>
                    <h1 style='color: #e44d26;'>Don't Miss Out!</h1>
                    <p>We're excited to offer you an exclusive discount of 
                       <strong style='font-size: 1.3em;'>{DiscountPercentage}%</strong> 
                       on all our products.</p>
                    <p>This limited-time offer expires at midnight — act fast!</p>
                    <a href='https://yourwebsite.com/shop'
                       style='display:inline-block; padding:12px 24px; background:#e44d26;
                              color:white; text-decoration:none; border-radius:4px;'>
                        Shop Now
                    </a>
                    <p style='color: #888; font-size: 0.85em; margin-top: 20px;'>
                        You're receiving this because you subscribed to our promotions.
                    </p>
                </body>
                </html>"
        };

        try
        {
            await _mailService.SendMail(mailContent);
            Console.WriteLine($"  ✓ Email sent to {recipientEmail}");
        }
        catch (Exception ex)
        {
            // Log error per recipient — don't let one failure stop others
            Console.WriteLine($"  ✗ Failed to send to {recipientEmail}: {ex.Message}");
        }
    }
}
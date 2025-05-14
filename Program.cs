using CollageMangmentSystem.Core.Interfaces;
using CollageMangmentSystem.Infrastructure.Data;
using CollageMangmentSystem.Infrastructure.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using CollageManagementSystem.Services;
using CollageManagementSystem.Core.Entities.userEnrollments;
using CollageMangmentSystem.Infrastructure.Middlewares;
using CollageManagementSystem.Core;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();
// Add infrastructure services
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres"),
                      o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
);

// Register repositories
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped(typeof(IDepRepostaory<>), typeof(DepRepostaory<>));
builder.Services.AddScoped(typeof(ICourseReposatory<>), typeof(CourseReposatory<>));
builder.Services.AddScoped(typeof(IUserEnrollments<>), typeof(UserEnrollmentsRepostaory<>));
builder.Services.AddScoped<IAdminReposatory, AdminReposatory>();
builder.Services.AddScoped<IQuizRepository, QuizRepository>();
builder.Services.AddScoped<IUserService, UserService>();

// Configure rate limiting
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("FixedWindowPolicy", opt =>
    {
        opt.Window = TimeSpan.FromSeconds(10);
        opt.PermitLimit = 5;
        opt.QueueLimit = 0;
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });

    options.AddFixedWindowLimiter("StrictPolicy", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(10);
        opt.PermitLimit = 10;
        opt.QueueLimit = 0;
    });

    options.OnRejected = (context, _) =>
    {
        context.HttpContext.Response.Headers["Retry-After"] =
            (600 - DateTime.Now.Second % 600).ToString();
        return new ValueTask();
    };

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});
// Register application services

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
                "http://localhost:3000",
                "http://collagemanagmentsystem.runasp.net"
              )
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials() // Important for cookies
              .SetPreflightMaxAge(TimeSpan.FromHours(1));
    });
});

var app = builder.Build();
app.UseCors("AllowFrontend");

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();
app.UseMiddleware<GlobalExceptionMiddleware>();
// Use middleware with factory approach
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
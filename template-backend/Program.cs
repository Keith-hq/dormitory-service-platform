using DormitoryPlatform.API.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Quartz;
using System.Text;
using TemplateDormApi.Data;
using TemplateDormApi.DTO;
using TemplateDormApi.Jobs;
using TemplateDormApi.Repository;
using TemplateDormApi.Services;

var builder = WebApplication.CreateBuilder(args);

// ===== 1. 注册 Controller（三层架构入口）=====
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // 首字母小写驼峰（与前端对齐）
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var message = context.ModelState.Values
            .SelectMany(state => state.Errors)
            .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage) ? "参数校验失败" : error.ErrorMessage)
            .FirstOrDefault() ?? "参数校验失败";

        return new BadRequestObjectResult(ApiResponse.Error(400, message));
    };
});

// ===== 2. 注册 Swagger（开发调试用）=====
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "TemplateDormApi - 样板间接口", Version = "v1" });
});

// ===== 3. 注册 Oracle EF Core DbContext =====
builder.Services.AddDbContext<AppDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("OracleConnection");
    if (string.IsNullOrEmpty(connectionString))
    {
        throw new Exception("OracleConnection 未配置，请在 User Secrets 中设置。");
    }
    options.UseOracle(connectionString);
});

// ===== 4. 注册 Repository 层 =====
builder.Services.AddScoped<BuildingRepository>();
builder.Services.AddScoped<UserAccountRepository>();
builder.Services.AddScoped<NotificationRepository>();
builder.Services.AddScoped<CreditRepository>();

// ===== 5. 注册 Service 层 =====
builder.Services.AddScoped<IBuildingService, BuildingService>();
builder.Services.AddScoped<IFeeSharingService, FeeSharingService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<ICreditService, CreditService>();
builder.Services.AddScoped<IFreezeNotifier, NotificationFreezeNotifier>();
builder.Services.AddScoped<IBillingService, BillingService>();
builder.Services.AddScoped<IFacilityBookingService, FacilityBookingService>();

// ===== 6. 注册 Quartz 定时任务 =====
builder.Services.AddQuartz(q =>
{
    // 注册水电分摊 Job
    var jobKey = new JobKey("FeeSharingJob");
    q.AddJob<FeeSharingJob>(opts => opts.WithIdentity(jobKey));

    // 每月1日凌晨0点触发
    q.AddTrigger(opts => opts
        .ForJob(jobKey)
        .WithIdentity("FeeSharingTrigger")
        .WithCronSchedule("0 0 0 1 * ?"));

    // --- 难点② 自动扣款：每月1/2/3日凌晨 ---
    var deductJobKey = new JobKey("AutoDeductJob");
    q.AddJob<AutoDeductJob>(opts => opts.WithIdentity(deductJobKey));
    q.AddTrigger(opts => opts.ForJob(deductJobKey).WithIdentity("DeductDay1")
        .UsingJobData("attemptNo", 1).WithCronSchedule("0 5 0 1 * ?"));
    q.AddTrigger(opts => opts.ForJob(deductJobKey).WithIdentity("DeductDay2")
        .UsingJobData("attemptNo", 2).WithCronSchedule("0 5 0 2 * ?"));
    q.AddTrigger(opts => opts.ForJob(deductJobKey).WithIdentity("DeductDay3")
        .UsingJobData("attemptNo", 3).WithCronSchedule("0 5 0 3 * ?"));

    // 断电判定：每月3日凌晨
    var powerCutKey = new JobKey("PowerCutJob");
    q.AddJob<PowerCutJob>(opts => opts.WithIdentity(powerCutKey));
    q.AddTrigger(opts => opts.ForJob(powerCutKey).WithIdentity("PowerCutTrigger")
        .WithCronSchedule("0 10 0 3 * ?"));

    // 恢复供电巡检：每分钟
    var restoreKey = new JobKey("RestorePowerJob");
    q.AddJob<RestorePowerJob>(opts => opts.WithIdentity(restoreKey));
    q.AddTrigger(opts => opts.ForJob(restoreKey).WithIdentity("RestorePowerTrigger")
        .WithCronSchedule("0 * * * * ?"));

    // --- 难点③ 设施预约巡检：每15秒 ---
    var expireKey = new JobKey("ExpireBookingJob");
    q.AddJob<ExpireBookingJob>(opts => opts.WithIdentity(expireKey));
    q.AddTrigger(opts => opts.ForJob(expireKey).WithIdentity("ExpireTrigger")
        .WithCronSchedule("0/15 * * * * ?"));

    var autoKey = new JobKey("AutoCompleteJob");
    q.AddJob<AutoCompleteJob>(opts => opts.WithIdentity(autoKey));
    q.AddTrigger(opts => opts.ForJob(autoKey).WithIdentity("AutoCompleteTrigger")
        .WithCronSchedule("0/15 * * * * ?"));
});
builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

// ===== 7. CORS 配置（允许前端跨域）=====
builder.Services.AddCors(options =>
{
    options.AddPolicy("DevCors", policy =>
    {
        policy.WithOrigins("http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// ===== JWT 认证配置 =====
var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new Exception("JWT Key 未配置，请在 User Secrets 中设置。");
var key = Encoding.UTF8.GetBytes(jwtKey);
var serviceKey = builder.Configuration["ServiceKey:Shared"];
if (string.IsNullOrWhiteSpace(serviceKey))
{
    throw new Exception("ServiceKey:Shared 未配置，请在 User Secrets 中设置。");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(key)
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// ===== 中间件管道 =====
app.UseMiddleware<ExceptionMiddleware>();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//认证与授权
app.UseAuthentication();
app.UseAuthorization();

app.UseCors("DevCors");
app.MapControllers();

app.Run();

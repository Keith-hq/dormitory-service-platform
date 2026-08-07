using Microsoft.EntityFrameworkCore;
using Quartz;
using TemplateDormApi.Data;
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
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException(
            "ConnectionStrings:OracleConnection is not configured. " +
            "Set it with .NET User Secrets or the ConnectionStrings__OracleConnection environment variable.");
    }
    options.UseOracle(connectionString);
});

// ===== 4. 注册 Repository 层 =====
builder.Services.AddScoped<BuildingRepository>();

// ===== 5. 注册 Service 层 =====
builder.Services.AddScoped<IBuildingService, BuildingService>();
builder.Services.AddScoped<IFeeSharingService, FeeSharingService>();
builder.Services.AddScoped<IBillingService, BillingService>();

// ===== 6. 注册 Quartz 定时任务 =====
builder.Services.AddQuartz(q =>
{
    // --- 难点① 水电分摊：每月1日凌晨 ---
    var feeJobKey = new JobKey("FeeSharingJob");
    q.AddJob<FeeSharingJob>(opts => opts.WithIdentity(feeJobKey));
    q.AddTrigger(opts => opts
        .ForJob(feeJobKey)
        .WithIdentity("FeeSharingTrigger")
        .WithCronSchedule("0 0 0 1 * ?"));

    // --- 难点② 自动扣款：每月1/2/3日凌晨 ---
    var deductJobKey = new JobKey("AutoDeductJob");
    q.AddJob<AutoDeductJob>(opts => opts.WithIdentity(deductJobKey));
    // 1日
    q.AddTrigger(opts => opts
        .ForJob(deductJobKey).WithIdentity("DeductDay1")
        .UsingJobData("attemptNo", 1)
        .WithCronSchedule("0 5 0 1 * ?"));
    // 2日
    q.AddTrigger(opts => opts
        .ForJob(deductJobKey).WithIdentity("DeductDay2")
        .UsingJobData("attemptNo", 2)
        .WithCronSchedule("0 5 0 2 * ?"));
    // 3日
    q.AddTrigger(opts => opts
        .ForJob(deductJobKey).WithIdentity("DeductDay3")
        .UsingJobData("attemptNo", 3)
        .WithCronSchedule("0 5 0 3 * ?"));

    // --- 断电判定：每月3日凌晨（扣款之后）---
    var powerCutJobKey = new JobKey("PowerCutJob");
    q.AddJob<PowerCutJob>(opts => opts.WithIdentity(powerCutJobKey));
    q.AddTrigger(opts => opts
        .ForJob(powerCutJobKey)
        .WithIdentity("PowerCutTrigger")
        .WithCronSchedule("0 10 0 3 * ?"));

    // --- 恢复供电巡检：每分钟 ---
    var restoreJobKey = new JobKey("RestorePowerJob");
    q.AddJob<RestorePowerJob>(opts => opts.WithIdentity(restoreJobKey));
    q.AddTrigger(opts => opts
        .ForJob(restoreJobKey)
        .WithIdentity("RestorePowerTrigger")
        .WithCronSchedule("0 * * * * ?"));
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

var app = builder.Build();

// ===== 中间件管道 =====
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("DevCors");
app.MapControllers();

app.Run();

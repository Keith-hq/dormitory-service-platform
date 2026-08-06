using Microsoft.EntityFrameworkCore;
using TemplateDormApi.Data;
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

// ===== 6. CORS 配置（允许前端跨域）=====
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
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new Exception("JWT Key 未配置");
var key = Encoding.UTF8.GetBytes(jwtKey);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
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
app.UseMiddleware<ExceptionMiddleware>();

// ===== 中间件管道 =====
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("DevCors");
app.MapControllers();

app.UseAuthentication();
app.UseAuthorization();

app.Run();

// ============================================================
// 文件：IotGateway.WebAPI/Program.cs
// 用途：ASP.NET Core 应用程序入口（企业级重构版）
//
// 第 3 章版本 vs 第 4 章版本对比：
//   第 3 章：内置 DI + 无数据库 + 无日志 + 无异常处理
//   第 4 章：AutoFac DI + EF Core MySQL + Serilog 日志 + 全局异常过滤
//
// 启动流程：
//   1. 配置 Serilog（必须先于 builder.Build()）
//   2. 注册 EF Core + MySQL（连接数据库）
//   3. 注册框架级服务（Controller, JWT, CORS, Swagger, AutoMapper）
//   4. 注册全局异常过滤器
//   5. 替换 DI 容器为 AutoFac（反射自动注册）
//   6. 配置中间件管道
//   7. 启动应用
// ============================================================

using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Serilog;
using IotGateway.Infrastructure.Data;
using IotGateway.WebAPI.Extensions;
using IotGateway.WebAPI.Filters;

// ===== 第 1 步：配置 Serilog（必须在最前面，确保启动日志也被记录）=====

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()                    // 最低日志级别：Information
    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
    // 覆盖 Microsoft.* 命名空间的日志级别为 Warning
    // 减少 EF Core、ASP.NET Core 框架的噪声日志
    .MinimumLevel.Override("System", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()                       // 添加日志上下文信息（如 TraceId）
    .WriteTo.Console(                              // 控制台输出（开发调试用）
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
        // 输出模板：
        //   {Timestamp:HH:mm:ss} → 时间戳
        //   {Level:u3}           → 日志级别（3个字符大写：INF/WRN/ERR）
        //   {Message:lj}         → 日志消息（lj = 行内 JSON 格式）
        //   {Exception}          → 异常堆栈（如果有）
    .WriteTo.File(                                 // 文件输出（生产排查用）
        path: "logs/log-.txt",                     // 路径模板（- 被替换为日期）
        rollingInterval: RollingInterval.Day,      // 每天滚动一个新文件
        retainedFileCountLimit: 30,                // 保留最近 30 天的日志
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
        // 文件日志包含毫秒时间戳，便于精确排查问题
    .CreateLogger();

try
{
    Log.Information("============================================");
    Log.Information("工业物联网设备全生命周期管理系统 - WebAPI 启动");
    Log.Information("============================================");

    // ===== 第 2 步：创建 WebApplication Builder =====
    var builder = WebApplication.CreateBuilder(args);

    // 替换默认日志为 Serilog
    builder.Host.UseSerilog();
    // UseSerilog()：将 ASP.NET Core 内置的 ILoggerFactory 替换为 Serilog
    // 之后所有使用 ILogger<T> 的地方都会输出到 Serilog

    // ===== 第 3 步：注册 EF Core + MySQL =====
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    // 从 appsettings.json 的 ConnectionStrings:DefaultConnection 读取

    if (string.IsNullOrEmpty(connectionString))
    {
        Log.Warning("数据库连接字符串未配置，使用默认连接");
        connectionString = "Server=localhost;Port=3306;Database=iot_gateway;User=iot_user;Password=Iot@2024;Charset=utf8mb4;";
    }

    builder.Services.AddDbContext<AppDbContext>(options =>
    {
        options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
        // UseMySql：使用 Pomelo.EntityFrameworkCore.MySql 驱动
        // ServerVersion.AutoDetect()：自动检测 MySQL 版本
        //   避免版本不匹配导致 SQL 语法错误
    });

    // ===== 第 4 步：注册框架级服务 =====

    // 4.1 注册 Controller（并添加全局异常过滤器）
    builder.Services.AddControllers(options =>
    {
        options.Filters.Add<GlobalExceptionFilter>();
        // 添加全局异常过滤器到 Controller 管道
        // 每个 Controller 的每个方法抛出的异常都会被此过滤器捕获
    });

    // 4.2 JWT 认证（和第 3 章相同）
    var jwtKey = builder.Configuration["Jwt:Key"]
        ?? "IotGateway_SuperSecret_Key_2024_Must_Be_At_Least_32_Chars!";

    builder.Services.AddAuthentication("Bearer")
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "IotGateway",

                ValidateAudience = true,
                ValidAudience = builder.Configuration["Jwt:Audience"] ?? "IotGateway_Client",

                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                    System.Text.Encoding.UTF8.GetBytes(jwtKey)),

                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(5)
            };
        });

    // 4.3 CORS（允许 WPF 客户端和 Vue3 前端跨域访问）
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowAll", policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
    });

    // 4.4 Swagger（API 文档）
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
    // 参数版本：builder.Services.AddSwaggerGen(options => { ... }) 可自定义标题、版本等

    // 4.5 AutoMapper 由 AutoFac 模块注册（见 Extensions/AutofacModule.cs）
    // IMapper 使用 MapperConfiguration 创建并注册为单例

    // ===== 第 5 步：替换 DI 容器为 AutoFac =====
    builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
    // UseServiceProviderFactory：将内置 DI 替换为 AutoFac
    // 内置 DI 仍然可用（AddControllers/AddDbContext 等），
    // AutoFac 作为"第三方 DI 增强层"接管所有依赖解析

    builder.Host.ConfigureContainer<ContainerBuilder>(containerBuilder =>
    {
        // 注册 AutoFac 模块
        // AutofacModule.Load() 中使用反射自动注册所有 Service/Repository/Controller
        containerBuilder.RegisterModule<AutofacModule>();
        Log.Information("AutoFac 容器配置完成，已注册 AutofacModule");
    });

    // ===== 第 6 步：构建 + 配置中间件管道 =====
    var app = builder.Build();

    // 中间件顺序：严格遵守！
    // Swagger → CORS → 请求日志 → 认证 → 授权 → Controller

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseCors("AllowAll");

    // Serilog 请求日志中间件
    // 自动记录每个 HTTP 请求的方法、路径、状态码、耗时
    app.UseSerilogRequestLogging(options =>
    {
        options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} → {StatusCode} 耗时 {Elapsed:0.000}ms";
    });

    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();

    // ===== 第 7 步：启动应用 =====
    Log.Information("WebAPI 启动成功，监听地址：{Urls}",
        string.Join(", ", app.Urls));
    app.Run();
}
catch (Exception ex)
{
    // 启动失败（如数据库连接失败、端口占用等）
    Log.Fatal(ex, "应用启动失败！");
}
finally
{
    // 确保所有日志被刷新到文件
    Log.CloseAndFlush();
}

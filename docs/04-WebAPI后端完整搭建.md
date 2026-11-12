# 第四章：WebAPI 后端企业级架构搭建

> **学习目标**：将第三章的"教学演示级"WebAPI 升级为"企业级"多层架构  
> **教学方式**：模块化推进，每完成一个模块、理解一个概念，再进入下一个模块  
> **本章重点**：多层项目分层、仓储模式、工作单元、EF Core + MySQL、Serilog、AutoMapper、AutoFac、统一响应格式、全局异常处理、RBAC 权限表设计

---

## 目录

- [4.0 章前概述：第三章的问题清单](#40-章前概述第三章的问题清单)
- [模块一：项目架构认知 —— 为什么要分层？](#模块一项目架构认知--为什么要分层)
- [模块二：Common 公共层 —— 统一规范先行](#模块二common-公共层--统一规范先行)
- [模块三：Domain 领域层 —— 先用 User 实体入门](#模块三domain-领域层--先用-user-实体入门)
- [模块四：Infrastructure 基础设施层 —— 数据库访问三件套](#模块四infrastructure-基础设施层--数据库访问三件套)
- [模块五：Application 应用层 —— 业务逻辑的主角](#模块五application-应用层--业务逻辑的主角)
- [模块六：WebAPI 表示层改造 —— AutoFac + Serilog + 全局异常过滤](#模块六webapi-表示层改造--autofac--serilog--全局异常过滤)
- [模块七：重构 AuthController](#模块七重构-authcontroller)
- [模块八：RBAC 权限模块 —— 精细化权限控制](#模块八rbac-权限模块--精细化权限控制)
- [模块九：代码上传 Gitee/GitHub](#模块九代码上传-giteegithub)
- [模块十：运行验证与本章总结](#模块十运行验证与本章总结)

---

## 4.0 章前概述：第三章的问题清单

### 4.0.1 第三章我们做了什么？

第三章完成后，我们有了这样一个系统：

```
WPF 客户端 → HTTP POST /api/auth/login → AuthController → 生成 JWT → 返回 Token
```

能跑，但只是"教学演示级"，离真正的企业级还差很远。

### 4.0.2 问题清单

打开第三章的 `AuthController.cs`，逐行审视：

| # | 问题 | 代码证据 | 风险 |
|---|------|----------|------|
| ① | **用户数据硬编码** | `var users = new Dictionary<string, ...>` 写死 3 个用户 | 🔴 高 |
| ② | **密码明文比对** | `if (userInfo.Password == password)` | 🔴 高 |
| ③ | **返回格式不统一** | `new { Code = 200, Message = "登录成功", Data = ... }` 匿名对象 | 🟡 中 |
| ④ | **异常处理分散** | 每个方法写 try-catch | 🟡 中 |
| ⑤ | **无日志记录** | 登录成功/失败无痕迹 | 🔴 高 |
| ⑥ | **依赖注入受限** | `builder.Services.AddXxx()` 逐个注册 | 🟡 中 |
| ⑦ | **业务逻辑混在 Controller** | 验证用户、生成 Token 全在一个文件 | 🟡 中 |
| ⑧ | **无权限模型** | `role` 只是字符串字段 | 🟡 中 |

### 4.0.3 第四章的解决方法

```
问题①+② → 模块四（EF Core + MySQL + BCrypt）
问题③   → 模块二（ApiResponse<T>）
问题④   → 模块六（GlobalExceptionFilter）
问题⑤   → 模块六（Serilog 日志）
问题⑥   → 模块六（AutoFac 反射扫描）
问题⑦   → 模块五（Service 层）+ 模块七（重构 Controller）
问题⑧   → 模块八（RBAC 四张表）
```

### 4.0.4 本章完成后的架构

```
wpf_wms/
├── IotGateway.Common/          ← 公共层（ApiResponse + 异常体系）
├── IotGateway.Domain/          ← 领域层（Entity 实体类）
├── IotGateway.Infrastructure/  ← 基础设施层（EF Core + Repository + UoW）
├── IotGateway.Application/     ← 应用层（Service + DTO + AutoMapper）
├── IotGateway.WebAPI/          ← 表示层（Controller + AutoFac + Serilog + Filter）
├── sql/                        ← 数据库脚本
│   ├── 01-init-database.sql
│   └── 02-rbac-tables.sql
└── docs/                       ← 文档
    └── 04-WebAPI后端完整搭建.md
```

---

## 模块一：项目架构认知 —— 为什么要分层？

### 1.1 单体 vs 多层：用餐厅类比

如果把软件项目比作一家餐厅：

| 餐厅岗位 | 职责 | 对应项目层 |
|----------|------|-----------|
| 菜单设计 | 定义菜品名称、价格 | Domain（Entity） |
| 后厨 | 做菜，不关心顾客是谁 | Application（Service） |
| 仓库管理 | 进货、库存 | Infrastructure（Repository） |
| 服务员 | 接待顾客、传菜 | WebAPI（Controller） |
| 公司制度 | 统一收款流程、投诉处理 | Common（ApiResponse） |

**单体项目** = 服务员同时管仓库、炒菜、收银。小店能应付，大店必乱。

**多层项目** = 各司其职，通过"接口"（传菜单）协作。

### 1.2 依赖规则

```
WebAPI ──→ Application ──→ Infrastructure ──→ Domain
  │            │                │                │
  └────────────┴──────→ Common ←────────────────┘
```

- ✅ 上层可以引用下层（WebAPI 可以调用 Application 的类）
- ❌ 下层**不能**引用上层（Domain 不能调用 WebAPI 的类）

就像大楼：**地基（Domain）不知道楼上住的是谁，但楼上（WebAPI）必须知道地基在哪。**

### 1.3 动手：创建 5 个项目

```bash
cd wpf_wms

# 1. 创建 4 个类库 + 1 个 WebAPI
dotnet new classlib -n IotGateway.Common       -o IotGateway.Common
dotnet new classlib -n IotGateway.Domain       -o IotGateway.Domain
dotnet new classlib -n IotGateway.Infrastructure -o IotGateway.Infrastructure
dotnet new classlib -n IotGateway.Application   -o IotGateway.Application
# WebAPI 第三章已创建，跳过

# 2. 建立引用关系（上层引用下层）
dotnet add IotGateway.Infrastructure reference IotGateway.Domain
dotnet add IotGateway.Infrastructure reference IotGateway.Common
dotnet add IotGateway.Application reference IotGateway.Domain
dotnet add IotGateway.Application reference IotGateway.Common
dotnet add IotGateway.Application reference IotGateway.Infrastructure
dotnet add IotGateway.WebAPI reference IotGateway.Application
dotnet add IotGateway.WebAPI reference IotGateway.Infrastructure
dotnet add IotGateway.WebAPI reference IotGateway.Common

# 3. 加入解决方案
dotnet sln add IotGateway.Common IotGateway.Domain IotGateway.Infrastructure IotGateway.Application IotGateway.WebAPI

# 4. 编译验证
dotnet build
# 预期：Build succeeded. 0 Error(s)
```

> 如果 SDK 不支持 net8.0，创建后手动改 `.csproj` 中的 `<TargetFramework>net8.0</TargetFramework>`。

### 1.4 模块一知识小结

| 知识点 | 一句话理解 |
|--------|-----------|
| 分层架构 | 按职责分项目，像公司分部门 |
| 依赖方向 | 只能上层引用下层 |
| Domain 层 | 最底层，纯数据模型 |
| `dotnet add reference` | 建立项目间"认识"关系 |

---

## 模块二：Common 公共层 —— 统一规范先行

> **设计思路**：开工之前先定规矩。就像建筑工地先拉围栏、标安全规范，写代码前先把"返回值长什么样"、"异常怎么分类"定下来。

### 2.1 统一响应格式 ApiResponse\<T\>

#### 2.1.1 为什么需要？

第三章每个接口返回格式都不一样：

```csharp
// 登录成功
return Ok(new { Code = 200, Message = "登录成功", Data = ... });
// 某个查询
return Ok(new { code = 0, msg = "查询成功", data = list });  // 命名不一致！
// 错误
return BadRequest(new { error = "参数错误" });                // 结构完全不同！
```

WPF 客户端要写 3 套解析代码。

**ApiResponse\<T\> 解决思路**：所有接口返回同一个格式：

```json
{ "code": 200, "message": "操作成功", "data": { ... }, "traceId": "abc123" }
```

#### 2.1.2 什么是泛型 `<T>`？

```csharp
// 没有泛型 → 每种类型写一个类（噩梦）
public class ApiResponseInt    { public int Data { get; set; } }
public class ApiResponseString { public string Data { get; set; } }
public class ApiResponseUser   { public User Data { get; set; } }
// 每多一种数据类型，就多一个类！

// 有泛型 → 一个类通吃所有类型
public class ApiResponse<T> { public T Data { get; set; } }
// T 是占位符，使用时指定：
// ApiResponse<int>     → Data 是 int
// ApiResponse<UserDto> → Data 是 UserDto
```

#### 2.1.3 完整代码

**创建文件**：`IotGateway.Common/Helpers/ApiResponse.cs`

```csharp
namespace IotGateway.Common.Helpers;

/// <summary>
/// 统一 API 响应格式（泛型版本）
/// 所有 Controller 都返回这个格式
/// </summary>
public class ApiResponse<T>
{
    /// <summary>业务状态码（200=成功, 400=业务错误, 401=未授权...）</summary>
    public int Code { get; set; }

    /// <summary>提示消息</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>业务数据（类型 T 由调用方指定）</summary>
    public T? Data { get; set; }

    /// <summary>请求追踪 ID（出问题时拿它去查日志）</summary>
    public string? TraceId { get; set; }

    // ===== 静态工厂方法：快速创建常用响应 =====

    /// <summary>成功响应</summary>
    public static ApiResponse<T> Ok(T data, string message = "操作成功")
    {
        return new ApiResponse<T>
        {
            Code = 200,
            Message = message,
            Data = data,
            TraceId = System.Diagnostics.Activity.Current?.Id
        };
    }

    /// <summary>失败响应</summary>
    public static ApiResponse<T> Fail(string message, int code = 400)
    {
        return new ApiResponse<T>
        {
            Code = code,
            Message = message,
            TraceId = System.Diagnostics.Activity.Current?.Id
        };
    }

    /// <summary>未授权响应</summary>
    public static ApiResponse<T> Unauthorized(string message = "未授权访问")
    {
        return new ApiResponse<T>
        {
            Code = 401,
            Message = message,
            TraceId = System.Diagnostics.Activity.Current?.Id
        };
    }

    /// <summary>资源未找到响应</summary>
    public static ApiResponse<T> NotFound(string message = "资源未找到")
    {
        return new ApiResponse<T>
        {
            Code = 404,
            Message = message,
            TraceId = System.Diagnostics.Activity.Current?.Id
        };
    }

    /// <summary>服务器错误响应</summary>
    public static ApiResponse<T> Error(string message = "服务器内部错误")
    {
        return new ApiResponse<T>
        {
            Code = 500,
            Message = message,
            TraceId = System.Diagnostics.Activity.Current?.Id
        };
    }
}

/// <summary>
/// 非泛型 ApiResponse（不需要返回 Data 的场景）
/// </summary>
public class ApiResponse
{
    public int Code { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? TraceId { get; set; }

    public static ApiResponse Ok(string msg = "操作成功")
        => new() { Code = 200, Message = msg, TraceId = System.Diagnostics.Activity.Current?.Id };

    public static ApiResponse Fail(string msg, int code = 400)
        => new() { Code = code, Message = msg, TraceId = System.Diagnostics.Activity.Current?.Id };

    public static ApiResponse Unauthorized(string msg = "未授权访问")
        => new() { Code = 401, Message = msg, TraceId = System.Diagnostics.Activity.Current?.Id };

    public static ApiResponse NotFound(string msg = "资源未找到")
        => new() { Code = 404, Message = msg, TraceId = System.Diagnostics.Activity.Current?.Id };

    public static ApiResponse Error(string msg = "服务器内部错误")
        => new() { Code = 500, Message = msg, TraceId = System.Diagnostics.Activity.Current?.Id };
}
```

#### 2.1.4 使用示例

```csharp
// 成功返回数据
return Ok(ApiResponse<UserDto>.Ok(user, "登录成功"));
// → {"code":200,"message":"登录成功","data":{...}}

// 失败
return Ok(ApiResponse<UserDto>.Fail("用户名或密码错误"));
// → {"code":400,"message":"用户名或密码错误","data":null}
```

### 2.2 自定义异常体系

#### 2.2.1 传统方式 vs 异常驱动方式

**传统方式**——每个 Controller 方法都写 try-catch：

```csharp
[HttpPost("login")]
public IActionResult Login(LoginRequest req) {
    try {
        if (string.IsNullOrEmpty(req.Username))
            return Ok(new { Code = 422, Message = "用户名不能为空" });
        // ... 每步都要想"怎么返回、返回什么格式"
    } catch (Exception ex) {
        return Ok(new { Code = 500, Message = ex.Message });
    }
}
```

**异常驱动方式**——Service 直接 throw，Filter 统一处理：

```csharp
// Service 中：
if (string.IsNullOrEmpty(request.Username))
    throw new ValidationException("用户名不能为空");  // 直接抛！
// 不需要管 HTTP 状态码、响应格式

// Controller 中：
public async Task<IActionResult> Login(LoginRequestDto request) {
    var result = await _authService.LoginAsync(request);
    return Ok(ApiResponse<LoginResponseDto>.Ok(result, "登录成功"));
    // 没有 try-catch！
}
```

#### 2.2.2 异常类层次结构

```
System.Exception
  └── AppException（自定义基类，多了 StatusCode 属性）
        ├── BusinessException      → HTTP 400
        │     └── ValidationException → HTTP 422
        ├── NotFoundException      → HTTP 404
        ├── UnauthorizedException  → HTTP 401
        └── ForbiddenException     → HTTP 403
```

#### 2.2.3 完整代码

**创建文件**：`IotGateway.Common/Exceptions/AppException.cs`

```csharp
namespace IotGateway.Common.Exceptions;

/// <summary>应用异常基类</summary>
public class AppException : Exception
{
    /// <summary>HTTP 状态码（GlobalExceptionFilter 用这个字段设置 HTTP 响应）</summary>
    public int StatusCode { get; set; } = 500;

    public AppException(string message) : base(message) { }
    public AppException(string message, int statusCode) : base(message)
    {
        StatusCode = statusCode;
    }
}

/// <summary>业务逻辑异常 → HTTP 400</summary>
public class BusinessException : AppException
{
    public BusinessException(string message) : base(message, 400) { }
}

/// <summary>资源未找到 → HTTP 404</summary>
public class NotFoundException : AppException
{
    public NotFoundException(string message) : base(message, 404) { }
    public NotFoundException(string resourceName, object key)
        : base($"未找到 {resourceName}（ID: {key}）", 404) { }
}

/// <summary>未授权 → HTTP 401</summary>
public class UnauthorizedException : AppException
{
    public UnauthorizedException(string msg = "未授权访问") : base(msg, 401) { }
}

/// <summary>权限不足 → HTTP 403</summary>
public class ForbiddenException : AppException
{
    public ForbiddenException(string msg = "权限不足") : base(msg, 403) { }
}

/// <summary>数据验证失败 → HTTP 422</summary>
public class ValidationException : AppException
{
    public ValidationException(string message) : base(message, 422) { }
}
```

### 2.3 模块二完成检查

```bash
cd IotGateway.Common && dotnet build
# 预期：Build succeeded.
```

Common 层完成 2 个文件：
- `Helpers/ApiResponse.cs` —— 统一响应格式
- `Exceptions/AppException.cs` —— 6 种异常类型

### 2.4 模块二知识小结

| 知识点 | 一句话理解 |
|--------|-----------|
| ApiResponse\<T\> | 统一响应格式，泛型 T 保证 Data 类型安全 |
| 泛型 `<T>` | 占位符，使用时再指定具体类型 |
| 静态工厂方法 | `ApiResponse.Ok(data, msg)` 比 `new ApiResponse{...}` 更语义化 |
| 异常驱动设计 | Service 抛异常 → Filter 自动捕获 → 自动转 JSON |
| AppException | 自定义异常基类，多了 StatusCode 属性 |

---

## 模块三：Domain 领域层 —— 先用 User 实体入门

> **设计思路**：不一次性创建 22 个实体。本模块只创建 User 实体，学会 Data Annotations 的基本用法。后续模块按需创建。

### 3.1 什么是 Entity（实体类）？

Entity 是数据库表在 C# 中的"影子"。每一行数据库记录，在 C# 中就是一个 Entity 对象。

```
MySQL 表 users：                         C# 类 User：
┌────┬──────────┬──────────────┐         class User {
│ id │ username │ password_hash│           long Id;
├────┼──────────┼──────────────┤           string Username;
│ 1  │ admin    │ $2a$11$...   │    →      string PasswordHash;
│ 2  │ operator │ $2a$11$...   │           string Email;
└────┴──────────┴──────────────┘           ...
                                          }

查数据库 → EF Core 自动创建 User 对象 → 代码操作对象 → EF Core 写回数据库
```

**ORM（Object-Relational Mapping，对象关系映射）**：把"表←→对象"的转换自动化。

### 3.2 什么是 Data Annotations？

通过 `[特性]` 标记 C# 属性，告诉 EF Core 如何映射到数据库列。

| 特性 | 做什么 | 示例 |
|------|--------|------|
| `[Table("表名")]` | 映射到哪个表 | `[Table("users")]` |
| `[Key]` | 主键 | `[Key] public long Id` |
| `[Column("列名")]` | 映射到哪个列 | `[Column("password_hash")]` |
| `[Required]` | NOT NULL | `[Required]` |
| `[MaxLength(50)]` | VARCHAR(50) | `[MaxLength(50)]` |
| `[DatabaseGenerated(Identity)]` | 自增 | 用在 Id 上 |

> 为什么手动写 Entity 而不是 `Scaffold-DbContext` 一键生成？
> 手动写能理解每个特性的含义，之后改表结构也更好控制。

### 3.3 动手：创建 User 实体

**创建文件**：`IotGateway.Domain/Entities/User.cs`

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IotGateway.Domain.Entities;

/// <summary>
/// 用户实体 —— 一个 User 对象 = users 表中的一行数据
/// </summary>
[Table("users")]    // 数据库表名
public class User
{
    [Key]                                       // 主键
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]  // 自增
    [Column("id")]
    public long Id { get; set; }

    [Required]                                  // NOT NULL
    [MaxLength(50)]                             // VARCHAR(50)
    [Column("username")]
    public string Username { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    [Column("password_hash")]   // ← C# PascalCase → SQL snake_case
    public string PasswordHash { get; set; } = string.Empty;
    // 🔒 永远不存明文密码！只存 BCrypt 哈希（如 $2a$11$...）

    [Required]
    [MaxLength(50)]
    [Column("real_name")]
    public string RealName { get; set; } = string.Empty;

    [MaxLength(100)]
    [Column("email")]
    public string Email { get; set; } = string.Empty;

    [MaxLength(20)]
    [Column("phone")]
    public string Phone { get; set; } = string.Empty;

    [MaxLength(500)]
    [Column("avatar_url")]
    public string AvatarUrl { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    [Column("role")]
    public string Role { get; set; } = "Operator";

    [Column("status")]
    public sbyte Status { get; set; } = 1;      // TINYINT → sbyte

    [Column("last_login_at")]
    public DateTime? LastLoginAt { get; set; }   // ? = 可为 NULL

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
```

### 3.4 C# vs MySQL 命名约定

```
C# PascalCase          [Column("...")]         MySQL snake_case
───────────────        ─────────────           ────────────────
PasswordHash      →    password_hash      →    password_hash
RealName          →    real_name          →    real_name
LastLoginAt       →    last_login_at      →    last_login_at
```

### 3.5 模块三知识小结

| 知识点 | 一句话理解 |
|--------|-----------|
| Entity | 数据库表的 C# 映射，一行数据 = 一个对象 |
| ORM | 对象关系映射，自动转换"表←→对象" |
| `[Table]` `[Key]` `[Column]` | 三大核心 Data Annotations |
| Domain 层 | 纯数据模型，不依赖任何框架 |

---

## 模块四：Infrastructure 基础设施层 —— 数据库访问三件套

> **设计思路**：DbContext（数据库会话）、Repository（仓储）、UnitOfWork（工作单元）三者层层递进。

### 4.1 安装 NuGet 包

```bash
cd IotGateway.Infrastructure
dotnet add package Pomelo.EntityFrameworkCore.MySql
dotnet add package BCrypt.Net-Next
```

### 4.2 DbContext —— 数据库会话

#### 4.2.1 什么是 DbContext？

DbContext 好比**一次数据库通话**：

```
DbContext 的四项职责：
┌─────────────────────────────────────────┐
│ 1. 连接管理   打开→保持→关闭连接          │
│ 2. 查询翻译   LINQ（C#代码）→ SQL        │
│ 3. 变更追踪   记录哪些对象被修改了         │
│ 4. 事务管理   Begin/Commit/Rollback      │
└─────────────────────────────────────────┘
```

#### 4.2.2 动手：创建 AppDbContext

**创建文件**：`IotGateway.Infrastructure/Data/AppDbContext.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using IotGateway.Domain.Entities;

namespace IotGateway.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // DbSet：每张表的入口。LINQ 查询翻译为 SQL。
    public DbSet<User> Users => Set<User>();

    // Fluent API：做 Data Annotations 做不到的事（索引、唯一约束）
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(e => e.Username).IsUnique()
                .HasDatabaseName("uk_username");
            entity.HasIndex(e => e.Role).HasDatabaseName("idx_role");
        });
    }
}
```

**Data Annotations vs Fluent API 分工**：

| 功能 | Data Annotations | Fluent API |
|------|:---:|:---:|
| 表名/列名/主键/NOT NULL/长度 | ✅ | ✅ |
| **唯一索引** | ❌ | ✅ `HasIndex().IsUnique()` |
| **复合主键** | ❌ | ✅ `HasKey(e => new {e.A, e.B})` |

**本章策略**：基本映射用 Annotations（直观），高级配置用 Fluent API（功能全）。

### 4.3 Repository —— 仓储模式

#### 4.3.1 什么是仓储模式？

**没有仓储**：Controller 直接操作 DbContext → Controller 要知道 EF Core API → 换 ORM 要改所有 Controller。

**使用仓储**：Controller → IRepository\<T\>（接口）→ Repository\<T\>（实现）→ DbContext。Controller 只知道接口，不关心实现。

#### 4.3.2 什么是 Expression\<Func\<T, bool\>\>？

```csharp
// 这行代码：
var user = await _repo.FirstOrDefaultAsync(u => u.Username == "admin");

// 参数类型是 Expression<Func<User, bool>>
// EF Core 解析 lambda 表达式树，翻译为 SQL：
// SELECT * FROM users WHERE username = 'admin' LIMIT 1
//
// 如果是普通 Func<User,bool>，EF Core 无法翻译 → 全表查出再内存过滤！
```

#### 4.3.3 动手：创建 IRepository\<T\>

**创建文件**：`IotGateway.Infrastructure/Repositories/IRepository.cs`

```csharp
using System.Linq.Expressions;

namespace IotGateway.Infrastructure.Repositories;

/// <summary>通用仓储接口</summary>
public interface IRepository<TEntity> where TEntity : class
{
    // 查询
    Task<TEntity?> GetByIdAsync(long id);
    Task<List<TEntity>> GetAllAsync();
    Task<List<TEntity>> FindAsync(Expression<Func<TEntity, bool>> predicate);
    Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate);
    Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate);
    Task<int> CountAsync(Expression<Func<TEntity, bool>>? predicate = null);

    // 分页
    Task<(List<TEntity> Items, int TotalCount)> GetPagedAsync(
        int pageIndex, int pageSize,
        Expression<Func<TEntity, bool>>? predicate = null,
        Expression<Func<TEntity, object>>? orderBy = null,
        bool ascending = true);

    // 写操作
    Task AddAsync(TEntity entity);
    Task AddRangeAsync(IEnumerable<TEntity> entities);
    void Update(TEntity entity);
    void Delete(TEntity entity);
    void DeleteRange(IEnumerable<TEntity> entities);
}
```

#### 4.3.4 动手：实现 Repository\<T\>

**创建文件**：`IotGateway.Infrastructure/Repositories/Repository.cs`

```csharp
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using IotGateway.Infrastructure.Data;

namespace IotGateway.Infrastructure.Repositories;

public class Repository<TEntity> : IRepository<TEntity> where TEntity : class
{
    protected readonly AppDbContext _context;
    protected readonly DbSet<TEntity> _dbSet;

    public Repository(AppDbContext context)
    {
        _context = context;
        _dbSet = context.Set<TEntity>();
    }

    public virtual async Task<TEntity?> GetByIdAsync(long id)
        => await _dbSet.FindAsync(id);

    public virtual async Task<List<TEntity>> GetAllAsync()
        => await _dbSet.ToListAsync();

    public virtual async Task<List<TEntity>> FindAsync(
        Expression<Func<TEntity, bool>> predicate)
        => await _dbSet.Where(predicate).ToListAsync();

    public virtual async Task<TEntity?> FirstOrDefaultAsync(
        Expression<Func<TEntity, bool>> predicate)
        => await _dbSet.FirstOrDefaultAsync(predicate);

    public virtual async Task<bool> AnyAsync(
        Expression<Func<TEntity, bool>> predicate)
        => await _dbSet.AnyAsync(predicate);

    public virtual async Task<int> CountAsync(
        Expression<Func<TEntity, bool>>? predicate = null)
        => predicate == null
            ? await _dbSet.CountAsync()
            : await _dbSet.CountAsync(predicate);

    public virtual async Task<(List<TEntity> Items, int TotalCount)> GetPagedAsync(
        int pageIndex, int pageSize,
        Expression<Func<TEntity, bool>>? predicate = null,
        Expression<Func<TEntity, object>>? orderBy = null,
        bool ascending = true)
    {
        IQueryable<TEntity> query = _dbSet;
        if (predicate != null) query = query.Where(predicate);
        int totalCount = await query.CountAsync();
        if (orderBy != null)
            query = ascending ? query.OrderBy(orderBy) : query.OrderByDescending(orderBy);
        var items = await query.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToListAsync();
        return (items, totalCount);
    }

    public virtual async Task AddAsync(TEntity entity)
        => await _dbSet.AddAsync(entity);

    public virtual async Task AddRangeAsync(IEnumerable<TEntity> entities)
        => await _dbSet.AddRangeAsync(entities);

    public virtual void Update(TEntity entity)
        => _dbSet.Update(entity);

    public virtual void Delete(TEntity entity)
        => _dbSet.Remove(entity);

    public virtual void DeleteRange(IEnumerable<TEntity> entities)
        => _dbSet.RemoveRange(entities);
}
```

### 4.4 UnitOfWork —— 工作单元

#### 4.4.1 什么是工作单元？

**类比：银行转账**

```
操作 1：A 账户 -100 元
操作 2：B 账户 +100 元
→ 如果操作 1 之后系统崩溃 → 钱消失了！
→ 事务：BEGIN → 操作1 → 操作2 → COMMIT（全成功）或 ROLLBACK（全撤销）
```

UnitOfWork 就是管理这些操作的"事务管理器"：

```csharp
await _unitOfWork.Users.Update(user);        // 表 1：更新登录时间
await _unitOfWork.AuditLogs.AddAsync(log);   // 表 2：插入审计日志
await _unitOfWork.SaveChangesAsync();        // 一起提交 → 事务保证
```

#### 4.4.2 动手：创建 IUnitOfWork + UnitOfWork

**创建文件**：`IotGateway.Infrastructure/Repositories/IUnitOfWork.cs`

```csharp
using IotGateway.Domain.Entities;

namespace IotGateway.Infrastructure.Repositories;

public interface IUnitOfWork : IDisposable
{
    IRepository<User> Users { get; }
    IRepository<AuditLog> AuditLogs { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task BeginTransactionAsync();
    Task CommitTransactionAsync();
    Task RollbackTransactionAsync();
}
```

**创建文件**：`IotGateway.Infrastructure/Repositories/UnitOfWork.cs`

```csharp
using Microsoft.EntityFrameworkCore.Storage;
using IotGateway.Domain.Entities;
using IotGateway.Infrastructure.Data;

namespace IotGateway.Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;
    private IDbContextTransaction? _currentTransaction;

    // 懒加载：用到哪个 Repository 才创建哪个
    private IRepository<User>? _users;
    private IRepository<AuditLog>? _auditLogs;

    public UnitOfWork(AppDbContext context) { _context = context; }

    public IRepository<User> Users =>
        _users ??= new Repository<User>(_context);  // ??= ：如果是 null 就 new

    public IRepository<AuditLog> AuditLogs =>
        _auditLogs ??= new Repository<AuditLog>(_context);

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
        => await _context.SaveChangesAsync(ct);

    public async Task BeginTransactionAsync()
        => _currentTransaction = await _context.Database.BeginTransactionAsync();

    public async Task CommitTransactionAsync()
    {
        if (_currentTransaction != null)
        {
            await _currentTransaction.CommitAsync();
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
        }
    }

    public async Task RollbackTransactionAsync()
    {
        if (_currentTransaction != null)
        {
            await _currentTransaction.RollbackAsync();
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
        }
    }

    public void Dispose()
    {
        _currentTransaction?.Dispose();
        _context.Dispose();
    }
}
```

### 4.5 顺带创建 AuditLog 实体

登录需要写入 `audit_logs` 表，所以顺带创建：

**创建文件**：`IotGateway.Domain/Entities/AuditLog.cs`

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IotGateway.Domain.Entities;

[Table("audit_logs")]
public class AuditLog
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity), Column("id")]
    public long Id { get; set; }

    [Column("user_id")] public long UserId { get; set; }
    [MaxLength(50), Column("username")] public string Username { get; set; } = "";
    [Required, MaxLength(50), Column("action_type")] public string ActionType { get; set; } = "";
    [MaxLength(50), Column("target_type")] public string TargetType { get; set; } = "";
    [MaxLength(100), Column("target_id")] public string TargetId { get; set; } = "";
    [MaxLength(500), Column("description")] public string Description { get; set; } = "";
    [MaxLength(50), Column("request_ip")] public string RequestIp { get; set; } = "";
    [Column("request_params", TypeName = "json")] public string? RequestParams { get; set; }
    [Column("status")] public sbyte Status { get; set; } = 1;
    [MaxLength(500), Column("error_msg")] public string ErrorMsg { get; set; } = "";
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

记得在 `AppDbContext` 中添加：`public DbSet<AuditLog> AuditLogs => Set<AuditLog>();`

### 4.6 模块四知识小结

| 知识点 | 一句话理解 |
|--------|-----------|
| DbContext | 数据库会话，管理连接+查询翻译+变更追踪+事务 |
| DbSet\<T\> | 一张表的入口，LINQ 操作的起点 |
| IRepository\<T\> | 通用仓储接口，封装标准 CRUD |
| Repository\<T\> | 仓储实现，内部用 DbSet 操作 |
| Expression\<Func\> | 表达式树，EF Core 解析为 SQL |
| IUnitOfWork | 事务管理器 + 懒加载 Repository |
| ??= | 懒加载关键：null 则 new，否则复用 |
| 事务 | 一组操作要么全成功、要么全回滚 |

---

## 模块五：Application 应用层 —— 业务逻辑的主角

### 5.1 安装 NuGet 包

```bash
cd IotGateway.Application
dotnet add package AutoMapper
dotnet add package System.IdentityModel.Tokens.Jwt
dotnet add package Microsoft.Extensions.Configuration.Binder
```

### 5.2 什么是 DTO（数据传输对象）？

#### 5.2.1 为什么需要 DTO？

```
User（Entity - 12 字段）                 UserDto（DTO - 8 字段）
┌────────────────────┐                  ┌──────────────────┐
│ Id              ✅ │                  │ Id            ✅ │
│ Username        ✅ │    AutoMapper    │ Username      ✅ │
│ PasswordHash    🔒 │    ────────→     │ RealName      ✅ │
│ RealName        ✅ │    映射          │ Email         ✅ │
│ Email           ✅ │                  │ Phone         ✅ │
│ Phone           ✅ │                  │ AvatarUrl     ✅ │
│ AvatarUrl       ✅ │                  │ Role          ✅ │
│ Role            ✅ │                  │ LastLoginAt   ✅ │
│ Status          ❌ │                  └──────────────────┘
│ LastLoginAt     ✅ │
│ CreatedAt       ❌ │                  🔒 = 敏感字段（不暴露）
│ UpdatedAt       ❌ │                  ❌ = 内部字段（不需要）
└────────────────────┘
```

如果直接把 User Entity 返回给前端，`password_hash` 就泄露了。

#### 5.2.2 DTO 命名约定

| 方向 | 后缀 | 示例 |
|------|------|------|
| 请求（前端→后端） | `RequestDto` | `LoginRequestDto` |
| 响应（后端→前端） | `ResponseDto` | `LoginResponseDto` |
| 展示 | `Dto` | `UserDto` |

### 5.3 动手：创建认证 DTO

**创建文件**：`IotGateway.Application/DTOs/AuthDto.cs`

```csharp
namespace IotGateway.Application.DTOs;

/// <summary>登录请求（前端→后端）</summary>
public class LoginRequestDto
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

/// <summary>登录响应（后端→前端）</summary>
public class LoginResponseDto
{
    public string Token { get; set; } = string.Empty;
    public long ExpiresAt { get; set; }          // Unix 时间戳
    public UserDto User { get; set; } = new();
}

/// <summary>用户信息（前端展示用）</summary>
public class UserDto
{
    public long Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string RealName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTime? LastLoginAt { get; set; }
}
```

### 5.4 什么是 AutoMapper？

手写映射（8 字段 = 8 行，50 实体 = 400 行）：

```csharp
var dto = new UserDto {
    Id = user.Id, Username = user.Username, RealName = user.RealName,
    Email = user.Email, Phone = user.Phone, AvatarUrl = user.AvatarUrl,
    Role = user.Role, LastLoginAt = user.LastLoginAt
};
```

AutoMapper 一句话搞定：

```csharp
var dto = _mapper.Map<UserDto>(user);
// 同名属性自动映射，User 的 PasswordHash 在 DTO 中不存在 → 自动跳过
```

### 5.5 动手：创建 AutoMapper Profile

**创建文件**：`IotGateway.Application/Mappings/AutoMapperProfile.cs`

```csharp
using AutoMapper;
using IotGateway.Domain.Entities;
using IotGateway.Application.DTOs;

namespace IotGateway.Application.Mappings;

public class AutoMapperProfile : Profile
{
    public AutoMapperProfile()
    {
        CreateMap<User, UserDto>();  // User Entity → UserDto（同名自动映射）
    }
}
```

### 5.6 Service 层

#### 5.6.1 Controller vs Service 职责划分

```
Controller（表示层）：          Service（应用层）：
┌─────────────────────┐       ┌─────────────────────────┐
│ ✅ 接收 HTTP 请求    │  调用  │ ✅ 参数校验               │
│ ✅ [FromBody] 绑定   │──────→│ ✅ 业务规则验证            │
│ ✅ 调用 Service      │  返回  │ ✅ 调用 Repository        │
│ ✅ 返回 HTTP 响应    │←──────│ ✅ 生成 JWT Token         │
│ ❌ 不写业务逻辑       │       │ ✅ 事务管理               │
│ ❌ 不写 try-catch    │       │ ❌ 不关心 HTTP 协议        │
└─────────────────────┘       └─────────────────────────┘
```

#### 5.6.2 动手：创建 IAuthService + AuthService

**创建文件**：`IotGateway.Application/Services/IAuthService.cs`

```csharp
using IotGateway.Application.DTOs;

namespace IotGateway.Application.Services;

public interface IAuthService
{
    Task<LoginResponseDto> LoginAsync(LoginRequestDto request);
}
```

**创建文件**：`IotGateway.Application/Services/AuthService.cs`

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AutoMapper;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using IotGateway.Application.DTOs;
using IotGateway.Common.Exceptions;
using IotGateway.Domain.Entities;
using IotGateway.Infrastructure.Repositories;

namespace IotGateway.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly IConfiguration _config;

    public AuthService(IUnitOfWork uow, IMapper mapper, IConfiguration config)
    {
        _uow = uow;
        _mapper = mapper;
        _config = config;
    }

    public async Task<LoginResponseDto> LoginAsync(LoginRequestDto request)
    {
        // 1. 参数校验
        if (string.IsNullOrWhiteSpace(request.Username))
            throw new ValidationException("用户名不能为空");
        if (string.IsNullOrWhiteSpace(request.Password))
            throw new ValidationException("密码不能为空");

        // 2. 查用户
        var user = await _uow.Users.FirstOrDefaultAsync(
            u => u.Username == request.Username);
        if (user == null)
            throw new UnauthorizedException("用户名或密码错误");

        // 3. 验状态
        if (user.Status != 1)
            throw new UnauthorizedException("账户已被禁用");

        // 4. BCrypt 密码验证
        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            await _uow.AuditLogs.AddAsync(new AuditLog
            {
                UserId = user.Id, Username = user.Username,
                ActionType = "Login", Status = 0,
                Description = "登录失败：密码错误"
            });
            await _uow.SaveChangesAsync();
            throw new UnauthorizedException("用户名或密码错误");
        }

        // 5. 生成 JWT
        var token = GenerateJwtToken(user);
        var expiresAt = DateTimeOffset.UtcNow
            .AddHours(_config.GetValue<int>("Jwt:ExpireHours", 8))
            .ToUnixTimeSeconds();

        // 6. 更新登录时间
        user.LastLoginAt = DateTime.UtcNow;
        _uow.Users.Update(user);

        // 7. 记录审计日志
        await _uow.AuditLogs.AddAsync(new AuditLog
        {
            UserId = user.Id, Username = user.Username,
            ActionType = "Login", Status = 1,
            Description = $"用户 {user.Username}（{user.Role}）登录成功"
        });

        // 8. 提交（Update + Add 同一个事务）
        await _uow.SaveChangesAsync();

        // 9. 返回 DTO
        return new LoginResponseDto
        {
            Token = token,
            ExpiresAt = expiresAt,
            User = _mapper.Map<UserDto>(user)
        };
    }

    private string GenerateJwtToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
            _config["Jwt:Key"] ?? "IotGateway_SuperSecret_Key_2024_Must_Be_At_Least_32_Chars!"));

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.GivenName, user.RealName),
            new Claim(ClaimTypes.Role, user.Role),
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"] ?? "IotGateway",
            audience: _config["Jwt:Audience"] ?? "IotGateway_Client",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(_config.GetValue<int>("Jwt:ExpireHours", 8)),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
```

### 5.7 模块五知识小结

| 知识点 | 一句话理解 |
|--------|-----------|
| DTO | 前后端数据传输的安全容器 |
| Entity vs DTO | Entity 映射全字段，DTO 是前端"安全视图" |
| LoginRequestDto | 前端→后端（请求体） |
| LoginResponseDto | 后端→前端（响应体） |
| AutoMapper | `_mapper.Map<UserDto>(user)` 自动映射同名属性 |
| Service 层 | 业务逻辑的家，不依赖 HTTP 协议 |
| BCrypt | `BCrypt.Verify(明文, 哈希)` 安全验证 |
| 异常驱动 | Service 抛异常，Controller 不 try-catch |

---

## 模块六：WebAPI 表示层改造 —— AutoFac + Serilog + 全局异常过滤

### 6.1 安装 NuGet 包

```bash
cd IotGateway.WebAPI
dotnet add package Autofac.Extensions.DependencyInjection
dotnet add package Serilog.AspNetCore
dotnet add package Serilog.Sinks.Console
dotnet add package Serilog.Sinks.File
```

### 6.2 AutoFac —— 反射自动注册

#### 6.2.1 内置 DI 的问题

```csharp
// 30+ 行手动注册
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IDeviceService, DeviceService>();
// ... 重复 30 次
```

#### 6.2.2 AutoFac 反射扫描（3 行 = 30 行）

```csharp
builder.RegisterAssemblyTypes(applicationAssembly)
    .Where(t => t.Name.EndsWith("Service"))   // 所有 *Service 类
    .AsImplementedInterfaces()                 // 自动匹配接口
    .InstancePerLifetimeScope();               // Scoped 生命周期
```

#### 6.2.3 生命周期对照

| AutoFac | 内置 DI | 含义 |
|---------|---------|------|
| `InstancePerDependency()` | `AddTransient()` | 每次注入创建新实例 |
| `InstancePerLifetimeScope()` | `AddScoped()` | 每个 HTTP 请求一个实例 |
| `SingleInstance()` | `AddSingleton()` | 整个应用一个实例 |

### 6.3 Serilog —— 结构化日志

#### 6.3.1 传统日志 vs 结构化日志

```
传统：登录失败：用户 admin 密码错误
→ 只能用 grep 搜索

结构：{"time":"10:30","level":"WRN","user":"admin","action":"login_fail"}
→ 可按字段查询：level=WRN AND action=login_fail
```

#### 6.3.2 双输出（Sink）

- **Console Sink**：开发时终端实时查看 `[10:30:00 INF] 用户 admin 登录成功`
- **File Sink**：按天滚动（`logs/log-20240812.txt`），保留 30 天

### 6.4 GlobalExceptionFilter —— 统一异常处理

```
异常类型                →  HTTP 状态码   →  ApiResponse.Code
───────────────────────────────────────────────────────────
ValidationException     →  422          →  422
UnauthorizedException   →  401          →  401
ForbiddenException      →  403          →  403
NotFoundException       →  404          →  404
BusinessException       →  400          →  400
Exception（未知）        →  500          →  500（隐藏详情）
```

### 6.5 动手：创建三个关键文件

#### 6.5.1 AutofacModule

**创建文件**：`IotGateway.WebAPI/Extensions/AutofacModule.cs`

```csharp
using System.Reflection;
using Autofac;
using AutoMapper;
using Microsoft.Extensions.Logging;
using IotGateway.Application.Mappings;

namespace IotGateway.WebAPI.Extensions;

public class AutofacModule : Autofac.Module
{
    protected override void Load(ContainerBuilder builder)
    {
        var webApiAssembly = Assembly.GetExecutingAssembly();
        var applicationAssembly = Assembly.Load("IotGateway.Application");
        var infrastructureAssembly = Assembly.Load("IotGateway.Infrastructure");

        // 自动注册 Service
        builder.RegisterAssemblyTypes(applicationAssembly)
            .Where(t => t.Name.EndsWith("Service"))
            .AsImplementedInterfaces().InstancePerLifetimeScope();

        // 自动注册 Repository
        builder.RegisterAssemblyTypes(infrastructureAssembly)
            .Where(t => t.Name.EndsWith("Repository"))
            .AsImplementedInterfaces().InstancePerLifetimeScope();

        // 自动注册 UnitOfWork
        builder.RegisterAssemblyTypes(infrastructureAssembly)
            .Where(t => t.Name.EndsWith("UnitOfWork"))
            .AsImplementedInterfaces().InstancePerLifetimeScope();

        // Controller + Filter
        builder.RegisterAssemblyTypes(webApiAssembly)
            .Where(t => t.Name.EndsWith("Controller")).PropertiesAutowired();
        builder.RegisterAssemblyTypes(webApiAssembly)
            .Where(t => t.Name.EndsWith("Filter")).InstancePerLifetimeScope();

        // AutoMapper
        builder.Register(ctx =>
        {
            var loggerFactory = ctx.Resolve<ILoggerFactory>();
            var config = new MapperConfiguration(cfg =>
            {
                cfg.AddProfile<AutoMapperProfile>();
            }, loggerFactory);  // AutoMapper 16.x 需要 ILoggerFactory
            return config.CreateMapper();
        }).As<IMapper>().SingleInstance();
    }
}
```

#### 6.5.2 GlobalExceptionFilter

**创建文件**：`IotGateway.WebAPI/Filters/GlobalExceptionFilter.cs`

```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using IotGateway.Common.Exceptions;
using IotGateway.Common.Helpers;

namespace IotGateway.WebAPI.Filters;

public class GlobalExceptionFilter : IExceptionFilter
{
    private readonly ILogger<GlobalExceptionFilter> _logger;

    public GlobalExceptionFilter(ILogger<GlobalExceptionFilter> logger)
    {
        _logger = logger;
    }

    public void OnException(ExceptionContext context)
    {
        switch (context.Exception)
        {
            case ValidationException ex:
                context.Result = new ObjectResult(
                    ApiResponse.Fail(ex.Message, 422)) { StatusCode = 422 };
                break;
            case UnauthorizedException ex:
                context.Result = new ObjectResult(
                    ApiResponse.Unauthorized(ex.Message)) { StatusCode = 401 };
                break;
            case ForbiddenException ex:
                context.Result = new ObjectResult(
                    ApiResponse.Fail(ex.Message, 403)) { StatusCode = 403 };
                break;
            case NotFoundException ex:
                context.Result = new ObjectResult(
                    ApiResponse.NotFound(ex.Message)) { StatusCode = 404 };
                break;
            case BusinessException ex:
                context.Result = new ObjectResult(
                    ApiResponse.Fail(ex.Message, ex.StatusCode)) { StatusCode = ex.StatusCode };
                break;
            default:
                _logger.LogError(context.Exception, "未处理异常");
                context.Result = new ObjectResult(
                    ApiResponse.Error("服务器内部错误")) { StatusCode = 500 };
                break;
        }
        context.ExceptionHandled = true;
    }
}
```

#### 6.5.3 Program.cs（关键部分）

```csharp
// ===== Serilog 初始化（最前面）=====
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30)
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();  // 替换内置日志

// ===== EF Core + MySQL =====
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(builder.Configuration.GetConnectionString("DefaultConnection"),
        ServerVersion.AutoDetect(...)));

// ===== Controller + 全局异常过滤器 =====
builder.Services.AddControllers(options => options.Filters.Add<GlobalExceptionFilter>());

// ===== JWT（同第三章）=====
builder.Services.AddAuthentication("Bearer").AddJwtBearer(...);

// ===== AutoFac 替换 DI =====
builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
builder.Host.ConfigureContainer<ContainerBuilder>(b => b.RegisterModule<AutofacModule>());

// ===== 中间件 =====
app.UseSerilogRequestLogging();
app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
app.Run();
```

### 6.6 更新 appsettings.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Port=3306;Database=iot_gateway;User=iot_user;Password=Iot@2024;Charset=utf8mb4;"
  },
  "Jwt": {
    "Key": "IotGateway_SuperSecret_Key_2024_Must_Be_At_Least_32_Chars!",
    "Issuer": "IotGateway",
    "Audience": "IotGateway_Client",
    "ExpireHours": 8
  },
  "Urls": "http://localhost:5000"
}
```

### 6.7 模块六知识小结

| 知识点 | 说明 |
|--------|------|
| AutoFac | 第三方 DI，反射扫描自动注册 |
| `RegisterAssemblyTypes` | 程序集扫描核心 API |
| Serilog | 结构化日志，Console + File 双 Sink |
| GlobalExceptionFilter | 6 种异常类型 → HTTP 状态码 + ApiResponse |
| Program.cs | 6 步启动流程：Serilog → EF → Services → AutoFac → Middleware → Run |

---

## 模块七：重构 AuthController

### 7.1 Before / After

**第三章（200+ 行）**：Dictionary 硬编码 + 明文比对 + 匿名对象

**第四章（~70 行）**：

```csharp
[ApiController, Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
    {
        var result = await _authService.LoginAsync(request);
        return Ok(ApiResponse<LoginResponseDto>.Ok(result, "登录成功"));
    }

    [HttpGet("me")]
    [Authorize]
    public IActionResult GetCurrentUser()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var username = User.FindFirst(ClaimTypes.Name)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        return Ok(ApiResponse<object>.Ok(new { UserId = userId, Username = username, Role = role }));
    }
}
```

### 7.2 为什么 Controller 不需要 try-catch？

```
请求 → Controller.Login()
         → _authService.LoginAsync()
              ├─ 验证失败 → throw ValidationException
              ├─ 用户不存在 → throw UnauthorizedException
              ├─ 密码错误 → throw UnauthorizedException
              └─ 一切正常 → 返回 LoginResponseDto
         → GlobalExceptionFilter 自动捕获异常 → 转换为 ApiResponse JSON
```

### 7.3 模块七知识小结

| 变化 | 第三章 | 第四章 |
|------|--------|--------|
| 数据源 | 硬编码 Dictionary | EF Core MySQL |
| 密码 | 明文 `==` | BCrypt.Verify() |
| 返回格式 | 匿名对象 | `ApiResponse<T>` |
| 异常处理 | try-catch | GlobalExceptionFilter |
| Controller 代码量 | 207 行 | ~70 行 |

---

## 模块八：RBAC 权限模块 —— 精细化权限控制

> **设计思路**：第三章 `users.role = 'Admin'` 无法做"操作员可以看设备但不能配置设备"这种精细控制。RBAC 解决这个问题。

### 8.1 什么是 RBAC？

**RBAC（Role-Based Access Control，基于角色的访问控制）**：

```
用户（张三）→ 拥有角色（操作员）→ 角色包含权限（device:read, alarm:read...）
```

就像公司门禁：员工刷卡 → 系统查他的"角色"对应的"通行权限" → 放行或拒绝。

### 8.2 RBAC 数据模型

```
┌──────────┐     ┌──────────────┐     ┌──────────┐
│  users   │1──* │  user_roles  │*──1 │  roles   │
└──────────┘     └──────────────┘     └────┬─────┘
                                           │*    ┌───────────────┐
                                           └─────│role_permissions│
                                                 └───────┬───────┘
                                                         │*
                                                 ┌───────┴──────┐
                                                 │ permissions  │
                                                 └──────────────┘
```

### 8.3 权限编码规范：resource:action

| 权限编码 | 含义 | Admin | Engineer | Operator |
|----------|------|:-----:|:--------:|:--------:|
| `device:read` | 查看设备 | ✅ | ✅ | ✅ |
| `device:create` | 新增设备 | ✅ | ❌ | ❌ |
| `device:write` | 编辑设备 | ✅ | ✅ | ❌ |
| `device:delete` | 删除设备 | ✅ | ❌ | ❌ |
| `device:config` | 配置设备 | ✅ | ✅ | ❌ |
| `alarm:read` | 查看告警 | ✅ | ✅ | ✅ |
| `alarm:confirm` | 确认告警 | ✅ | ✅ | ✅ |
| `alarm:resolve` | 解决告警 | ✅ | ✅ | ❌ |
| `data:read` | 查看数据 | ✅ | ✅ | ✅ |
| `data:export` | 导出数据 | ✅ | ✅ | ❌ |
| `user:manage` | 管理用户 | ✅ | ❌ | ❌ |

### 8.4 动手：创建 4 个 RBAC 实体

#### 8.4.1 Role 实体

**创建文件**：`IotGateway.Domain/Entities/Role.cs`

```csharp
[Table("roles")]
public class Role
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity), Column("id")]
    public long Id { get; set; }
    [Required, MaxLength(50), Column("name")] public string Name { get; set; } = "";
    [Required, MaxLength(50), Column("code")] public string Code { get; set; } = "";
    [MaxLength(200), Column("description")] public string Description { get; set; } = "";
    [Column("status")] public sbyte Status { get; set; } = 1;
    [Column("sort_order")] public int SortOrder { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

#### 8.4.2 Permission 实体

**创建文件**：`IotGateway.Domain/Entities/Permission.cs`

```csharp
[Table("permissions")]
public class Permission
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity), Column("id")]
    public long Id { get; set; }
    [Required, MaxLength(100), Column("name")] public string Name { get; set; } = "";
    [Required, MaxLength(100), Column("code")] public string Code { get; set; } = "";
    [MaxLength(50), Column("resource")] public string Resource { get; set; } = "";
    [MaxLength(50), Column("action")] public string Action { get; set; } = "";
    [MaxLength(200), Column("description")] public string Description { get; set; } = "";
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

#### 8.4.3 UserRole + RolePermission 关联实体

```csharp
[Table("user_roles")]
public class UserRole
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity), Column("id")]
    public long Id { get; set; }
    [Column("user_id")] public long UserId { get; set; }
    [Column("role_id")] public long RoleId { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

[Table("role_permissions")]
public class RolePermission
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity), Column("id")]
    public long Id { get; set; }
    [Column("role_id")] public long RoleId { get; set; }
    [Column("permission_id")] public long PermissionId { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

### 8.5 更新 AppDbContext

在 `AppDbContext` 中添加：

```csharp
public DbSet<Role> Roles => Set<Role>();
public DbSet<Permission> Permissions => Set<Permission>();
public DbSet<UserRole> UserRoles => Set<UserRole>();
public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

// OnModelCreating 中添加索引：
modelBuilder.Entity<Role>(e => e.HasIndex(x => x.Code).IsUnique());
modelBuilder.Entity<Permission>(e => e.HasIndex(x => x.Code).IsUnique());
modelBuilder.Entity<UserRole>(e => e.HasIndex(x => new { x.UserId, x.RoleId }).IsUnique());
modelBuilder.Entity<RolePermission>(e => e.HasIndex(x => new { x.RoleId, x.PermissionId }).IsUnique());
```

### 8.6 RBAC SQL 建表脚本

**创建文件**：`sql/02-rbac-tables.sql`

```sql
USE `iot_gateway`;

DROP TABLE IF EXISTS `role_permissions`, `user_roles`, `permissions`, `roles`;

CREATE TABLE `roles` (
    `id` BIGINT AUTO_INCREMENT, `name` VARCHAR(50) NOT NULL,
    `code` VARCHAR(50) NOT NULL, `description` VARCHAR(200) DEFAULT '',
    `status` TINYINT DEFAULT 1, `sort_order` INT DEFAULT 0,
    `created_at` DATETIME DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (`id`), UNIQUE KEY `uk_role_code` (`code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='角色表';

CREATE TABLE `permissions` (
    `id` BIGINT AUTO_INCREMENT, `name` VARCHAR(100) NOT NULL,
    `code` VARCHAR(100) NOT NULL, `resource` VARCHAR(50) DEFAULT '',
    `action` VARCHAR(50) DEFAULT '', `description` VARCHAR(200) DEFAULT '',
    `created_at` DATETIME DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (`id`), UNIQUE KEY `uk_permission_code` (`code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='权限表';

CREATE TABLE `user_roles` (
    `id` BIGINT AUTO_INCREMENT, `user_id` BIGINT NOT NULL,
    `role_id` BIGINT NOT NULL, `created_at` DATETIME DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (`id`), UNIQUE KEY `uk_user_role` (`user_id`,`role_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='用户角色关联表';

CREATE TABLE `role_permissions` (
    `id` BIGINT AUTO_INCREMENT, `role_id` BIGINT NOT NULL,
    `permission_id` BIGINT NOT NULL, `created_at` DATETIME DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (`id`), UNIQUE KEY `uk_role_permission` (`role_id`,`permission_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='角色权限关联表';

-- 种子数据：3 角色 + 20 权限 + 分配关系
INSERT INTO `roles` (`name`, `code`, `description`, `sort_order`) VALUES
('系统管理员','admin','拥有所有权限',1),
('工程师','engineer','设备配置、告警处理、维修',2),
('操作员','operator','查看、告警确认',3);

INSERT INTO `permissions` (`name`,`code`,`resource`,`action`) VALUES
('设备查看','device:read','device','read'),
('设备新增','device:create','device','create'),
('设备编辑','device:write','device','write'),
('设备删除','device:delete','device','delete'),
('设备配置','device:config','device','config'),
('告警查看','alarm:read','alarm','read'),
('告警确认','alarm:confirm','alarm','confirm'),
('告警解决','alarm:resolve','alarm','resolve'),
('告警规则','alarm:rule','alarm','rule'),
('数据查看','data:read','data','read'),
('报表查看','data:report','data','report'),
('数据导出','data:export','data','export'),
('维保计划','maintain:plan','maintain','plan'),
('工单创建','repair:create','repair','create'),
('工单处理','repair:handle','repair','handle'),
('工单验证','repair:verify','repair','verify'),
('用户管理','user:manage','user','manage'),
('角色管理','role:manage','role','manage'),
('系统配置','system:config','system','config'),
('AI分析','ai:read','ai','read'),
('AI报告','ai:report','ai','report');

-- Admin = 全部权限
INSERT INTO `role_permissions` (`role_id`,`permission_id`)
SELECT 1, id FROM `permissions`;

-- Engineer = 15 个权限
INSERT INTO `role_permissions` (`role_id`,`permission_id`)
SELECT 2, id FROM `permissions`
WHERE `code` IN ('device:read','device:write','device:config',
    'alarm:read','alarm:confirm','alarm:resolve','alarm:rule',
    'data:read','data:report','data:export',
    'maintain:plan','repair:create','repair:handle','repair:verify',
    'ai:read','ai:report');

-- Operator = 6 个权限
INSERT INTO `role_permissions` (`role_id`,`permission_id`)
SELECT 3, id FROM `permissions`
WHERE `code` IN ('device:read','alarm:read','alarm:confirm',
    'data:read','data:report','repair:create','ai:read');

-- 现有用户分配角色
INSERT INTO `user_roles` (`user_id`,`role_id`) VALUES (1,1),(2,3),(3,2);
```

执行：

```bash
docker exec -i iot_mysql mysql -uiot_user -pIot@2024 iot_gateway < sql/02-rbac-tables.sql
```

### 8.7 模块八知识小结

| 知识点 | 说明 |
|--------|------|
| RBAC | 用户→角色→权限 三层模型 |
| 多对多关系 | 通过中间表实现 |
| resource:action | 权限编码格式 |
| 本章范围 | 建表 + Entity + 种子数据，权限逻辑放第五章 |

---

## 模块九：代码上传 Gitee/GitHub

### 9.1 为什么需要版本控制？

- 电脑坏了 → 代码在云端安全
- 改错了 → `git checkout` 撤销回去
- 团队协作 → 多人同时改代码不冲突

### 9.2 Git 三句话入门

| 概念 | 理解 |
|------|------|
| Git | 记录代码**每一次修改**的工具 |
| 仓库（Repo） | 存放代码 + 修改历史 |
| 提交（Commit） | 一次修改的快照（可回到任意一次提交） |

```
工作流程：
  写代码 → git add（暂存）→ git commit（本地快照）→ git push（上传云端）
```

### 9.3 创建 .gitignore

**创建文件**：`wpf_wms/.gitignore`

```gitignore
# 编译产物
bin/  obj/  *.user  *.suo  *.cache
# 日志
logs/  *.log
# IDE
.vs/  .vscode/  .idea/
# 系统
.DS_Store  Thumbs.db
```

### 9.4 上传到 Gitee（码云）

**为什么优先 Gitee？** 国内访问快、中文界面、无需翻墙。

```bash
# 1. 配置用户信息（首次使用）
git config --global user.name "你的姓名"
git config --global user.email "你的邮箱"

# 2. 初始化仓库
cd /path/to/wpfxm
git init
git add .
git commit -m "第四章完成：WebAPI企业级架构 + RBAC权限模块"

# 3. 去 gitee.com 注册 → 创建仓库 → 复制地址

# 4. 关联远程仓库
git remote add origin https://gitee.com/你的用户名/iot-gateway-wms.git

# 5. 推送
git push -u origin master
```

**后续更新代码三步曲**：

```bash
git add .                    # 暂存
git commit -m "描述改了什么"  # 提交
git push                     # 推送
```

> **GitHub 同理**：注册 github.com → 创建仓库 → `git remote add origin https://github.com/...` → `git push`

### 9.5 常用 Git 命令

| 命令 | 用途 |
|------|------|
| `git status` | 查看哪些文件被改了 |
| `git log --oneline` | 查看提交历史 |
| `git checkout -- 文件名` | 撤销某文件的修改 |
| `git pull` | 拉取远程最新代码 |
| `git clone 地址` | 克隆仓库到本地 |

### 9.6 模块九知识小结

| 知识点 | 说明 |
|--------|------|
| Git | 分布式版本控制 |
| Gitee | 国内 Git 托管平台 |
| .gitignore | 排除编译产物、日志、密钥 |
| commit → push | 本地快照 → 上传云端 |

---

## 模块十：运行验证与本章总结

### 10.1 启动并测试

```bash
# 1. Docker
cd docker && docker compose up -d
docker ps | grep mysql  # 确认 healthy

# 2. RBAC SQL
docker exec -i iot_mysql mysql -uiot_user -pIot@2024 iot_gateway < ../sql/02-rbac-tables.sql

# 3. 启动 WebAPI
cd ../wpf_wms/IotGateway.WebAPI && dotnet run
# 预期：[10:30:00 INF] WebAPI 启动成功，监听地址：http://localhost:5000
```

**测试 API**：

```bash
# 正常登录 → HTTP 200 + Token
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"admin123"}'

# 错误密码 → HTTP 401
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"wrong"}'

# 空用户名 → HTTP 422
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"","password":"test"}'

# Swagger：浏览器打开 http://localhost:5000/swagger

# 查看日志
cat logs/log-$(date +%Y%m%d).txt

# 查看审计日志
docker exec -i iot_mysql mysql -uiot_user -pIot@2024 iot_gateway \
  -e "SELECT * FROM audit_logs ORDER BY id DESC LIMIT 5;"
```

### 10.2 全部文件清单

| # | 文件 | 模块 |
|---|------|------|
| 1 | `IotGateway.Common/Helpers/ApiResponse.cs` | 2 |
| 2 | `IotGateway.Common/Exceptions/AppException.cs` | 2 |
| 3 | `IotGateway.Domain/Entities/User.cs` | 3 |
| 4 | `IotGateway.Domain/Entities/AuditLog.cs` | 4 |
| 5 | `IotGateway.Domain/Entities/Role.cs` | 8 |
| 6 | `IotGateway.Domain/Entities/Permission.cs` | 8 |
| 7 | `IotGateway.Domain/Entities/UserRole.cs` | 8 |
| 8 | `IotGateway.Domain/Entities/RolePermission.cs` | 8 |
| 9 | `IotGateway.Infrastructure/Data/AppDbContext.cs` | 4 |
| 10 | `IotGateway.Infrastructure/Repositories/IRepository.cs` | 4 |
| 11 | `IotGateway.Infrastructure/Repositories/Repository.cs` | 4 |
| 12 | `IotGateway.Infrastructure/Repositories/IUnitOfWork.cs` | 4 |
| 13 | `IotGateway.Infrastructure/Repositories/UnitOfWork.cs` | 4 |
| 14 | `IotGateway.Application/DTOs/AuthDto.cs` | 5 |
| 15 | `IotGateway.Application/Services/IAuthService.cs` | 5 |
| 16 | `IotGateway.Application/Services/AuthService.cs` | 5 |
| 17 | `IotGateway.Application/Mappings/AutoMapperProfile.cs` | 5 |
| 18 | `IotGateway.WebAPI/Extensions/AutofacModule.cs` | 6 |
| 19 | `IotGateway.WebAPI/Filters/GlobalExceptionFilter.cs` | 6 |
| 20 | `IotGateway.WebAPI/Controllers/AuthController.cs` | 7 |
| 21 | `IotGateway.WebAPI/Program.cs` | 6 |
| 22 | `IotGateway.WebAPI/appsettings.json` | 6 |
| 23 | `sql/02-rbac-tables.sql` | 8 |
| 24 | `.gitignore` | 9 |

### 10.3 第三章 vs 第四章对照表

| 维度 | 第三章 | 第四章 |
|------|--------|--------|
| 项目数 | 2 个 | 6 个（5 层 + WPF） |
| 用户数据 | 硬编码 | MySQL 数据库 |
| 密码验证 | 明文 `==` | BCrypt.Verify() |
| 响应格式 | 匿名对象 | `ApiResponse<T>` |
| 异常处理 | try-catch | GlobalExceptionFilter |
| DI | `builder.Services.AddXxx` | AutoFac 反射扫描 |
| 数据访问 | Controller 直接操作 | Repository + UoW |
| 对象映射 | 手写 | AutoMapper |
| 日志 | 无 | Serilog（Console + File） |
| 审计 | 无 | audit_logs 表 |
| 权限 | `role` 字符串 | RBAC 4 张表 |

### 10.4 23 个核心概念速查

| # | 概念 | 一句话 | 模块 |
|---|------|--------|------|
| 1 | 分层架构 | 按职责拆分为独立项目 | 1 |
| 2 | ApiResponse\<T\> | 泛型统一响应 | 2 |
| 3 | 异常驱动设计 | 抛异常 → Filter 自动处理 | 2 |
| 4 | Entity | 表→类映射 | 3 |
| 5 | Data Annotations | `[Table]` `[Key]` `[Column]` | 3 |
| 6 | DbContext | EF Core 数据库会话 | 4 |
| 7 | DbSet\<T\> | 表入口 | 4 |
| 8 | IRepository\<T\> | 通用仓储接口 | 4 |
| 9 | Expression\<Func\> | LINQ→SQL 表达式树 | 4 |
| 10 | UnitOfWork | 事务管理+懒加载 Repository | 4 |
| 11 | DTO | 前后端数据传输安全容器 | 5 |
| 12 | AutoMapper | 同名属性自动映射 | 5 |
| 13 | BCrypt | 慢哈希算法（带盐值） | 5 |
| 14 | Service 层 | 业务逻辑层 | 5 |
| 15 | AutoFac | 反射扫描自动注册 DI | 6 |
| 16 | Serilog | 结构化日志 | 6 |
| 17 | Sink | 日志输出目标 | 6 |
| 18 | GlobalExceptionFilter | 异常→ApiResponse | 6 |
| 19 | 瘦 Controller | 只调 Service+返回响应 | 7 |
| 20 | RBAC | 用户→角色→权限 | 8 |
| 21 | resource:action | 权限编码格式 | 8 |
| 22 | .gitignore | 排除不需要上传的文件 | 9 |
| 23 | Git commit/push | 本地快照→云端 | 9 |

---

> **下一章预告：第五章 - JWT 深度 + RBAC 权限完整实现**
> Refresh Token 刷新机制、Redis Token 黑名单、`[RequirePermission]` 自定义授权特性、JWT Claims 多角色支持

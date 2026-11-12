// ============================================================
// 文件：IotGateway.WebAPI/Extensions/AutofacModule.cs
// 用途：AutoFac 依赖注入注册模块
//       使用反射自动扫描并注册所有 Service 和 Repository
//       替代手动逐行 builder.Services.AddXxx<IXxx, Xxx>()
// 所属层：WebAPI（表示层 - DI 配置）
//
// AutoFac 替换内置 DI 的原因：
//   内置 DI（Microsoft.Extensions.DependencyInjection）：
//     - 只支持构造函数注入（Constructor Injection）
//     - 不支持属性注入、AOP 拦截器
//     - 需要手动逐行注册每个服务
//
//   AutoFac：
//     - 支持构造函数注入 + 属性注入
//     - 支持 AOP 拦截器（如事务拦截、日志拦截）
//     - 支持程序集扫描自动注册（反射）
//     - 支持 Module 模块化注册（按层分模块管理）
//
// WPF 端用 Prism.Unity ↔ WebAPI 端用 AutoFac
// 两个 DI 容器对比教学，展示不同场景的选择
// ============================================================

using System.Reflection;
using Autofac;
using AutoMapper;
using Microsoft.Extensions.Logging;
using IotGateway.Application.Mappings;

namespace IotGateway.WebAPI.Extensions;

/// <summary>
/// AutoFac 注册模块
/// 继承 Autofac.Module → AutoFac 启动时自动调用 Load 方法
/// </summary>
public class AutofacModule : Autofac.Module
{
    /// <summary>
    /// 重写 Load 方法
    /// AutoFac 容器构建时自动调用此方法注册所有服务
    /// </summary>
    protected override void Load(ContainerBuilder builder)
    {
        // ===== 第 1 步：获取需要扫描的程序集 =====
        // 需要扫描 3 个项目：Application + Infrastructure + WebAPI

        var webApiAssembly = Assembly.GetExecutingAssembly();
        // 当前程序集：IotGateway.WebAPI.dll
        // 包含：Controllers、Filters、Extensions

        var applicationAssembly = Assembly.Load("IotGateway.Application");
        // IotGateway.Application.dll
        // 包含：Services、DTOs、Mappings

        var infrastructureAssembly = Assembly.Load("IotGateway.Infrastructure");
        // IotGateway.Infrastructure.dll
        // 包含：Data(AppDbContext)、Repositories

        // ===== 第 2 步：注册 Service 层（Application 项目）=====
        // 规则：找到 IotGateway.Application 中所有以 "Service" 结尾的类
        //       注册为实现了其接口的实例
        builder.RegisterAssemblyTypes(applicationAssembly)
            .Where(t => t.Name.EndsWith("Service"))
            // 筛选条件：类名以 "Service" 结尾
            // 例如：AuthService、DeviceService、AlarmService
            .AsImplementedInterfaces()
            // 注册为：IAuthService → AuthService
            //        IDeviceService → DeviceService
            .InstancePerLifetimeScope();
            // 生命周期：每个 HTTP 请求创建一个新实例（Scoped）
            // 等价于内置 DI 的 AddScoped

        // ===== 第 3 步：注册 Repository 层（Infrastructure 项目）=====
        builder.RegisterAssemblyTypes(infrastructureAssembly)
            .Where(t => t.Name.EndsWith("Repository"))
            .AsImplementedInterfaces()
            .InstancePerLifetimeScope();
        // 注册 IRepository<User> → Repository<User>
        //      IUnitOfWork → UnitOfWork

        // ===== 第 4 步：注册 UnitOfWork =====
        builder.RegisterAssemblyTypes(infrastructureAssembly)
            .Where(t => t.Name.EndsWith("UnitOfWork"))
            .AsImplementedInterfaces()
            .InstancePerLifetimeScope();

        // ===== 第 5 步：注册 Controller（WebAPI 项目）=====
        builder.RegisterAssemblyTypes(webApiAssembly)
            .Where(t => t.Name.EndsWith("Controller"))
            .PropertiesAutowired();
            // PropertiesAutowired：启用属性注入
            // 允许通过 [Autowired] 特性注入依赖（除了构造函数注入外）

        // ===== 第 6 步：注册 Filter（WebAPI 项目）=====
        builder.RegisterAssemblyTypes(webApiAssembly)
            .Where(t => t.Name.EndsWith("Filter"))
            .InstancePerLifetimeScope();

        // ===== 第 7 步：注册 AutoMapper IMapper =====
        builder.Register(ctx =>
        {
            var loggerFactory = ctx.Resolve<ILoggerFactory>();
            var config = new MapperConfiguration(cfg =>
            {
                cfg.AddProfile<AutoMapperProfile>();
            }, loggerFactory);
            // AutoMapper 16.x 的 MapperConfiguration 构造函数需要两个参数：
            //   1. Action<IMapperConfigurationExpression>：映射配置
            //   2. ILoggerFactory：日志工厂（用于内部日志输出）
            return config.CreateMapper();
        }).As<IMapper>().SingleInstance();
        // IMapper 注册为单例（线程安全，全局共享一个映射器实例）
    }
}

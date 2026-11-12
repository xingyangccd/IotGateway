// ============================================================
// 文件：IotGateway.Application/Mappings/AutoMapperProfile.cs
// 用途：AutoMapper 对象映射配置
//       定义 Entity ↔ DTO 之间的转换规则
// 所属层：Application（应用层）
//
// AutoMapper 解决的问题：
//   手写映射（繁琐、易漏字段）：
//     var dto = new UserDto {
//         Id = user.Id,
//         Username = user.Username,
//         RealName = user.RealName,
//         Email = user.Email,
//         Phone = user.Phone,
//         AvatarUrl = user.AvatarUrl,
//         Role = user.Role,
//         LastLoginAt = user.LastLoginAt
//     };
//     // 每次都要写 8 行，字段增加时容易遗漏
//
//   AutoMapper 自动映射（一行搞定）：
//     var dto = _mapper.Map<UserDto>(user);
//     // 同名属性自动映射，约定大于配置
//
// 映射约定：
//   User.Username → UserDto.Username（同名自动映射 ✓）
//   User.PasswordHash → UserDto 中没有 → 自动忽略 ✓
//   User.Role → UserDto.Role（同名自动映射 ✓）
// ============================================================

using AutoMapper;
using IotGateway.Domain.Entities;
using IotGateway.Application.DTOs;

namespace IotGateway.Application.Mappings;

/// <summary>
/// AutoMapper 映射配置类
/// 继承 Profile → AutoMapper 自动发现并注册
/// </summary>
public class AutoMapperProfile : Profile
{
    public AutoMapperProfile()
    {
        // ===== User 相关映射 =====

        // User 实体 → UserDto（Entity → DTO）
        CreateMap<User, UserDto>();
        // 因为是 Database First，默认同名属性自动映射
        // PasswordHash 在 Entity 中存在但 DTO 中没有 → 自动跳过

        // LoginRequestDto → User（用于反向映射，实际不常用）
        CreateMap<LoginRequestDto, User>()
            .ForMember(dest => dest.PasswordHash, opt => opt.Ignore())
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.Status, opt => opt.Ignore());
        // Ignore()：告诉 AutoMapper 忽略这些字段
        // 因为我们不会从 DTO 创建 User 实体

        // ===== 后续章节扩展映射 =====
        // CreateMap<Device, DeviceDto>();
        // CreateMap<Alarm, AlarmDto>();
    }
}

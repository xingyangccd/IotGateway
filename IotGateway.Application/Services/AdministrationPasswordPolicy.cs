using System.Text;
using IotGateway.Common.Exceptions;

namespace IotGateway.Application.Services;

public static class AdministrationPasswordPolicy
{
    public static void Validate(string? password)
    {
        if (string.IsNullOrEmpty(password))
            throw new BusinessException("密码不能为空", 422);

        int bytes;
        try
        {
            bytes = new UTF8Encoding(false, true).GetByteCount(password);
        }
        catch (EncoderFallbackException)
        {
            throw new BusinessException("密码包含无效字符", 422);
        }

        if (password.EnumerateRunes().Count() < 12 || bytes > 72 || password.Contains('\0'))
            throw new BusinessException("密码至少需要12个字符，UTF-8编码不得超过72字节，且不能包含空字符", 422);
    }

    public static string Hash(string? password)
    {
        Validate(password);
        return BCrypt.Net.BCrypt.HashPassword(password!, workFactor: 12);
    }
}

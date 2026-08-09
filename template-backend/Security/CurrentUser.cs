using System.Globalization;
using System.Security.Claims;

namespace TemplateDormApi.Security;

/// <summary>
/// 从已认证用户中解析当前账户 ID。
/// </summary>
public static class CurrentUser
{
    public static int? GetAccountId(ClaimsPrincipal principal)
    {
        var accountIdValue = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!int.TryParse(accountIdValue, NumberStyles.None, CultureInfo.InvariantCulture, out var accountId) ||
            accountId <= 0)
        {
            return null;
        }

        return accountId;
    }
}

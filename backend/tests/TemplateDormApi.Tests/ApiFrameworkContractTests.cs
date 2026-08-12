using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using TemplateDormApi.Controllers;

namespace TemplateDormApi.Tests;

public sealed class ApiFrameworkContractTests
{
    private static readonly Type[] FrameworkControllers =
    {
        typeof(StudentProfileController),
        typeof(RepairController),
        typeof(LateEntryController),
        typeof(HygieneController),
        typeof(StudentReportController),
        typeof(AccessController),
        typeof(VisitorRegistryController),
        typeof(ViolationController)
    };

    private static readonly HashSet<string> ExpectedRoutes = new(StringComparer.OrdinalIgnoreCase)
    {
        "PUT /api/students/{studentId}/profile",
        "GET /api/students/{studentId}/accommodation",
        "GET /api/students/{studentId}/accommodation/history",
        "POST /api/repair-tickets",
        "GET /api/students/{studentId}/repair-tickets",
        "GET /api/repair-tickets/{ticketId}",
        "POST /api/repair-tickets/{ticketId}/cancel",
        "POST /api/repair-tickets/{ticketId}/attachments",
        "GET /api/students/{studentId}/late-entries",
        "PUT /api/late-entries/{recordId}/reason",
        "GET /api/rooms/{roomId}/hygiene",
        "GET /api/students/{studentId}/reports/monthly-fee",
        "GET /api/students/{studentId}/reports/facility-usage",
        "GET /api/students/{studentId}/reports/annual",
        "POST /api/late-entries",
        "POST /api/hygiene-records",
        "PUT /api/hygiene-records/{recordId}",
        "GET /api/hygiene-rankings",
        "GET /api/access-logs",
        "GET /api/access-logs/density",
        "POST /api/visitor-registry",
        "POST /api/visitor-registry/{registryId}/verify",
        "POST /api/visitor-registry/{registryId}/exit",
        "POST /api/violations",
        "GET /api/violations"
    };

    [Fact]
    public void Framework_exposes_all_25_claimed_routes()
    {
        var actualRoutes = FrameworkControllers
            .SelectMany(GetRoutes)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Equal(25, actualRoutes.Count);
        Assert.Empty(ExpectedRoutes.Except(actualRoutes, StringComparer.OrdinalIgnoreCase));
        Assert.Empty(actualRoutes.Except(ExpectedRoutes, StringComparer.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> GetRoutes(Type controllerType)
    {
        var controllerRoute = controllerType.GetCustomAttribute<RouteAttribute>()?.Template ?? string.Empty;

        foreach (var method in controllerType.GetMethods(BindingFlags.Instance | BindingFlags.Public))
        {
            foreach (var attribute in method.GetCustomAttributes().OfType<HttpMethodAttribute>())
            {
                var route = JoinRoute(controllerRoute, attribute.Template);
                foreach (var httpMethod in attribute.HttpMethods)
                {
                    yield return $"{httpMethod} {route}";
                }
            }
        }
    }

    private static string JoinRoute(string controllerRoute, string? actionRoute)
    {
        var combined = string.Join(
            '/',
            new[] { controllerRoute, actionRoute }
                .Where(part => !string.IsNullOrWhiteSpace(part))
                .Select(part => part!.Trim('/')));

        var normalized = Regex.Replace(combined, @"\{([^}:]+):[^}]+\}", "{$1}");
        return $"/{normalized}";
    }
}

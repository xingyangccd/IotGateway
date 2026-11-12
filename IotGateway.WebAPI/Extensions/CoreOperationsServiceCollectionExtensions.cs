using IotGateway.WebAPI.Services;

namespace IotGateway.WebAPI.Extensions;

public static class CoreOperationsServiceCollectionExtensions
{
    public static IServiceCollection AddCoreOperationsAI(this IServiceCollection services)
    {
        services.AddScoped<KnowledgeBaseStore>();
        services.AddHttpClient<OpenAiCompatibleClient>(client => client.Timeout = Timeout.InfiniteTimeSpan)
            .RemoveAllLoggers()
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AllowAutoRedirect = false,
                UseCookies = false
            });
        return services;
    }
}

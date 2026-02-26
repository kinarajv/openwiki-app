using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using OpenWiki.Api.Data;
using OpenWiki.Api.Services;
using OpenWiki.Api.Repositories;
using OpenWiki.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace OpenWiki.Api.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"));
            });

            services.AddScoped<IRepositoryRepo, RepositoryRepo>();
            services.AddScoped<IDocumentationRepo, DocumentationRepo>();
            services.AddScoped<IDiagramRepo, DiagramRepo>();
            
            services.AddHttpClient<GitHubService>();
            services.AddHttpClient<AiClientService>();
            services.AddScoped<GitHubIngestService>();

            return services;
        }
    }
}

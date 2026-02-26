using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using OpenWiki.Api.Data;
using OpenWiki.Api.Services;
using OpenWiki.Api.Services.Interfaces;
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
            
            services.AddHttpClient<IGitHubService, GitHubService>();
            services.AddHttpClient<IAiClientService, AiClientService>();
            services.AddScoped<GitHubIngestService>();
            services.AddScoped<GitHubService>(); // For RepositoryController direct use

            return services;
        }
    }
}

using Microsoft.EntityFrameworkCore;
using OpenWiki.Api.Data;
using OpenWiki.Api.Models;
using OpenWiki.Api.Repositories.Interfaces;

namespace OpenWiki.Api.Repositories;

public class DiagramRepo : IDiagramRepo
{
    private readonly AppDbContext _db;

    public DiagramRepo(AppDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(Diagram diagram)
    {
        _db.Diagrams.Add(diagram);
        await _db.SaveChangesAsync();
    }

    public async Task<List<Diagram>> GetByRepoIdAsync(Guid repoId)
    {
        return await _db.Diagrams
            .Where(d => d.RepositoryId == repoId)
            .ToListAsync();
    }
}

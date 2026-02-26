using Microsoft.EntityFrameworkCore;
using OpenWiki.Api.Data;
using OpenWiki.Api.Models;
using OpenWiki.Api.Repositories.Interfaces;

namespace OpenWiki.Api.Repositories;

public class DocumentationRepo : IDocumentationRepo
{
    private readonly AppDbContext _db;

    public DocumentationRepo(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Documentation> AddAsync(Documentation entity)
    {
        _db.Documentations.Add(entity);
        return entity;
    }

    public async Task AddSectionAsync(DocSection section)
    {
        _db.DocSections.Add(section);
    }

    public async Task AddRelationAsync(DocRelation relation)
    {
        _db.DocRelations.Add(relation);
    }

    public async Task<List<DocSection>> GetSectionsByRepoIdAsync(Guid repoId)
    {
        return await _db.DocSections
            .Where(s => s.Documentation.RepositoryId == repoId)
            .OrderBy(s => s.OrderIndex)
            .ToListAsync();
    }

    public async Task<List<DocRelation>> GetRelationsByRepoIdAsync(Guid repoId)
    {
        return await _db.DocRelations
            .Where(r => r.RepositoryId == repoId)
            .ToListAsync();
    }
}

using Microsoft.EntityFrameworkCore;
using OpenWiki.Api.Data;
using OpenWiki.Api.Models;
using OpenWiki.Api.Repositories.Interfaces;

namespace OpenWiki.Api.Repositories;

public class RepositoryRepo : IRepositoryRepo
{
    private readonly AppDbContext _db;

    public RepositoryRepo(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Repository?> GetByFullNameAsync(string fullName)
    {
        return await _db.Repositories
            .Include(r => r.Documentations)
            .ThenInclude(d => d.Sections)
            .FirstOrDefaultAsync(r => r.FullName == fullName);
    }

    public async Task<Repository> AddAsync(Repository entity)
    {
        _db.Repositories.Add(entity);
        await _db.SaveChangesAsync();
        return entity;
    }

    public async Task SaveChangesAsync()
    {
        await _db.SaveChangesAsync();
    }
}

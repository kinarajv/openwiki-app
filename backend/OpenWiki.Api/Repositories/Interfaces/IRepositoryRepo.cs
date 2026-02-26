using System;
using System.Threading.Tasks;
using OpenWiki.Api.Models;

namespace OpenWiki.Api.Repositories.Interfaces;

public interface IRepositoryRepo
{
    Task<Repository?> GetByFullNameAsync(string fullName);
    Task<Repository> AddAsync(Repository entity);
    Task SaveChangesAsync();
}

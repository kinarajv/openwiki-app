using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OpenWiki.Api.Models;

namespace OpenWiki.Api.Repositories.Interfaces;

public interface IDocumentationRepo
{
    Task<Documentation> AddAsync(Documentation entity);
    Task AddSectionAsync(DocSection section);
    Task AddRelationAsync(DocRelation relation);
    Task<List<DocSection>> GetSectionsByRepoIdAsync(Guid repoId);
    Task<List<DocRelation>> GetRelationsByRepoIdAsync(Guid repoId);
}

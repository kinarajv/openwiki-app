using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OpenWiki.Api.Models;

namespace OpenWiki.Api.Repositories.Interfaces;

public interface IDiagramRepo
{
    Task AddAsync(Diagram diagram);
    Task<List<Diagram>> GetByRepoIdAsync(Guid repoId);
}

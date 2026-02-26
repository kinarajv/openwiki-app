import { Star } from 'lucide-react';
import type {  RepoSearchResult  } from "../../types";

interface PopularReposProps {
  repos: RepoSearchResult[];
  onRepoSelect: (fullName: string) => void;
}

export function PopularRepos({ repos, onRepoSelect }: PopularReposProps) {
  return (
    <div className="max-w-2xl mx-auto">
      <h2 className="text-lg font-semibold text-[#1A1A1A] mb-4">Popular Repositories</h2>
      <div className="grid gap-3">
        {repos.map((repo) => (
          <button 
            key={repo.fullName} 
            onClick={() => onRepoSelect(repo.fullName)} 
            className="w-full text-left p-4 bg-white border border-[#E5E5E5] rounded-lg hover:border-blue-300 hover:shadow-sm transition-all"
            data-testid="repo-card"
          >
            <div className="flex justify-between items-start">
              <div>
                <div className="font-medium text-blue-600">{repo.fullName}</div>
                <div className="text-sm text-[#6B6B6B] mt-1">{repo.description}</div>
              </div>
              <div className="flex items-center gap-4 text-sm text-[#6B6B6B]">
                <span>{repo.language}</span>
                <span className="flex items-center gap-1">
                  <Star className="w-4 h-4" /> {repo.stars.toLocaleString()}
                </span>
              </div>
            </div>
          </button>
        ))}
      </div>
    </div>
  );
}

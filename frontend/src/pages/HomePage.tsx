import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Loader2 } from 'lucide-react';
import { Header } from '../components/layout/Header';
import { SearchBar } from '../components/home/SearchBar';
import { PopularRepos } from '../components/home/PopularRepos';
import { useApi } from '../hooks/useApi';
import type {  RepoSearchResult  } from "../types";

const POPULAR_REPOS: RepoSearchResult[] = [
  { fullName: 'microsoft/vscode', description: 'Visual Studio Code', stars: 168000, language: 'TypeScript' },
  { fullName: 'facebook/react', description: 'React library', stars: 230000, language: 'JavaScript' },
  { fullName: 'microsoft/playwright', description: 'Playwright testing', stars: 68000, language: 'TypeScript' },
  { fullName: 'router-for-me/CLIProxyAPI', description: 'CLI Proxy API', stars: 100, language: 'TypeScript' },
];

export function HomePage() {
  const [searchQuery, setSearchQuery] = useState('');
  const [searchResults, setSearchResults] = useState<RepoSearchResult[]>([]);
  const [isChecking, setIsChecking] = useState(false);
  const { isSearching, searchRepos } = useApi();
  const navigate = useNavigate();

  const handleSearchSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!searchQuery.trim()) return;
    const results = await searchRepos(searchQuery);
    if (results.length > 0) {
      setSearchResults(results);
    } else {
      setSearchResults(POPULAR_REPOS.filter(r => r.fullName.toLowerCase().includes(searchQuery.toLowerCase())));
    }
  };

  const handleRepoSelect = (fullName: string) => {
    setIsChecking(true);
    const [owner, repo] = fullName.split('/');
    navigate(`/wiki/${owner}/${repo}`);
  };

  return (
    <div className="min-h-screen bg-[#FAFAFA] font-sans">
      <Header />

      <main className="max-w-5xl mx-auto px-6 py-16">
        <div className="text-center mb-12">
          <h1 className="text-4xl md:text-5xl font-bold text-[#1A1A1A] mb-4">Which repo would you like to understand?</h1>
          <p className="text-xl text-[#6B6B6B]">Multi-document AI with diagrams, relations, and persistent indexing.</p>
        </div>

        <SearchBar 
          searchQuery={searchQuery}
          isSearching={isSearching}
          onSearchChange={setSearchQuery}
          onSubmit={handleSearchSubmit}
        />

        {isChecking && (
          <div className="text-center mb-8">
            <Loader2 className="w-6 h-6 text-blue-500 animate-spin mx-auto" />
            <p className="text-[#6B6B6B] mt-2">Checking if indexed...</p>
          </div>
        )}

        <PopularRepos 
          repos={searchResults.length > 0 ? searchResults : POPULAR_REPOS}
          onRepoSelect={handleRepoSelect}
        />
      </main>
    </div>
  );
}

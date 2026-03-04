import { useState } from 'react';
import { Search, Loader2, KeyRound, ChevronDown, ChevronUp, ArrowRight } from 'lucide-react';

interface SearchBarProps {
  searchQuery: string;
  isSearching: boolean;
  onSearchChange: (query: string) => void;
  onSubmit: (e: React.FormEvent) => void;
  token: string;
  onTokenChange: (token: string) => void;
}

export function SearchBar({ searchQuery, isSearching, onSearchChange, onSubmit, token, onTokenChange }: SearchBarProps) {
  const [showToken, setShowToken] = useState(false);
  const isUrl = searchQuery.startsWith('http://') || searchQuery.startsWith('https://');

  return (
    <form onSubmit={onSubmit} className="max-w-2xl mx-auto mb-16">
      <div className="relative">
        <Search className="absolute left-4 top-1/2 transform -translate-y-1/2 w-5 h-5 text-[#6B6B6B]" />
        <input 
          type="text" 
          value={searchQuery} 
          onChange={(e) => onSearchChange(e.target.value)} 
          className="w-full pl-12 pr-4 py-4 text-lg border border-[#E5E5E5] rounded-xl bg-white shadow-sm focus:outline-none focus:ring-2 focus:ring-blue-500" 
          placeholder="Search repos or paste URL (e.g. https://gitlab.example.com/org/repo)" 
          data-testid="search-input"
        />
        {isSearching && <Loader2 className="absolute right-4 top-1/2 transform -translate-y-1/2 w-5 h-5 text-blue-500 animate-spin" />}
      </div>

      {isUrl && (
        <div className="mt-3">
          <div className="flex items-center justify-between">
            <button
              type="button"
              onClick={() => setShowToken(!showToken)}
              className="flex items-center gap-2 text-sm text-[#6B6B6B] hover:text-[#1A1A1A] transition-colors"
            >
              <KeyRound className="w-4 h-4" />
              <span>Private repository? Add access token</span>
              {showToken ? <ChevronUp className="w-3 h-3" /> : <ChevronDown className="w-3 h-3" />}
            </button>

            <button
              type="submit"
              className="flex items-center gap-2 bg-[#4B7BEC] text-white px-5 py-2 rounded-lg text-sm font-medium hover:bg-blue-600 transition-colors"
            >
              Generate Wiki
              <ArrowRight className="w-4 h-4" />
            </button>
          </div>

          {showToken && (
            <div className="mt-2 relative">
              <KeyRound className="absolute left-4 top-1/2 transform -translate-y-1/2 w-4 h-4 text-[#6B6B6B]" />
              <input
                type="password"
                value={token}
                onChange={(e) => onTokenChange(e.target.value)}
                onKeyDown={(e) => { if (e.key === 'Enter') { e.preventDefault(); onSubmit(e as unknown as React.FormEvent); } }}
                className="w-full pl-11 pr-4 py-3 text-sm border border-[#E5E5E5] rounded-lg bg-white shadow-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
                placeholder="Personal Access Token (PAT)"
                autoComplete="off"
              />
              <p className="mt-1 text-xs text-[#9B9B9B]">
                Token is sent per-request only and never stored.
              </p>
            </div>
          )}
        </div>
      )}
    </form>
  );
}

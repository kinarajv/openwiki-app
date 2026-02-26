import { Search, Loader2 } from 'lucide-react';

interface SearchBarProps {
  searchQuery: string;
  isSearching: boolean;
  onSearchChange: (query: string) => void;
  onSubmit: (e: React.FormEvent) => void;
}

export function SearchBar({ searchQuery, isSearching, onSearchChange, onSubmit }: SearchBarProps) {
  return (
    <form onSubmit={onSubmit} className="max-w-2xl mx-auto mb-16">
      <div className="relative">
        <Search className="absolute left-4 top-1/2 transform -translate-y-1/2 w-5 h-5 text-[#6B6B6B]" />
        <input 
          type="text" 
          value={searchQuery} 
          onChange={(e) => onSearchChange(e.target.value)} 
          className="w-full pl-12 pr-4 py-4 text-lg border border-[#E5E5E5] rounded-xl bg-white shadow-sm focus:outline-none focus:ring-2 focus:ring-blue-500" 
          placeholder="Search repositories..." 
          data-testid="search-input"
        />
        {isSearching && <Loader2 className="absolute right-4 top-1/2 transform -translate-y-1/2 w-5 h-5 text-blue-500 animate-spin" />}
      </div>
    </form>
  );
}

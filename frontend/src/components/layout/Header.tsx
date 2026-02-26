import { Moon } from 'lucide-react';

export function Header() {
  return (
    <header className="border-b border-dashed border-[#E5E5E5] bg-white">
      <div className="max-w-5xl mx-auto px-6 py-4 flex justify-between items-center">
        <span className="text-2xl font-bold text-[#1A1A1A]">OpenWiki</span>
        <button 
          className="text-[#6B6B6B] hover:text-[#1A1A1A]"
          data-testid="mode-toggle"
        >
          <Moon className="w-5 h-5" />
        </button>
      </div>
    </header>
  );
}

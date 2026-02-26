import { FileCode } from 'lucide-react';
import type {  DocSection, Diagram  } from "../../types";

interface SidebarProps {
  docSections: DocSection[];
  diagrams: Diagram[];
  activeSection: string | null;
  onSectionClick: (slug: string) => void;
}

const slugify = (text: string): string => text.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/(^-|-$)/g, '');

export function Sidebar({ docSections, diagrams, activeSection, onSectionClick }: SidebarProps) {
  return (
    <aside className="hidden lg:block w-[280px] border-r border-dashed border-[#E5E5E5] bg-white h-[calc(100vh-60px)] sticky top-[60px] overflow-y-auto">
      <div className="p-4">
        <div className="text-xs font-semibold text-[#6B6B6B] uppercase tracking-wide mb-3">
          Documents ({docSections.length})
        </div>
        <nav className="space-y-1">
          {docSections.map((section) => (
            <button 
              key={section.id} 
              onClick={() => onSectionClick(section.slug)} 
              className={'w-full text-left py-2 px-3 rounded-md text-sm transition-colors ' + (activeSection === section.slug ? 'bg-blue-100 text-blue-700' : section.level === 1 ? 'font-medium text-[#1A1A1A] hover:bg-gray-100' : 'text-[#6B6B6B] hover:bg-gray-100 pl-6')}
            >
              {section.title}
            </button>
          ))}
        </nav>
        {diagrams.length > 0 && (
          <>
            <div className="text-xs font-semibold text-[#6B6B6B] uppercase tracking-wide mt-6 mb-3">
              Diagrams
            </div>
            <nav className="space-y-1">
              {diagrams.map((d, i) => (
                <button 
                  key={i} 
                  onClick={() => onSectionClick('diagram-' + slugify(d.title))} 
                  className="w-full text-left py-2 px-3 rounded-md text-sm text-[#6B6B6B] hover:bg-gray-100 flex items-center gap-2"
                >
                  <FileCode className="w-4 h-4" /> {d.title}
                </button>
              ))}
            </nav>
          </>
        )}
      </div>
    </aside>
  );
}

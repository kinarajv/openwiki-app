import { CheckCircle2, Loader2, Download, FileSearch, GitBranch, FileText } from 'lucide-react';

const INGEST_STEPS = [
  { label: "Cloning repository from GitHub...", icon: Download },
  { label: "Analyzing source code structure...", icon: FileSearch },
  { label: "Generating multi-document architecture...", icon: GitBranch },
  { label: "Creating document relations and diagrams...", icon: FileText }
];

interface IngestProgressProps {
  loadingStep: number;
}

export function IngestProgress({ loadingStep }: IngestProgressProps) {
  return (
    <div className="min-h-screen bg-[#FAFAFA] flex items-center justify-center font-sans">
      <div className="max-w-lg w-full px-6">
        <div className="text-center mb-8">
          <h1 className="text-2xl font-bold text-[#1A1A1A] mb-2">OpenWiki</h1>
          <p className="text-[#6B6B6B]">Indexing...</p>
        </div>
        <div className="w-full flex flex-col gap-4">
          {INGEST_STEPS.map((step, idx) => {
            const Icon = step.icon;
            const isActive = idx === loadingStep;
            const isComplete = idx < loadingStep;
            return (
              <div 
                key={idx} 
                className={'flex items-center gap-4 p-4 rounded-lg transition-all ' + (idx > loadingStep ? 'opacity-40 bg-gray-50' : isActive ? 'bg-blue-50 border-2 border-blue-200' : 'bg-white border border-[#E5E5E5]')}
              >
                <div className={'w-12 h-12 rounded-full flex items-center justify-center ' + (isComplete ? 'bg-green-100' : isActive ? 'bg-blue-100' : 'bg-gray-100')}>
                  {isComplete ? 
                    <CheckCircle2 className="w-6 h-6 text-green-600" /> : 
                    isActive ? 
                      <Loader2 className="w-6 h-6 text-blue-600 animate-spin" /> : 
                      <Icon className="w-6 h-6 text-gray-400" />
                  }
                </div>
                <span className={'text-base font-medium ' + (isActive ? 'text-blue-700' : isComplete ? 'text-green-700' : 'text-[#6B6B6B]')}>
                  {step.label}
                </span>
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
}

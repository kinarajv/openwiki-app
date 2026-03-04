import { useEffect, useRef } from 'react'
import { CheckCircle2, Loader2, Download, FileSearch, GitBranch, FileText, Cpu, TerminalSquare } from 'lucide-react'

const INGEST_STEPS = [
  { label: "Cloning repository...", icon: Download },
  { label: "Analyzing source code structure...", icon: FileSearch },
  { label: "Generating multi-document architecture...", icon: GitBranch },
  { label: "Parsing structure response...", icon: Cpu },
  { label: "Generating section content...", icon: FileText },
  { label: "Complete", icon: CheckCircle2 }
]

interface IngestProgressProps {
  loadingStep: number
  logs: string[]
}

export function IngestProgress({ loadingStep, logs }: IngestProgressProps) {
  const logEndRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    logEndRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [logs])

  const progressPercent = Math.min(Math.round((loadingStep / (INGEST_STEPS.length - 1)) * 100), 100)

  return (
    <div className="min-h-screen bg-[#FAFAFA] flex items-center justify-center font-sans">
      <div className="max-w-2xl w-full px-6">
        <div className="text-center mb-8">
          <h1 className="text-2xl font-bold text-[#1A1A1A] mb-2">OpenWiki</h1>
          <p className="text-[#6B6B6B]">Indexing repository...</p>
        </div>

        <div className="mb-6">
          <div className="flex justify-between items-center mb-2">
            <span className="text-sm font-medium text-[#6B6B6B]">
              Step {Math.min(loadingStep + 1, INGEST_STEPS.length)} of {INGEST_STEPS.length}
            </span>
            <span className="text-sm font-medium text-blue-600">{progressPercent}%</span>
          </div>
          <div className="w-full h-2.5 bg-gray-200 rounded-full overflow-hidden">
            <div
              className="h-full bg-blue-500 rounded-full transition-all duration-500 ease-out"
              style={{ width: `${progressPercent}%` }}
            />
          </div>
        </div>

        <div className="w-full flex flex-col gap-3 mb-6">
          {INGEST_STEPS.map((step, idx) => {
            const Icon = step.icon
            const isActive = idx === loadingStep
            const isComplete = idx < loadingStep
            return (
              <div
                key={idx}
                className={'flex items-center gap-4 p-3 rounded-lg transition-all ' + (idx > loadingStep ? 'opacity-40 bg-gray-50' : isActive ? 'bg-blue-50 border-2 border-blue-200' : 'bg-white border border-[#E5E5E5]')}
              >
                <div className={'w-10 h-10 rounded-full flex items-center justify-center shrink-0 ' + (isComplete ? 'bg-green-100' : isActive ? 'bg-blue-100' : 'bg-gray-100')}>
                  {isComplete ?
                    <CheckCircle2 className="w-5 h-5 text-green-600" /> :
                    isActive ?
                      <Loader2 className="w-5 h-5 text-blue-600 animate-spin" /> :
                      <Icon className="w-5 h-5 text-gray-400" />
                  }
                </div>
                <span className={'text-sm font-medium ' + (isActive ? 'text-blue-700' : isComplete ? 'text-green-700' : 'text-[#6B6B6B]')}>
                  {step.label}
                </span>
              </div>
            )
          })}
        </div>

        {logs.length > 0 && (
          <div className="bg-[#1a1a2e] rounded-lg border border-[#313244] overflow-hidden">
            <div className="flex items-center gap-2 px-4 py-2 border-b border-[#313244] bg-[#1e1e2e]">
              <TerminalSquare className="w-4 h-4 text-neutral-400" />
              <span className="text-xs font-medium text-neutral-400 uppercase tracking-wider">Activity Log</span>
            </div>
            <div className="max-h-48 overflow-y-auto p-4 font-mono text-xs space-y-1">
              {logs.map((log, idx) => (
                <div key={idx} className="text-neutral-400 leading-relaxed">
                  <span className="text-neutral-600 mr-2 select-none">{String(idx + 1).padStart(2, '0')}</span>
                  {log}
                </div>
              ))}
              <div ref={logEndRef} />
            </div>
          </div>
        )}
      </div>
    </div>
  )
}

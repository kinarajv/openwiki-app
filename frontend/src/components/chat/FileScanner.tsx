import { useEffect, useState } from 'react'
import { FileText, Loader2 } from 'lucide-react'

interface FileScannerProps {
  files: string[]
  isScanning: boolean
}

export function FileScanner({ files, isScanning }: FileScannerProps) {
  const [visibleCount, setVisibleCount] = useState(0)

  useEffect(() => {
    if (!isScanning && files.length > 0) {
      setVisibleCount(files.length)
      return
    }

    if (isScanning && visibleCount < files.length) {
      const timer = setTimeout(() => {
        setVisibleCount((prev) => Math.min(prev + 1, files.length))
      }, 300)
      return () => clearTimeout(timer)
    }
  }, [isScanning, visibleCount, files.length])

  useEffect(() => {
    if (files.length === 0) setVisibleCount(0)
  }, [files.length])

  const visibleFiles = files.slice(0, visibleCount)

  return (
    <div className="flex flex-col gap-1 p-4">
      <div className="flex items-center gap-2 mb-3">
        {isScanning && (
          <Loader2 className="w-4 h-4 text-indigo-400 animate-spin" />
        )}
        <span className="text-xs font-medium uppercase tracking-wider text-neutral-500">
          {isScanning ? 'Scanning files...' : `${files.length} files scanned`}
        </span>
      </div>

      <div className="flex flex-col gap-0.5">
        {visibleFiles.map((file, idx) => (
          <div
            key={file + idx}
            className="flex items-center gap-2 px-3 py-1.5 rounded-md text-sm text-neutral-400 animate-[fadeSlideIn_300ms_ease-out_forwards]"
            style={{ animationDelay: `${idx * 50}ms` }}
          >
            <FileText className="w-3.5 h-3.5 text-neutral-500 shrink-0" />
            <span className="truncate font-mono text-xs">{file}</span>
          </div>
        ))}
      </div>

      <style>{`
        @keyframes fadeSlideIn {
          from {
            opacity: 0;
            transform: translateY(6px);
          }
          to {
            opacity: 1;
            transform: translateY(0);
          }
        }
      `}</style>
    </div>
  )
}

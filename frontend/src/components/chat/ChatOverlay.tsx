import { useCallback, useEffect, useRef, useState } from 'react'
import { ArrowLeft, ArrowRight, Brain, Check, ChevronDown, Copy, Zap } from 'lucide-react'
import ReactMarkdown from 'react-markdown'
import remarkGfm from 'remark-gfm'
import rehypeHighlight from 'rehype-highlight'
import 'highlight.js/styles/github.css'
import type { ChatMessage } from '../../types'
import { FileScanner } from './FileScanner'

interface ChatOverlayProps {
  isOpen: boolean
  onClose: () => void
  messages: ChatMessage[]
  isTyping: boolean
  activeRepo: string | null
  repoFiles: string[]
  onSendQuestion: (mode: string, question: string) => Promise<void>
}

function CopyButton({ code }: { code: string }) {
  const [copied, setCopied] = useState(false)

  const handleCopy = async () => {
    await navigator.clipboard.writeText(code)
    setCopied(true)
    setTimeout(() => setCopied(false), 2000)
  }

  return (
    <button
      onClick={handleCopy}
      className="absolute top-2 right-2 p-1.5 rounded-md bg-white/10 hover:bg-white/20 text-gray-400 hover:text-white transition-all opacity-0 group-hover:opacity-100"
    >
      {copied ? <Check className="w-4 h-4 text-green-400" /> : <Copy className="w-4 h-4" />}
    </button>
  )
}

function extractText(node: unknown): string {
  if (typeof node === 'string') return node
  if (Array.isArray(node)) return node.map(extractText).join('')
  if (node && typeof node === 'object' && 'props' in node) {
    const n = node as { props?: { children?: unknown } }
    if (n.props?.children) return extractText(n.props.children)
  }
  return ''
}

function OverlayMessage({ message }: { message: ChatMessage }) {
  const [displayContent, setDisplayContent] = useState(message.content)

  useEffect(() => {
    if (!message.isStreaming) {
      setDisplayContent(message.content)
      return
    }
    const timer = setTimeout(() => {
      setDisplayContent(message.content)
    }, 80)
    return () => clearTimeout(timer)
  }, [message.content, message.isStreaming])

  return (
    <div className="prose prose-sm prose-invert max-w-none text-neutral-300 [overflow-wrap:anywhere]">
      <ReactMarkdown
        remarkPlugins={[remarkGfm]}
        rehypePlugins={[[rehypeHighlight, { ignoreMissing: true, subset: false }]]}
        components={{
          pre: ({ children, ...props }: React.ComponentProps<'pre'>) => {
            const codeChild = Array.isArray(children) ? children[0] : children
            const codeText = extractText((codeChild as { props?: { children?: unknown } })?.props?.children || '')

            return (
              <pre
                className="group relative rounded-md bg-[#1e1e2e] text-[#cdd6f4] text-sm leading-relaxed overflow-x-auto p-4 border border-[#313244] my-3 font-mono"
                {...props}
              >
                {children}
                <CopyButton code={codeText} />
              </pre>
            )
          },
          code: ({ className, children, node, ...props }: React.ComponentProps<'code'> & { node?: { position?: unknown; parent?: { tagName?: string } } }) => {
            const isBlock = node?.position && node?.parent?.tagName === 'pre'
              || (typeof className === 'string' && /language-/.test(className))
              || String(children || '').includes('\n')

            if (isBlock) {
              return (
                <code className={`${className || ''} block whitespace-pre`} {...props}>
                  {children}
                </code>
              )
            }

            return (
              <code className="text-[#cba6f7] bg-[#1e1e2e] px-1.5 py-0.5 rounded text-sm font-mono" {...props}>
                {children}
              </code>
            )
          },
          a: ({ href, children, ...props }: React.ComponentProps<'a'>) => (
            <a href={href} target="_blank" rel="noopener noreferrer" className="text-blue-400 hover:text-blue-300 hover:underline" {...props}>
              {children}
            </a>
          )
        }}
      >
        {displayContent}
      </ReactMarkdown>
      {message.isStreaming && (
        <span
          className="inline-block w-2 h-5 bg-indigo-400 ml-0.5 animate-[blink_1s_step-end_infinite]"
        />
      )}
    </div>
  )
}

export function ChatOverlay({ isOpen, onClose, messages, isTyping, activeRepo, repoFiles, onSendQuestion }: ChatOverlayProps) {
  const [question, setQuestion] = useState('')
  const [mode, setMode] = useState<'Fast' | 'Deep'>('Fast')
  const chatEndRef = useRef<HTMLDivElement>(null)
  const scrollContainerRef = useRef<HTMLDivElement>(null)
  const userScrolledUp = useRef(false)

  const handleScroll = useCallback(() => {
    const el = scrollContainerRef.current
    if (!el) return
    const distFromBottom = el.scrollHeight - el.scrollTop - el.clientHeight
    userScrolledUp.current = distFromBottom > 100
  }, [])

  useEffect(() => {
    if (!userScrolledUp.current) {
      chatEndRef.current?.scrollIntoView({ behavior: 'smooth' })
    }
  }, [messages])

  useEffect(() => {
    if (isOpen) {
      document.body.style.overflow = 'hidden'
    } else {
      document.body.style.overflow = ''
    }
    return () => { document.body.style.overflow = '' }
  }, [isOpen])

  const handleSend = () => {
    if (question.trim()) {
      onSendQuestion(mode, question)
      setQuestion('')
    }
  }

  if (!isOpen) return null

  const hasAssistantStreaming = messages.some((m) => m.role === 'assistant' && m.isStreaming)

  return (
    <div className="fixed inset-0 z-50 flex flex-col bg-[#0a0a1a] animate-[overlayFadeIn_200ms_ease-out_forwards]">
      <header className="flex items-center gap-3 px-4 py-3 border-b border-white/10 bg-[#0a0a1a]/90 backdrop-blur-sm shrink-0">
        <button
          onClick={onClose}
          className="flex items-center gap-2 text-neutral-400 hover:text-white transition-colors px-2 py-1 rounded-md hover:bg-white/5"
        >
          <ArrowLeft className="w-5 h-5" />
          <span className="text-sm font-medium">{activeRepo || 'Back'}</span>
        </button>
      </header>

      <div className="flex flex-1 min-h-0 overflow-hidden">
        <div
          ref={scrollContainerRef}
          onScroll={handleScroll}
          className="flex-1 md:w-[62%] md:flex-none overflow-y-auto px-6 md:px-10 py-6 pb-32 custom-scrollbar"
        >
          <div className="max-w-3xl mx-auto flex flex-col gap-6">
            {messages.map((msg) => (
              <div key={msg.id}>
                {msg.role === 'user' ? (
                  <div className="text-lg text-white font-semibold [overflow-wrap:break-word] mb-2">
                    {msg.content}
                  </div>
                ) : (
                  <OverlayMessage message={msg} />
                )}
              </div>
            ))}
            <div ref={chatEndRef} />
          </div>
        </div>

        <div className="hidden md:flex md:w-[38%] border-l border-white/10 overflow-y-auto bg-[#0d0d1a] custom-scrollbar">
          <div className="w-full">
            <FileScanner files={repoFiles} isScanning={hasAssistantStreaming || isTyping} />
          </div>
        </div>
      </div>

      <div className="pointer-events-none fixed bottom-0 left-0 right-0 pb-3 px-3 z-[60]">
        <div className="pointer-events-auto mx-auto max-w-3xl">
          <form
            onSubmit={(e) => { e.preventDefault(); handleSend() }}
            className="rounded-xl backdrop-blur-md transition-shadow focus-within:ring-[.5px] focus-within:ring-indigo-300/40 bg-[#1a1a2e]/80 border border-indigo-300/20 shadow-lg shadow-indigo-500/20 [backdrop-filter:blur(12px)]"
          >
            <textarea
              value={question}
              onChange={(e) => setQuestion(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === 'Enter' && !e.shiftKey) {
                  e.preventDefault()
                  handleSend()
                }
              }}
              placeholder="Ask a follow-up question"
              className="w-full resize-none bg-transparent text-white text-base p-3 focus:outline-none placeholder:text-gray-400/60"
              rows={1}
            />
            <div className="flex justify-between items-center px-3 pb-2">
              <button
                type="button"
                onClick={() => setMode(mode === 'Fast' ? 'Deep' : 'Fast')}
                className="flex items-center gap-2 text-sm text-gray-300 hover:text-white hover:bg-white/10 px-3 py-1.5 rounded-md font-medium transition-colors"
              >
                {mode === 'Fast' ? <Zap className="w-4 h-4 text-blue-400" /> : <Brain className="w-4 h-4 text-purple-400" />}
                {mode}
                <ChevronDown className="w-4 h-4" />
              </button>
              <button
                type="submit"
                disabled={isTyping || !question.trim()}
                className={`rounded-full p-2 transition-colors ${question.trim() ? 'bg-indigo-500 text-white hover:bg-indigo-400' : 'bg-white/10 text-gray-500'}`}
              >
                <ArrowRight className="w-5 h-5" />
              </button>
            </div>
          </form>
        </div>
      </div>

      <style>{`
        @keyframes overlayFadeIn {
          from { opacity: 0; }
          to { opacity: 1; }
        }
        @keyframes blink {
          0%, 100% { opacity: 1; }
          50% { opacity: 0; }
        }
        .custom-scrollbar::-webkit-scrollbar {
          width: 6px;
        }
        .custom-scrollbar::-webkit-scrollbar-track {
          background: transparent;
        }
        .custom-scrollbar::-webkit-scrollbar-thumb {
          background: rgba(255, 255, 255, 0.1);
          border-radius: 3px;
        }
        .custom-scrollbar::-webkit-scrollbar-thumb:hover {
          background: rgba(255, 255, 255, 0.2);
        }
      `}</style>
    </div>
  )
}

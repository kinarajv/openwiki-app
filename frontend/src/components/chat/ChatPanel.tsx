import { useState } from 'react'
import { ChevronDown, Zap, Brain, ArrowRight } from 'lucide-react'

interface ChatPanelProps {
  messages: ChatMessage[]
  isTyping: boolean
  activeRepo: string | null
  onSendQuestion: (mode: string, question: string) => Promise<void>
}

import type { ChatMessage } from '../../types'

export function ChatPanel({ isTyping, activeRepo, onSendQuestion }: ChatPanelProps) {
  const [question, setQuestion] = useState('')
  const [mode, setMode] = useState<'Fast' | 'Deep'>('Fast')

  const handleSend = () => {
    if (question.trim()) {
      onSendQuestion(mode, question)
      setQuestion('')
    }
  }

  return (
    <>
      <form
        onSubmit={(e) => { e.preventDefault(); handleSend() }}
        className="fixed bottom-2 left-2 right-2 md:left-1/2 md:-translate-x-1/2 md:max-w-3xl z-50 rounded-xl backdrop-blur-md transition-shadow focus-within:ring-[.5px] focus-within:ring-indigo-300/40 bg-[#1a1a2e]/80 border border-indigo-300/20 shadow-lg shadow-indigo-500/20 [backdrop-filter:blur(12px)]"
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
          placeholder={`Ask about ${activeRepo || 'this repository'}...`}
          className="w-full resize-none bg-transparent text-white text-base p-3 focus:outline-none placeholder:text-gray-400/60"
          rows={1}
          data-testid="chat-input"
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
            data-testid="chat-send"
          >
            <ArrowRight className="w-5 h-5" />
          </button>
        </div>
      </form>
    </>
  )
}

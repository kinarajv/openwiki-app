import { useEffect, useRef, useState } from 'react';
import { ChevronDown, Zap, Brain, ArrowRight } from 'lucide-react';
import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';

interface ChatPanelProps {
  chatLog: string;
  isTyping: boolean;
  activeRepo: string | null;
  onSendQuestion: (mode: string, question: string) => Promise<void>;
}

export function ChatPanel({ chatLog, isTyping, activeRepo, onSendQuestion }: ChatPanelProps) {
  const [question, setQuestion] = useState('');
  const [mode, setMode] = useState<'Fast' | 'Deep'>('Fast');
  const chatEndRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    chatEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [chatLog, isTyping]);

  const handleSend = () => {
    if (question.trim()) {
      onSendQuestion(mode, question);
      setQuestion('');
    }
  };

  return (
    <>
      {chatLog && (
        <div 
          className="bg-[#F0F0F5] p-6 rounded-xl border border-[#E5E5E5] mb-8"
          data-testid="chat-log"
        >
          <div className="prose prose-sm max-w-none text-[#333333]">
            <ReactMarkdown remarkPlugins={[remarkGfm]}>{chatLog}</ReactMarkdown>
          </div>
          {isTyping && <span className="animate-pulse inline-block w-2 h-4 bg-[#6B6B6B] ml-1 mt-2"></span>}
          <div ref={chatEndRef} />
        </div>
      )}

      <div className="fixed bottom-6 left-1/2 transform -translate-x-1/2 w-[90%] md:w-[600px] bg-white rounded-xl shadow-xl flex flex-col p-4 border border-[#E5E5E5] z-50">
        <input 
          type="text" 
          value={question} 
          onChange={e => setQuestion(e.target.value)} 
          onKeyDown={e => e.key === 'Enter' && handleSend()} 
          placeholder={'Ask about ' + (activeRepo || 'this repository') + '...'} 
          className="border-none bg-transparent text-base p-2 text-[#1A1A1A] w-full focus:outline-none placeholder:text-[#6B6B6B]" 
          data-testid="chat-input"
        />
        <div className="flex justify-between items-center mt-2 pt-2 border-t border-[#E5E5E5]">
          <button 
            onClick={() => setMode(mode === 'Fast' ? 'Deep' : 'Fast')} 
            className="flex items-center gap-2 text-sm text-[#4A4A4A] hover:bg-gray-100 px-3 py-1.5 rounded-md font-medium"
          >
            {mode === 'Fast' ? <Zap className="w-4 h-4 text-blue-500" /> : <Brain className="w-4 h-4 text-purple-500" />}
            {mode}
            <ChevronDown className="w-4 h-4" />
          </button>
          <button 
            onClick={handleSend} 
            disabled={isTyping || !question.trim()} 
            className={'rounded-full p-2 transition-colors ' + (question.trim() ? 'bg-[#1A1A1A] text-white hover:bg-black' : 'bg-[#E5E5E5] text-gray-400')}
            data-testid="chat-send"
          >
            <ArrowRight className="w-5 h-5" />
          </button>
        </div>
      </div>
    </>
  );
}

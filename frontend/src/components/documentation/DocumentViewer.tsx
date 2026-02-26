import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';

interface DocumentViewerProps {
  content: string;
}

export function DocumentViewer({ content }: DocumentViewerProps) {
  return (
    <article 
      className="prose prose-slate max-w-none prose-headings:scroll-mt-20 prose-h1:text-3xl prose-h1:font-bold prose-h1:text-[#1A1A1A] prose-h1:border-b prose-h1:border-[#E5E5E5] prose-h1:pb-4 prose-h1:mb-6 prose-h2:text-2xl prose-h2:font-semibold prose-h2:text-[#1A1A1A] prose-h2:mt-10 prose-h2:mb-4 prose-p:text-[#333333] prose-p:leading-relaxed prose-a:text-blue-600 prose-code:text-[#d63384] prose-code:bg-[#f8f9fa] prose-code:px-1.5 prose-code:py-0.5 prose-code:rounded prose-code:before:content-none prose-code:after:content-none prose-pre:bg-[#1e1e1e] prose-pre:text-[#d4d4d4] prose-pre:rounded-lg prose-pre:p-4"
      data-testid="doc-viewer"
    >
      <ReactMarkdown remarkPlugins={[remarkGfm]}>{content}</ReactMarkdown>
    </article>
  );
}

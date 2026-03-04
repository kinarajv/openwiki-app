import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';
import rehypeHighlight from 'rehype-highlight';
import mermaid from 'mermaid';
import { useMemo } from 'react';
import 'highlight.js/styles/github.css';

interface DocumentViewerProps {
  content: string;
  onSectionNavigate?: (slug: string) => void;
}

const toSlug = (v: string) => v.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/(^-|-$)/g, '');

export function DocumentViewer({ content, onSectionNavigate }: DocumentViewerProps) {
  const normalizedContent = useMemo(() => {
    return content.replace(/\\n/g, '\n');
  }, [content]);

  return (
    <article
      className="prose prose-slate max-w-none prose-headings:scroll-mt-20 prose-h1:text-3xl prose-h1:font-bold prose-h1:text-[#1A1A1A] prose-h1:border-b prose-h1:border-[#E5E5E5] prose-h1:pb-4 prose-h1:mb-6 prose-h2:text-2xl prose-h2:font-semibold prose-h2:text-[#1A1A1A] prose-h2:mt-10 prose-h2:mb-4 prose-p:text-[#333333] prose-p:leading-relaxed prose-a:text-blue-600 prose-code:before:content-none prose-code:after:content-none"
      data-testid="doc-viewer"
    >
      <ReactMarkdown
        remarkPlugins={[remarkGfm]}
        rehypePlugins={[[rehypeHighlight, { ignoreMissing: true, subset: false }]]}
        components={{
          a: ({ href, children, ...props }) => {
            const link = href || '';
            const isExternal = /^https?:\/\//i.test(link);
            const isGitHubSource = isExternal && link.includes('github.com') && (link.includes('/blob/') || link.includes('/tree/') || link.includes('#L'));
            const isInternalWiki = link.startsWith('/wiki/') || /^\/[^/]+\/[^/]+\//.test(link);

            if (isGitHubSource) {
              return (
                <a href={link} target="_blank" rel="noopener noreferrer" className="inline-flex items-center gap-1 px-2 py-0.5 text-xs rounded-md border border-[#E5E5E5] bg-[#F8F8F8] hover:bg-[#EFEFEF] no-underline" {...props}>
                  <span>{children}</span>
                </a>
              );
            }

            if (isInternalWiki) {
              return (
                <a
                  href={link}
                  onClick={(e) => {
                    e.preventDefault();
                    const parts = link.split('/').filter(Boolean);
                    const last = parts[parts.length - 1] || '';
                    const slug = toSlug(last);
                    if (onSectionNavigate && slug) onSectionNavigate(slug);
                  }}
                  className="text-blue-600 hover:underline"
                  {...props}
                >
                  {children}
                </a>
              );
            }

            return (
              <a href={link} target={isExternal ? '_blank' : undefined} rel={isExternal ? 'noopener noreferrer' : undefined} className="text-blue-600 hover:underline" {...props}>
                {children}
              </a>
            );
          },
          pre: ({ children, ...props }: any) => {
            const codeChild = Array.isArray(children) ? children[0] : children;
            const codeClassName = codeChild?.props?.className || '';
            if (/language-mermaid/.test(codeClassName)) {
              const extractText = (node: any): string => {
                if (typeof node === 'string') return node;
                if (Array.isArray(node)) return node.map(extractText).join('');
                if (node?.props?.children) return extractText(node.props.children);
                return '';
              };
              const mermaidRaw = extractText(codeChild?.props?.children || '').replace(/\n$/, '');
              const id = `mermaid-${Math.random().toString(36).slice(2, 10)}`;
              setTimeout(async () => {
                try {
                  mermaid.initialize({ startOnLoad: false, securityLevel: 'loose', theme: 'neutral' });
                  const el = document.getElementById(id);
                  if (!el) return;
                  const { svg } = await mermaid.render(`${id}-svg`, mermaidRaw);
                  el.innerHTML = svg;
                } catch {
                  const el = document.getElementById(id);
                  if (el) {
                    el.innerHTML = `<pre class="text-sm text-[#6B6B6B] whitespace-pre-wrap font-mono">${mermaidRaw.replace(/</g, '&lt;')}</pre>`;
                  }
                }
              }, 0);

              return <div id={id} className="overflow-x-auto border border-[#E5E5E5] rounded-lg p-4 bg-white my-4" />;
            }

            return (
              <pre
                className="rounded-md bg-[#f6f8fa] text-[#24292e] text-sm leading-relaxed overflow-x-auto p-4 border border-[#e1e4e8] my-4 font-mono"
                {...props}
              >
                {children}
              </pre>
            );
          },
          code: ({ className, children, node, ...props }: any) => {
            const langMatch = className?.match(/language-(\w+)/);
            const language = langMatch ? langMatch[1] : '';
            const raw = String(children || '').replace(/\n$/, '');
            const isBlock = node?.position && node?.parent?.tagName === 'pre'
              || (typeof className === 'string' && /language-/.test(className))
              || raw.includes('\n');

            if (language === 'mermaid') {
              return <code className={className} {...props}>{children}</code>;
            }

            if (isBlock) {
              return (
                <code className={`${className || ''} block whitespace-pre`} {...props}>
                  {children}
                </code>
              );
            }

            return (
              <code className="text-[#d63384] bg-[#f8f9fa] px-1.5 py-0.5 rounded text-sm font-mono" {...props}>
                {children}
              </code>
            );
          }
        }}
      >
        {normalizedContent}
      </ReactMarkdown>
    </article>
  );
}

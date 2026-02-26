import { useEffect, useRef } from 'react';
import mermaid from 'mermaid';
import type { Diagram } from '../../types';

interface DiagramRendererProps {
  diagrams: Diagram[];
}

const slugify = (text: string): string => text.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/(^-|-$)/g, '');

export function DiagramRenderer({ diagrams }: DiagramRendererProps) {
  const diagramRefs = useRef<Map<string, HTMLDivElement>>(new Map());

  useEffect(() => {
    diagrams.forEach(async (diag) => {
      const ref = diagramRefs.current.get(diag.title);
      if (ref && diag.type === 'mermaid') {
        try {
          const { svg } = await mermaid.render('mermaid-' + slugify(diag.title), diag.content);
          ref.innerHTML = svg;
        } catch (e) {
          console.error('Mermaid render error:', e);
        }
      }
    });
  }, [diagrams]);

  if (diagrams.length === 0) return null;

  return (
    <div className="mt-12">
      <h2 className="text-2xl font-bold text-[#1A1A1A] mb-6 pb-4 border-b border-[#E5E5E5]">
        Architecture Diagrams
      </h2>
      {diagrams.map((d, i) => (
        <div 
          key={i} 
          id={'diagram-' + slugify(d.title)} 
          className="mb-8 p-6 bg-white border border-[#E5E5E5] rounded-xl"
        >
          <h3 className="text-lg font-semibold mb-4">{d.title}</h3>
          {d.type === 'mermaid' ? (
            <div 
              ref={(el) => { if (el) diagramRefs.current.set(d.title, el); }} 
              className="overflow-x-auto"
            ></div>
          ) : (
            <pre className="bg-gray-50 p-4 rounded text-sm overflow-x-auto">
              {d.content}
            </pre>
          )}
        </div>
      ))}
    </div>
  );
}

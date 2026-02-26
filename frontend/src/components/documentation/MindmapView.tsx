import { useEffect, useRef } from 'react';
import mermaid from 'mermaid';
import type {  Relation  } from "../../types";

interface MindmapViewProps {
  relations: Relation[];
  show: boolean;
}

const slugify = (text: string): string => text.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/(^-|-$)/g, '');

export function MindmapView({ relations, show }: MindmapViewProps) {
  const mindmapRef = useRef<HTMLDivElement>(null);

  const generateMindmap = (): string => {
    if (relations.length === 0) return '';
    let graph = 'graph TD\n';
    relations.forEach(r => {
      graph += '  ' + slugify(r.from) + '[' + r.from + '] -->|' + r.type + '| ' + slugify(r.to) + '[' + r.to + ']\n';
    });
    return graph;
  };

  const mindmapCode = generateMindmap();

  useEffect(() => {
    if (show && mindmapRef.current && mindmapCode) {
      mermaid.render('mindmap-svg', mindmapCode).then(({ svg }) => {
        if (mindmapRef.current) mindmapRef.current.innerHTML = svg;
      });
    }
  }, [show, mindmapCode]);

  if (!show || !mindmapCode) return null;

  return (
    <div className="mb-8 p-6 bg-white border border-[#E5E5E5] rounded-xl">
      <h3 className="text-lg font-semibold mb-4">Document Relations</h3>
      <div ref={mindmapRef} className="overflow-x-auto"></div>
    </div>
  );
}

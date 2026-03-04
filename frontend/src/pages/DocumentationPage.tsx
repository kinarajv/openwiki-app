import { useEffect, useMemo, useState } from 'react';
import { useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { ExternalLink, Network, Pencil, Share2, Star } from 'lucide-react';
import { Sidebar } from '../components/layout/Sidebar';
import { IngestProgress } from '../components/ingestion/IngestProgress';
import { DocumentViewer } from '../components/documentation/DocumentViewer';
import { MindmapView } from '../components/documentation/MindmapView';
import { ChatPanel } from '../components/chat/ChatPanel';
import { ChatOverlay } from '../components/chat/ChatOverlay';
import { useApi } from '../hooks/useApi';
import { useSignalR } from '../hooks/useSignalR';
import type { RepoMetadata, DocSection, Diagram, Relation, ChatMessage } from '../types';

export function DocumentationPage() {
  const { owner, repo, sectionSlug } = useParams<{ owner: string; repo: string; sectionSlug?: string }>();
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();

  const repoUrl = searchParams.get('repoUrl') || undefined;
  const token = searchParams.get('token') || undefined;

  const [view, setView] = useState<'checking' | 'ingesting' | 'documentation'>('checking');
  const [activeRepo, setActiveRepo] = useState<string | null>(null);
  const [repoMetadata, setRepoMetadata] = useState<RepoMetadata | null>(null);
  const [docSections, setDocSections] = useState<DocSection[]>([]);
  const [diagrams, setDiagrams] = useState<Diagram[]>([]);
  const [relations, setRelations] = useState<Relation[]>([]);
  const [showMindmap, setShowMindmap] = useState(false);
  const [showOverlay, setShowOverlay] = useState(false);
  const [activeSection, setActiveSection] = useState<string | null>(null);
  const [loadingStep, setLoadingStep] = useState(0);
  const [ingestLogs, setIngestLogs] = useState<string[]>([]);

  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [isTyping, setIsTyping] = useState(false);

  const { checkRepo, getRepoData, ingestRepo, parseSections } = useApi();

  const { sendQuestion } = useSignalR(
    (chunk) => setMessages((prev) => {
      const last = prev[prev.length - 1]
      if (!last || last.role !== 'assistant') return prev
      return [...prev.slice(0, -1), { ...last, content: last.content + chunk }]
    }),
    () => {
      setIsTyping(false)
      setMessages((prev) => {
        const last = prev[prev.length - 1]
        if (!last || last.role !== 'assistant') return prev
        return [...prev.slice(0, -1), { ...last, isStreaming: false }]
      })
    }
  );

  const externalRepoUrl = useMemo(() => {
    if (repoMetadata?.sourceUrl) return repoMetadata.sourceUrl;
    if (repoUrl) return repoUrl;
    return `https://github.com/${owner}/${repo}`;
  }, [repoMetadata, repoUrl, owner, repo]);

  const repoFiles = useMemo(() => {
    const files = new Set<string>()
    docSections.forEach(s => {
      if (s.sourceCitations) {
        s.sourceCitations.forEach(c => {
          const match = c.match(/blob\/[^/]+\/(.+?)(?:#|$)/)
          if (match) files.add(match[1])
        })
      }
    })
    return files.size > 0 ? Array.from(files) : docSections.map(s => s.title + '.md')
  }, [docSections])

  useEffect(() => {
    let cancelled = false;

    const run = async () => {
      if (!owner || !repo) return;

      setView('checking');
      setDocSections([]);
      setDiagrams([]);
      setRelations([]);
      setMessages([]);

      const applyData = (data: any) => {
        if (cancelled) return;
        const sections: DocSection[] = Array.isArray(data?.sections) && data.sections.length > 0
          ? data.sections
          : parseSections(data?.document || '');

        setActiveRepo(`${owner}/${repo}`);
        setRepoMetadata(data?.metadata || { stars: 0, language: 'Unknown' });
        setDocSections(sections);
        setDiagrams(Array.isArray(data?.diagrams) ? data.diagrams : []);
        setRelations(Array.isArray(data?.relations) ? data.relations : []);

        const target = sectionSlug
          ? sections.find((s) => s.slug === sectionSlug)?.slug || sections[0]?.slug || null
          : sections[0]?.slug || null;
        setActiveSection(target);
        setView('documentation');
      };

      try {
        const checkData = await checkRepo(owner, repo);
        if (cancelled) return;
        if (checkData?.indexed) {
          const data = await getRepoData(owner, repo);
          if (cancelled) return;
          if (data) {
            applyData(data);
            return;
          }
        }

        setView('ingesting');
        setLoadingStep(0);
        setIngestLogs([]);
        const ingested = await ingestRepo(owner, repo, repoUrl, token, (event) => {
          if (cancelled) return;
          if (event.startsWith('step:')) {
            const parts = event.split(':');
            const stepNum = parseInt(parts[1], 10);
            const stepMsg = parts.slice(2).join(':');
            setLoadingStep(stepNum);
            setIngestLogs((prev) => [...prev, stepMsg]);
          } else if (event.startsWith('log:')) {
            setIngestLogs((prev) => [...prev, event.slice(4)]);
          }
        });
        if (cancelled) return;
        applyData(ingested);
      } catch (error: any) {
        if (cancelled) return;
        const fallback = parseSections(`# Error\n\n${error?.message || 'Unknown backend error'}`);
        setDocSections(fallback);
        setActiveSection(fallback[0]?.slug || null);
        setRepoMetadata({ stars: 0, language: 'Unknown' });
        setView('documentation');
      }
    };

    run();

    return () => { cancelled = true; };
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [owner, repo]);

  useEffect(() => {
    if (sectionSlug && docSections.length > 0) {
      const found = docSections.find((s) => s.slug === sectionSlug)
        || docSections.find((s) => s.slug.includes(sectionSlug) || sectionSlug.includes(s.slug));
      if (found && found.slug !== activeSection) {
        setActiveSection(found.slug);
      }
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [sectionSlug, docSections]);

  const currentSection = useMemo(
    () => docSections.find((s) => s.slug === activeSection) || docSections[0] || null,
    [docSections, activeSection]
  );

  const currentDiagram = useMemo(() => {
    if (!activeSection?.startsWith('diagram-')) return null;
    const diagramSlug = activeSection.replace('diagram-', '');
    return diagrams.find((d) => {
      const slug = d.title.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/(^-|-$)/g, '');
      return slug === diagramSlug || slug.includes(diagramSlug) || diagramSlug.includes(slug);
    }) || null;
  }, [activeSection, diagrams]);

  const onSectionClick = (slug: string) => {
    if (!owner || !repo) return;
    setActiveSection(slug);
    navigate(`/wiki/${owner}/${repo}/${slug}`);
  };

  const onSectionNavigate = (slug: string) => {
    if (!owner || !repo) return;
    const found = docSections.find((s) => s.slug === slug)
      || docSections.find((s) => s.slug.includes(slug) || slug.includes(s.slug))
      || docSections.find((s) => {
        const a = slug.replace(/(^the-|-the-|^a-|-a-)/g, '');
        const b = s.slug.replace(/(^the-|-the-|^a-|-a-)/g, '');
        return a === b || a.includes(b) || b.includes(a);
      });
    const target = found?.slug || slug;
    setActiveSection(target);
    navigate(`/wiki/${owner}/${repo}/${target}`);
  };

  const handleSendQuestion = async (mode: string, question: string) => {
    if (!activeRepo) return;
    setShowOverlay(true)
    const userMsg: ChatMessage = { id: crypto.randomUUID(), role: 'user', content: question, isStreaming: false }
    const assistantMsg: ChatMessage = { id: crypto.randomUUID(), role: 'assistant', content: '', isStreaming: true }
    setMessages((prev) => [...prev, userMsg, assistantMsg])
    setIsTyping(true)
    try {
      await sendQuestion(activeRepo, mode, question);
    } catch {
      setIsTyping(false)
      setMessages((prev) => {
        const last = prev[prev.length - 1]
        if (!last || last.role !== 'assistant') return prev
        return [...prev.slice(0, -1), { ...last, isStreaming: false, content: last.content || 'Error: Failed to get response.' }]
      })
    }
  };

  if (view === 'checking' || view === 'ingesting') {
    return <IngestProgress loadingStep={loadingStep} logs={ingestLogs} />;
  }

  return (
    <div className="min-h-screen bg-[#FAFAFA] font-sans">
      <header className="h-[60px] flex justify-between items-center px-6 border-b border-dashed border-[#E5E5E5] bg-white sticky top-0 z-40">
        <div className="flex items-center gap-4">
          <button onClick={() => navigate('/')} className="text-2xl font-bold text-[#1A1A1A] hover:text-blue-600">
            OpenWiki
          </button>
          {activeRepo && (
            <div className="flex items-center gap-2 text-[#6B6B6B]">
              <span className="text-lg">{activeRepo}</span>
              <a href={externalRepoUrl} target="_blank" rel="noopener noreferrer" className="hover:text-blue-600">
                <ExternalLink className="w-4 h-4" />
              </a>
            </div>
          )}
        </div>
        <div className="flex items-center gap-3">
          {relations.length > 0 && (
            <button
              onClick={() => setShowMindmap(!showMindmap)}
              className={`flex items-center gap-2 px-3 py-1.5 rounded-md text-sm font-medium ${showMindmap ? 'bg-blue-100 text-blue-700' : 'bg-[#F0F0F0] hover:bg-gray-200'}`}
            >
              <Network className="w-4 h-4" /> Mindmap
            </button>
          )}
          <button className="flex items-center gap-2 bg-[#F0F0F0] px-3 py-1.5 rounded-md text-sm font-medium hover:bg-gray-200">
            <Pencil className="w-4 h-4" /> Edit
          </button>
          <button className="flex items-center gap-2 bg-[#4B7BEC] text-white px-3 py-1.5 rounded-md text-sm font-medium hover:bg-blue-600">
            <Share2 className="w-4 h-4" /> Share
          </button>
        </div>
      </header>

      <div className="flex">
        <Sidebar docSections={docSections} diagrams={diagrams} activeSection={activeSection} onSectionClick={onSectionClick} />

        <main className="flex-1 min-w-0 pb-32">
          <div className="max-w-4xl mx-auto px-6 md:px-12 py-10">
            {repoMetadata && (
              <div className="mb-8 pb-6 border-b border-[#E5E5E5]">
                <div className="flex items-center gap-4 text-sm text-[#6B6B6B]">
                  {repoMetadata.language && <span>{repoMetadata.language}</span>}
                  {repoMetadata.stars > 0 && (
                    <span className="flex items-center gap-1">
                      <Star className="w-4 h-4" /> {repoMetadata.stars.toLocaleString()}
                    </span>
                  )}
                  {docSections.length > 0 && <span>{docSections.length} documents</span>}
                </div>
              </div>
            )}

            <MindmapView relations={relations} show={showMindmap} />

            {currentDiagram ? (
              <DocumentViewer
                content={`## ${currentDiagram.title}\n\n\`\`\`mermaid\n${currentDiagram.content}\n\`\`\``}
                onSectionNavigate={onSectionNavigate}
              />
            ) : currentSection ? (
              <DocumentViewer content={currentSection.content || ''} onSectionNavigate={onSectionNavigate} />
            ) : (
              <div className="text-[#6B6B6B]">No section available.</div>
            )}
          </div>
        </main>
      </div>

      <ChatPanel messages={messages} isTyping={isTyping} activeRepo={activeRepo} onSendQuestion={handleSendQuestion} />
      <ChatOverlay
        isOpen={showOverlay}
        onClose={() => setShowOverlay(false)}
        messages={messages}
        isTyping={isTyping}
        activeRepo={activeRepo}
        repoFiles={repoFiles}
        onSendQuestion={handleSendQuestion}
      />
    </div>
  );
}

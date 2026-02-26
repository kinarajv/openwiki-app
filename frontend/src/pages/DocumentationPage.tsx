import { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { ExternalLink, Database, Network, Pencil, Share2, Star } from 'lucide-react';
import { Sidebar } from '../components/layout/Sidebar';
import { IngestProgress } from '../components/ingestion/IngestProgress';
import { DocumentViewer } from '../components/documentation/DocumentViewer';
import { DiagramRenderer } from '../components/documentation/DiagramRenderer';
import { MindmapView } from '../components/documentation/MindmapView';
import { ChatPanel } from '../components/chat/ChatPanel';
import { useApi } from '../hooks/useApi';
import { useSignalR } from '../hooks/useSignalR';
import type { RepoMetadata, DocSection, Diagram, Relation } from '../types';

export function DocumentationPage() {
  const { owner, repo } = useParams<{ owner: string; repo: string }>();
  const navigate = useNavigate();
  const [view, setView] = useState<'checking' | 'ingesting' | 'documentation'>('checking');
  const [activeRepo, setActiveRepo] = useState<string | null>(null);
  const [repoMetadata, setRepoMetadata] = useState<RepoMetadata | null>(null);
  const [generatedDoc, setGeneratedDoc] = useState<string | null>(null);
  const [docSections, setDocSections] = useState<DocSection[]>([]);
  const [diagrams, setDiagrams] = useState<Diagram[]>([]);
  const [relations, setRelations] = useState<Relation[]>([]);
  const [cachedStatus, setCachedStatus] = useState<string | null>(null);
  const [showMindmap, setShowMindmap] = useState(false);
  const [activeSection, setActiveSection] = useState<string | null>(null);
  const [loadingStep, setLoadingStep] = useState(0);
  
  const [chatLog, setChatLog] = useState<string>('');
  const [isTyping, setIsTyping] = useState(false);

  const { checkRepo, getRepoData, ingestRepo } = useApi();
  
  const { sendQuestion } = useSignalR(
    (chunk) => setChatLog(prev => prev + chunk),
    () => setIsTyping(false)
  );

  useEffect(() => {
    const fetchData = async () => {
      if (!owner || !repo) return;
      
      setView('checking');
      setCachedStatus(null);
      
      try {
        const checkData = await checkRepo(owner, repo);
        if (checkData.indexed) {
          setCachedStatus('Previously indexed');
          const data = await getRepoData(owner, repo);
          if (data) {
            setActiveRepo(`${owner}/${repo}`);
            setGeneratedDoc(data.document);
            setRepoMetadata(data.metadata);
            setDocSections(data.sections || []);
            setDiagrams(data.diagrams || []);
            setRelations(data.relations || []);
            setView('documentation');
            return;
          }
        }
        
        setView('ingesting');
        setLoadingStep(0);
        setGeneratedDoc(null);
        setChatLog('');
        setDocSections([]);
        setDiagrams([]);
        setRelations([]);
        
        const stepTimer = setInterval(() => setLoadingStep(prev => prev < 3 ? prev + 1 : prev), 4000);

        try {
          const data = await ingestRepo(owner, repo);
          setActiveRepo(`${owner}/${repo}`);
          setGeneratedDoc(data.document);
          setRepoMetadata(data.metadata || { stars: 0, language: 'Unknown' });
          setCachedStatus(data.status === 'cached' ? 'Loaded from cache' : 'Newly indexed');
          setDocSections(data.sections || []);
          setDiagrams(data.diagrams || []);
          setRelations(data.relations || []);
          setView('documentation');
        } finally {
          clearInterval(stepTimer);
        }
      } catch (e: any) {
        setGeneratedDoc(`# Error\n\nBackend failed.\n\n\`\`\`\n${e.message}\n\`\`\``);
        setView('documentation');
      }
    };
    
    fetchData();
  }, [owner, repo, checkRepo, getRepoData, ingestRepo]);

  const scrollToSection = (slug: string) => {
    const element = document.getElementById(slug);
    if (element) element.scrollIntoView({ behavior: 'smooth', block: 'start' });
    setActiveSection(slug);
  };

  const handleSendQuestion = async (mode: string, question: string) => {
    if (activeRepo) {
      setChatLog(prev => prev + '\n\n**You:** ' + question + '\n\n**OpenWiki:**\n');
      setIsTyping(true);
      try {
        await sendQuestion(activeRepo, mode, question);
      } catch (e) {
        console.error(e);
        setIsTyping(false);
      }
    }
  };

  if (view === 'checking' || view === 'ingesting') {
    return <IngestProgress loadingStep={loadingStep} />;
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
              <a 
                href={`https://github.com/${activeRepo}`} 
                target="_blank" 
                rel="noopener noreferrer" 
                className="hover:text-blue-600"
              >
                <ExternalLink className="w-4 h-4" />
              </a>
              {cachedStatus && (
                <span className="text-xs bg-green-100 text-green-700 px-2 py-0.5 rounded-full flex items-center gap-1">
                  <Database className="w-3 h-3" />
                  {cachedStatus}
                </span>
              )}
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
        <Sidebar 
          docSections={docSections}
          diagrams={diagrams}
          activeSection={activeSection}
          onSectionClick={scrollToSection}
        />

        <main className="flex-1 min-w-0 pb-32">
          <div className="max-w-4xl mx-auto px-6 md:px-12 py-10">
            {repoMetadata && (
              <div className="mb-8 pb-6 border-b border-[#E5E5E5]">
                <div className="flex items-center gap-4 text-sm text-[#6B6B6B]">
                  {repoMetadata.language && (
                    <span className="flex items-center gap-1">
                      <span className="w-3 h-3 rounded-full bg-blue-500"></span>
                      {repoMetadata.language}
                    </span>
                  )}
                  {repoMetadata.stars > 0 && (
                    <span className="flex items-center gap-1">
                      <Star className="w-4 h-4" /> {repoMetadata.stars.toLocaleString()}
                    </span>
                  )}
                  {docSections.length > 0 && <span>{docSections.length} documents</span>}
                  {relations.length > 0 && <span>{relations.length} relations</span>}
                </div>
              </div>
            )}

            <MindmapView relations={relations} show={showMindmap} />

            {generatedDoc && <DocumentViewer content={generatedDoc} />}

            <DiagramRenderer diagrams={diagrams} />
          </div>
        </main>
      </div>

      <ChatPanel 
        chatLog={chatLog}
        isTyping={isTyping}
        activeRepo={activeRepo}
        onSendQuestion={handleSendQuestion}
      />
    </div>
  );
}

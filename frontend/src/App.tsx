import { useState, useEffect, useRef } from 'react';
import * as signalR from '@microsoft/signalr';
import mermaid from 'mermaid';
import { 
  ChevronDown,
  Pencil, 
  Share2, 
  Moon,
  Zap,
  Brain,
  ArrowRight,
  Loader2,
  CheckCircle2,
  Download,
  FileSearch,
  GitBranch,
  FileText,
  Search,
  Star,
  ExternalLink,
  Database,
  Network,
  FileCode
} from 'lucide-react';
import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';

mermaid.initialize({ startOnLoad: false, theme: 'neutral' });

const INGEST_STEPS = [
  { label: "Cloning repository from GitHub...", icon: Download },
  { label: "Analyzing source code structure...", icon: FileSearch },
  { label: "Generating multi-document architecture...", icon: GitBranch },
  { label: "Creating document relations and diagrams...", icon: FileText }
];

interface RepoSearchResult {
  fullName: string;
  description: string;
  stars: number;
  language: string;
}

interface DocSection {
  id: string;
  title: string;
  level: number;
  content: string;
  slug: string;
  summary: string;
  type: string;
}

interface Diagram {
  title: string;
  type: string;
  content: string;
}

interface Relation {
  from: string;
  to: string;
  type: string;
  description?: string;
}

const slugify = (text: string): string => text.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/(^-|-$)/g, '');

function App() {
  const [view, setView] = useState<'home' | 'checking' | 'ingesting' | 'documentation'>('home');
  const [searchQuery, setSearchQuery] = useState('');
  const [_searchResults, setSearchResults] = useState<RepoSearchResult[]>([]);
  const [isSearching, setIsSearching] = useState(false);
  
  const [connection, setConnection] = useState<signalR.HubConnection | null>(null);
  const [chatLog, setChatLog] = useState<string>('');
  const [question, setQuestion] = useState('');
  const [activeRepo, setActiveRepo] = useState<string | null>(null);
  const [repoMetadata, setRepoMetadata] = useState<{stars: number, language: string, description?: string} | null>(null);
  const [generatedDoc, setGeneratedDoc] = useState<string | null>(null);
  const [docSections, setDocSections] = useState<DocSection[]>([]);
  const [diagrams, setDiagrams] = useState<Diagram[]>([]);
  const [relations, setRelations] = useState<Relation[]>([]);
  const [mode, setMode] = useState<'Fast' | 'Deep'>('Fast');
  const [isTyping, setIsTyping] = useState(false);
  const [loadingStep, setLoadingStep] = useState(0);
  const [cachedStatus, setCachedStatus] = useState<string | null>(null);
  const [showMindmap, setShowMindmap] = useState(false);
  const [activeSection, setActiveSection] = useState<string | null>(null);
  
  const chatEndRef = useRef<HTMLDivElement>(null);
  const mindmapRef = useRef<HTMLDivElement>(null);
  const diagramRefs = useRef<Map<string, HTMLDivElement>>(new Map());

  const getBackendUrl = () => {
    const protocol = window.location.protocol;
    const hostname = window.location.hostname;
    return protocol + '//' + hostname + ':5000';
  };

  const popularRepos: RepoSearchResult[] = [
    { fullName: 'microsoft/vscode', description: 'Visual Studio Code', stars: 168000, language: 'TypeScript' },
    { fullName: 'facebook/react', description: 'React library', stars: 230000, language: 'JavaScript' },
    { fullName: 'microsoft/playwright', description: 'Playwright testing', stars: 68000, language: 'TypeScript' },
    { fullName: 'router-for-me/CLIProxyAPI', description: 'CLI Proxy API', stars: 100, language: 'TypeScript' },
  ];

  const generateMindmap = (): string => {
    if (relations.length === 0) return '';
    let graph = 'graph TD\n';
    relations.forEach(r => {
      graph += '  ' + slugify(r.from) + '[' + r.from + '] -->|' + r.type + '| ' + slugify(r.to) + '[' + r.to + ']\n';
    });
    return graph;
  };

  const mindmapCode = generateMindmap();

  // Render mindmap when shown
  useEffect(() => {
    if (showMindmap && mindmapRef.current && mindmapCode) {
      mermaid.render('mindmap-svg', mindmapCode).then(({ svg }) => {
        if (mindmapRef.current) mindmapRef.current.innerHTML = svg;
      });
    }
  }, [showMindmap, mindmapCode]);

  // Render diagrams
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

  useEffect(() => {
    chatEndRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [chatLog, isTyping]);

  useEffect(() => {
    const newConnection = new signalR.HubConnectionBuilder()
      .withUrl(getBackendUrl() + '/chatHub') 
      .withAutomaticReconnect()
      .build();
    setConnection(newConnection);
  }, []);

  useEffect(() => {
    if (connection) {
      connection.start()
        .then(() => {
          connection.on('ReceiveChunk', (chunk: string) => setChatLog(prev => prev + chunk));
          connection.on('ReceiveComplete', () => setIsTyping(false));
        })
        .catch(e => console.log('SignalR failed:', e));
    }
  }, [connection]);

  const handleSearch = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!searchQuery.trim()) return;
    setIsSearching(true);
    setSearchResults([]);
    try {
      const response = await fetch(getBackendUrl() + '/api/search?q=' + encodeURIComponent(searchQuery));
      if (response.ok) {
        const data = await response.json();
        const results = (data.results || []).map((r: any) => ({
          fullName: r.fullName || r.full_name,
          description: r.description || '',
          stars: r.stars || r.stargazers_count || 0,
          language: r.language || 'Unknown'
        }));
        setSearchResults(results.length > 0 ? results : popularRepos.filter(r => r.fullName.toLowerCase().includes(searchQuery.toLowerCase())));
      }
    } catch {
      setSearchResults(popularRepos.filter(r => r.fullName.toLowerCase().includes(searchQuery.toLowerCase())));
    } finally {
      setIsSearching(false);
    }
  };

  const handleRepoSelect = async (fullName: string) => {
    const parts = fullName.split('/');
    const owner = parts[0];
    const repo = parts[1];
    
    setView('checking');
    setCachedStatus(null);
    
    try {
      const checkResponse = await fetch(getBackendUrl() + '/api/repo/check?owner=' + owner + '&repo=' + repo);
      if (checkResponse.ok) {
        const checkData = await checkResponse.json();
        if (checkData.indexed) {
          setCachedStatus('Previously indexed');
          const dataResponse = await fetch(getBackendUrl() + '/api/repo/data?owner=' + owner + '&repo=' + repo);
          if (dataResponse.ok) {
            const data = await dataResponse.json();
            setActiveRepo(fullName);
            setGeneratedDoc(data.document);
            setRepoMetadata(data.metadata);
            setDocSections(data.sections || []);
            setDiagrams(data.diagrams || []);
            setRelations(data.relations || []);
            setView('documentation');
            return;
          }
        }
      }
    } catch {}
    
    await ingestRepository(owner, repo);
  };

  const ingestRepository = async (owner: string, repo: string) => {
    setView('ingesting');
    setLoadingStep(0);
    setGeneratedDoc(null);
    setChatLog('');
    setDocSections([]);
    setDiagrams([]);
    setRelations([]);
    
    const stepTimer = setInterval(() => setLoadingStep(prev => prev < INGEST_STEPS.length - 1 ? prev + 1 : prev), 4000);

    const repoFullName = owner + '/' + repo;

    try {
      const response = await fetch(getBackendUrl() + '/api/ingest?owner=' + owner + '&repo=' + repo, { method: 'POST' });
      
      if (response.ok) {
        const data = await response.json();
        setActiveRepo(repoFullName);
        setGeneratedDoc(data.document);
        setRepoMetadata(data.metadata || { stars: 0, language: 'Unknown' });
        setCachedStatus(data.status === 'cached' ? 'Loaded from cache' : 'Newly indexed');
        setDocSections(data.sections && Array.isArray(data.sections) ? data.sections : parseSections(data.document));
        setDiagrams(data.diagrams || []);
        setRelations(data.relations || []);
        setView('documentation');
      } else {
        const errData = await response.json().catch(() => ({}));
        setGeneratedDoc('# Error\n\n```\n' + (errData.error || 'Unknown error') + '\n```');
        setView('documentation');
      }
    } catch (e: any) {
      setGeneratedDoc('# Error\n\nBackend failed.\n\n```\n' + e.message + '\n```');
      setView('documentation');
    } finally {
      clearInterval(stepTimer);
    }
  };

  const parseSections = (markdown: string): DocSection[] => {
    const sections: DocSection[] = [];
    const lines = markdown.split('\n');
    let currentSection: DocSection | null = null;
    let sectionContent: string[] = [];
    
    for (let idx = 0; idx < lines.length; idx++) {
      const line = lines[idx];
      const h1Match = line.match(/^# (.+)$/);
      const h2Match = line.match(/^## (.+)$/);
      
      if (h1Match || h2Match) {
        if (currentSection !== null) {
          currentSection.content = sectionContent.join('\n');
          sections.push(currentSection);
        }
        const title = (h1Match || h2Match)?.[1] || '';
        currentSection = { id: 'section-' + idx, title, level: h1Match ? 1 : 2, content: '', slug: slugify(title), summary: '', type: 'content' };
        sectionContent = [];
      } else if (currentSection !== null) {
        sectionContent.push(line);
      }
    }
    if (currentSection !== null) {
      currentSection.content = sectionContent.join('\n');
      sections.push(currentSection);
    }
    return sections;
  };

  const scrollToSection = (slug: string) => {
    const element = document.getElementById(slug);
    if (element) element.scrollIntoView({ behavior: 'smooth', block: 'start' });
    setActiveSection(slug);
  };

  const sendQuestion = async () => {
    if (connection && question.trim() && activeRepo) {
      setChatLog(prev => prev + '\n\n**You:** ' + question + '\n\n**OpenWiki:**\n');
      setIsTyping(true);
      try {
        await connection.send('AskQuestion', activeRepo, mode.toLowerCase(), question);
        setQuestion('');
      } catch (e) {
        console.error(e);
        setIsTyping(false);
      }
    }
  };

  // HOME VIEW
  if (view === 'home' || view === 'checking') {
    return (
      <div className="min-h-screen bg-[#FAFAFA] font-sans">
        <header className="border-b border-dashed border-[#E5E5E5] bg-white">
          <div className="max-w-5xl mx-auto px-6 py-4 flex justify-between items-center">
            <span className="text-2xl font-bold text-[#1A1A1A]">OpenWiki</span>
            <button className="text-[#6B6B6B] hover:text-[#1A1A1A]"><Moon className="w-5 h-5" /></button>
          </div>
        </header>

        <main className="max-w-5xl mx-auto px-6 py-16">
          <div className="text-center mb-12">
            <h1 className="text-4xl md:text-5xl font-bold text-[#1A1A1A] mb-4">Which repo would you like to understand?</h1>
            <p className="text-xl text-[#6B6B6B]">Multi-document AI with diagrams, relations, and persistent indexing.</p>
          </div>

          <form onSubmit={handleSearch} className="max-w-2xl mx-auto mb-16">
            <div className="relative">
              <Search className="absolute left-4 top-1/2 transform -translate-y-1/2 w-5 h-5 text-[#6B6B6B]" />
              <input type="text" value={searchQuery} onChange={(e) => setSearchQuery(e.target.value)} className="w-full pl-12 pr-4 py-4 text-lg border border-[#E5E5E5] rounded-xl bg-white shadow-sm focus:outline-none focus:ring-2 focus:ring-blue-500" placeholder="Search repositories..." />
              {isSearching && <Loader2 className="absolute right-4 top-1/2 transform -translate-y-1/2 w-5 h-5 text-blue-500 animate-spin" />}
            </div>
          </form>

          {view === 'checking' && (
            <div className="text-center mb-8"><Loader2 className="w-6 h-6 text-blue-500 animate-spin mx-auto" /><p className="text-[#6B6B6B] mt-2">Checking if indexed...</p></div>
          )}

          <div className="max-w-2xl mx-auto">
            <h2 className="text-lg font-semibold text-[#1A1A1A] mb-4">Popular Repositories</h2>
            <div className="grid gap-3">
              {popularRepos.map((repo) => (
                <button key={repo.fullName} onClick={() => handleRepoSelect(repo.fullName)} className="w-full text-left p-4 bg-white border border-[#E5E5E5] rounded-lg hover:border-blue-300 hover:shadow-sm transition-all">
                  <div className="flex justify-between items-start">
                    <div><div className="font-medium text-blue-600">{repo.fullName}</div><div className="text-sm text-[#6B6B6B] mt-1">{repo.description}</div></div>
                    <div className="flex items-center gap-4 text-sm text-[#6B6B6B]"><span>{repo.language}</span><span className="flex items-center gap-1"><Star className="w-4 h-4" /> {repo.stars.toLocaleString()}</span></div>
                  </div>
                </button>
              ))}
            </div>
          </div>
        </main>
      </div>
    );
  }

  // INGESTING VIEW
  if (view === 'ingesting') {
    return (
      <div className="min-h-screen bg-[#FAFAFA] flex items-center justify-center font-sans">
        <div className="max-w-lg w-full px-6">
          <div className="text-center mb-8"><h1 className="text-2xl font-bold text-[#1A1A1A] mb-2">OpenWiki</h1><p className="text-[#6B6B6B]">Indexing...</p></div>
          <div className="w-full flex flex-col gap-4">
            {INGEST_STEPS.map((step, idx) => {
              const Icon = step.icon;
              const isActive = idx === loadingStep;
              const isComplete = idx < loadingStep;
              return (
                <div key={idx} className={'flex items-center gap-4 p-4 rounded-lg transition-all ' + (idx > loadingStep ? 'opacity-40 bg-gray-50' : isActive ? 'bg-blue-50 border-2 border-blue-200' : 'bg-white border border-[#E5E5E5]')}>
                  <div className={'w-12 h-12 rounded-full flex items-center justify-center ' + (isComplete ? 'bg-green-100' : isActive ? 'bg-blue-100' : 'bg-gray-100')}>
                    {isComplete ? <CheckCircle2 className="w-6 h-6 text-green-600" /> : isActive ? <Loader2 className="w-6 h-6 text-blue-600 animate-spin" /> : <Icon className="w-6 h-6 text-gray-400" />}
                  </div>
                  <span className={'text-base font-medium ' + (isActive ? 'text-blue-700' : isComplete ? 'text-green-700' : 'text-[#6B6B6B]')}>{step.label}</span>
                </div>
              );
            })}
          </div>
        </div>
      </div>
    );
  }

  // DOCUMENTATION VIEW
  return (
    <div className="min-h-screen bg-[#FAFAFA] font-sans">
      <header className="h-[60px] flex justify-between items-center px-6 border-b border-dashed border-[#E5E5E5] bg-white sticky top-0 z-40">
        <div className="flex items-center gap-4">
          <button onClick={() => setView('home')} className="text-2xl font-bold text-[#1A1A1A] hover:text-blue-600">OpenWiki</button>
          {activeRepo && (
            <div className="flex items-center gap-2 text-[#6B6B6B]">
              <span className="text-lg">{activeRepo}</span>
              <a href={'https://github.com/' + activeRepo} target="_blank" rel="noopener noreferrer" className="hover:text-blue-600"><ExternalLink className="w-4 h-4" /></a>
              {cachedStatus && <span className="text-xs bg-green-100 text-green-700 px-2 py-0.5 rounded-full flex items-center gap-1"><Database className="w-3 h-3" />{cachedStatus}</span>}
            </div>
          )}
        </div>
        <div className="flex items-center gap-3">
          {relations.length > 0 && (
            <button onClick={() => setShowMindmap(!showMindmap)} className={'flex items-center gap-2 px-3 py-1.5 rounded-md text-sm font-medium ' + (showMindmap ? 'bg-blue-100 text-blue-700' : 'bg-[#F0F0F0] hover:bg-gray-200')}><Network className="w-4 h-4" /> Mindmap</button>
          )}
          <button className="flex items-center gap-2 bg-[#F0F0F0] px-3 py-1.5 rounded-md text-sm font-medium hover:bg-gray-200"><Pencil className="w-4 h-4" /> Edit</button>
          <button className="flex items-center gap-2 bg-[#4B7BEC] text-white px-3 py-1.5 rounded-md text-sm font-medium hover:bg-blue-600"><Share2 className="w-4 h-4" /> Share</button>
        </div>
      </header>

      <div className="flex">
        <aside className="hidden lg:block w-[280px] border-r border-dashed border-[#E5E5E5] bg-white h-[calc(100vh-60px)] sticky top-[60px] overflow-y-auto">
          <div className="p-4">
            <div className="text-xs font-semibold text-[#6B6B6B] uppercase tracking-wide mb-3">Documents ({docSections.length})</div>
            <nav className="space-y-1">
              {docSections.map((section) => (
                <button key={section.id} onClick={() => scrollToSection(section.slug)} className={'w-full text-left py-2 px-3 rounded-md text-sm transition-colors ' + (activeSection === section.slug ? 'bg-blue-100 text-blue-700' : section.level === 1 ? 'font-medium text-[#1A1A1A] hover:bg-gray-100' : 'text-[#6B6B6B] hover:bg-gray-100 pl-6')}>
                  {section.title}
                </button>
              ))}
            </nav>
            {diagrams.length > 0 && (
              <>
                <div className="text-xs font-semibold text-[#6B6B6B] uppercase tracking-wide mt-6 mb-3">Diagrams</div>
                <nav className="space-y-1">
                  {diagrams.map((d, i) => (
                    <button key={i} onClick={() => scrollToSection('diagram-' + slugify(d.title))} className="w-full text-left py-2 px-3 rounded-md text-sm text-[#6B6B6B] hover:bg-gray-100 flex items-center gap-2">
                      <FileCode className="w-4 h-4" /> {d.title}
                    </button>
                  ))}
                </nav>
              </>
            )}
          </div>
        </aside>

        <main className="flex-1 min-w-0 pb-32">
          <div className="max-w-4xl mx-auto px-6 md:px-12 py-10">
            {repoMetadata && (
              <div className="mb-8 pb-6 border-b border-[#E5E5E5]">
                <div className="flex items-center gap-4 text-sm text-[#6B6B6B]">
                  {repoMetadata.language && <span className="flex items-center gap-1"><span className="w-3 h-3 rounded-full bg-blue-500"></span>{repoMetadata.language}</span>}
                  {repoMetadata.stars > 0 && <span className="flex items-center gap-1"><Star className="w-4 h-4" /> {repoMetadata.stars.toLocaleString()}</span>}
                  {docSections.length > 0 && <span>{docSections.length} documents</span>}
                  {relations.length > 0 && <span>{relations.length} relations</span>}
                </div>
              </div>
            )}

            {showMindmap && mindmapCode && (
              <div className="mb-8 p-6 bg-white border border-[#E5E5E5] rounded-xl">
                <h3 className="text-lg font-semibold mb-4">Document Relations</h3>
                <div ref={mindmapRef} className="overflow-x-auto"></div>
              </div>
            )}

            {chatLog && (
              <div className="bg-[#F0F0F5] p-6 rounded-xl border border-[#E5E5E5] mb-8">
                <div className="prose prose-sm max-w-none text-[#333333]"><ReactMarkdown remarkPlugins={[remarkGfm]}>{chatLog}</ReactMarkdown></div>
                {isTyping && <span className="animate-pulse inline-block w-2 h-4 bg-[#6B6B6B] ml-1 mt-2"></span>}
                <div ref={chatEndRef} />
              </div>
            )}

            {generatedDoc && (
              <article className="prose prose-slate max-w-none prose-headings:scroll-mt-20 prose-h1:text-3xl prose-h1:font-bold prose-h1:text-[#1A1A1A] prose-h1:border-b prose-h1:border-[#E5E5E5] prose-h1:pb-4 prose-h1:mb-6 prose-h2:text-2xl prose-h2:font-semibold prose-h2:text-[#1A1A1A] prose-h2:mt-10 prose-h2:mb-4 prose-p:text-[#333333] prose-p:leading-relaxed prose-a:text-blue-600 prose-code:text-[#d63384] prose-code:bg-[#f8f9fa] prose-code:px-1.5 prose-code:py-0.5 prose-code:rounded prose-code:before:content-none prose-code:after:content-none prose-pre:bg-[#1e1e1e] prose-pre:text-[#d4d4d4] prose-pre:rounded-lg prose-pre:p-4">
                <ReactMarkdown remarkPlugins={[remarkGfm]}>{generatedDoc}</ReactMarkdown>
              </article>
            )}

            {diagrams.length > 0 && (
              <div className="mt-12">
                <h2 className="text-2xl font-bold text-[#1A1A1A] mb-6 pb-4 border-b border-[#E5E5E5]">Architecture Diagrams</h2>
                {diagrams.map((d, i) => (
                  <div key={i} id={'diagram-' + slugify(d.title)} className="mb-8 p-6 bg-white border border-[#E5E5E5] rounded-xl">
                    <h3 className="text-lg font-semibold mb-4">{d.title}</h3>
                    {d.type === 'mermaid' ? (
                      <div ref={(el) => { if (el) diagramRefs.current.set(d.title, el); }} className="overflow-x-auto"></div>
                    ) : (
                      <pre className="bg-gray-50 p-4 rounded text-sm overflow-x-auto">{d.content}</pre>
                    )}
                  </div>
                ))}
              </div>
            )}
          </div>
        </main>
      </div>

      <div className="fixed bottom-6 left-1/2 transform -translate-x-1/2 w-[90%] md:w-[600px] bg-white rounded-xl shadow-xl flex flex-col p-4 border border-[#E5E5E5] z-50">
        <input type="text" value={question} onChange={e => setQuestion(e.target.value)} onKeyDown={e => e.key === 'Enter' && sendQuestion()} placeholder={'Ask about ' + (activeRepo || 'this repository') + '...'} className="border-none bg-transparent text-base p-2 text-[#1A1A1A] w-full focus:outline-none placeholder:text-[#6B6B6B]" />
        <div className="flex justify-between items-center mt-2 pt-2 border-t border-[#E5E5E5]">
          <button onClick={() => setMode(mode === 'Fast' ? 'Deep' : 'Fast')} className="flex items-center gap-2 text-sm text-[#4A4A4A] hover:bg-gray-100 px-3 py-1.5 rounded-md font-medium">
            {mode === 'Fast' ? <Zap className="w-4 h-4 text-blue-500" /> : <Brain className="w-4 h-4 text-purple-500" />}
            {mode}
            <ChevronDown className="w-4 h-4" />
          </button>
          <button onClick={sendQuestion} disabled={isTyping || !question.trim()} className={'rounded-full p-2 transition-colors ' + (question.trim() ? 'bg-[#1A1A1A] text-white hover:bg-black' : 'bg-[#E5E5E5] text-gray-400')}>
            <ArrowRight className="w-5 h-5" />
          </button>
        </div>
      </div>
    </div>
  );
}

export default App;

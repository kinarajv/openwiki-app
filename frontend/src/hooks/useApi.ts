import { useCallback, useState } from 'react';
import { getBackendUrl } from '../config/api';
import type { DocSection, RepoSearchResult } from '../types';

const slugify = (text: string): string =>
  text.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/(^-|-$)/g, '');

const normalizeSections = (sections: any[]): DocSection[] => {
  return (sections || []).map((s: any, idx: number) => ({
    id: String(s.id ?? `section-${idx}`),
    title: s.title || '',
    level: Number(s.level ?? 2),
    content: s.content || '',
    slug: s.slug || slugify(s.title || `section-${idx}`),
    summary: s.summary || '',
    type: s.type || 'content',
    sectionNumber: s.sectionNumber || s.section_number || '',
    sourceCitations: s.sourceCitations || s.source_citations || []
  }));
};

export function useApi() {
  const [isSearching, setIsSearching] = useState(false);

  const searchRepos = async (query: string): Promise<RepoSearchResult[]> => {
    setIsSearching(true);
    try {
      const response = await fetch(`${getBackendUrl()}/api/search?q=${encodeURIComponent(query)}`);
      if (!response.ok) return [];
      const data = await response.json();
      return (data.results || []).map((r: any) => ({
        fullName: r.fullName || r.full_name,
        description: r.description || '',
        stars: r.stars || r.stargazers_count || 0,
        language: r.language || 'Unknown'
      }));
    } catch {
      return [];
    } finally {
      setIsSearching(false);
    }
  };

  const checkRepo = async (owner: string, repo: string) => {
    try {
      const response = await fetch(`${getBackendUrl()}/api/repo/check?owner=${owner}&repo=${repo}`);
      if (!response.ok) return { indexed: false };
      return await response.json();
    } catch {
      return { indexed: false };
    }
  };

  const getRepoData = async (owner: string, repo: string) => {
    try {
      const response = await fetch(`${getBackendUrl()}/api/repo/data?owner=${owner}&repo=${repo}`);
      if (!response.ok) return null;
      const data = await response.json();
      data.sections = normalizeSections(data.sections || []);
      return data;
    } catch {
      return null;
    }
  };

  const parseSections = useCallback((markdown: string): DocSection[] => {
    const sections: DocSection[] = [];
    const lines = markdown.split('\n');
    let currentSection: DocSection | null = null;
    let sectionContent: string[] = [];

    for (let idx = 0; idx < lines.length; idx++) {
      const line = lines[idx];
      const h1Match = line.match(/^# (.+)$/);
      const h2Match = line.match(/^## (.+)$/);

      if (h1Match || h2Match) {
        if (currentSection) {
          currentSection.content = sectionContent.join('\n');
          sections.push(currentSection);
        }

        const title = (h1Match || h2Match)?.[1] || '';
        currentSection = {
          id: `section-${idx}`,
          title,
          level: h1Match ? 1 : 2,
          content: '',
          slug: slugify(title),
          summary: '',
          type: 'content',
          sectionNumber: '',
          sourceCitations: []
        };
        sectionContent = [];
      } else if (currentSection) {
        sectionContent.push(line);
      }
    }

    if (currentSection) {
      currentSection.content = sectionContent.join('\n');
      sections.push(currentSection);
    }

    return sections;
  }, []);

  const ingestRepo = async (owner: string, repo: string, repoUrl?: string, token?: string, onProgress?: (event: string) => void) => {
    const params = new URLSearchParams({ owner, repo });
    if (repoUrl) params.set('repoUrl', repoUrl);
    if (token) params.set('token', token);

    const response = await fetch(`${getBackendUrl()}/api/ingest?${params.toString()}`, {
      method: 'POST'
    });

    if (!response.ok) {
      const errData = await response.json().catch(() => ({}));
      throw new Error(errData.error || 'Unknown error');
    }

    const contentType = response.headers.get('content-type') || '';

    if (contentType.includes('application/json')) {
      const data = await response.json();
      if (!Array.isArray(data.sections)) {
        data.sections = parseSections(data.document || '');
      } else {
        data.sections = normalizeSections(data.sections);
      }
      return data;
    }

    if (!response.body) {
      throw new Error('No response body for SSE stream');
    }

    const reader = response.body.getReader();
    const decoder = new TextDecoder();
    let buffer = '';
    let finalData: Record<string, unknown> | null = null;

    while (true) {
      const { done, value } = await reader.read();
      if (done) break;

      buffer += decoder.decode(value, { stream: true });
      const lines = buffer.split('\n');
      buffer = lines.pop() || '';

      for (const line of lines) {
        if (line.startsWith('data: ')) {
          const payload = line.slice(6).trim();
          if (!payload) continue;

          if (payload.startsWith('{')) {
            try {
              const parsed = JSON.parse(payload);
              if (parsed.type === 'complete') {
                finalData = parsed;
                onProgress?.('step:5:Indexing complete');
              } else if (parsed.type === 'error') {
                throw new Error(parsed.message || 'Ingestion failed');
              }
            } catch (e) {
              if (e instanceof Error && e.message !== 'Ingestion failed') {
                onProgress?.(payload);
              } else {
                throw e;
              }
            }
          } else {
            onProgress?.(payload);
          }
        }
      }
    }

    if (!finalData) {
      throw new Error('SSE stream ended without complete event');
    }

    const repoData = await getRepoData(owner, repo);
    if (repoData) return repoData;

    return finalData;
  };

  return { isSearching, searchRepos, checkRepo, getRepoData, ingestRepo, parseSections };
}

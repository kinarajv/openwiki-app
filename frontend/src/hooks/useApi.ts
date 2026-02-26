import { useState, useCallback } from 'react';
import { getBackendUrl } from '../config/api';
import type { RepoSearchResult, DocSection } from "../types";

const slugify = (text: string): string => text.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/(^-|-$)/g, '');

export function useApi() {
  const [isSearching, setIsSearching] = useState(false);

  const searchRepos = async (query: string): Promise<RepoSearchResult[]> => {
    setIsSearching(true);
    try {
      const response = await fetch(`${getBackendUrl()}/api/search?q=${encodeURIComponent(query)}`);
      if (response.ok) {
        const data = await response.json();
        return (data.results || []).map((r: any) => ({
          fullName: r.fullName || r.full_name,
          description: r.description || '',
          stars: r.stars || r.stargazers_count || 0,
          language: r.language || 'Unknown'
        }));
      }
      return [];
    } catch {
      return [];
    } finally {
      setIsSearching(false);
    }
  };

  const checkRepo = async (owner: string, repo: string) => {
    try {
      const response = await fetch(`${getBackendUrl()}/api/repo/check?owner=${owner}&repo=${repo}`);
      if (response.ok) {
        return await response.json();
      }
      return { indexed: false };
    } catch {
      return { indexed: false };
    }
  };

  const getRepoData = async (owner: string, repo: string) => {
    try {
      const response = await fetch(`${getBackendUrl()}/api/repo/data?owner=${owner}&repo=${repo}`);
      if (response.ok) {
        return await response.json();
      }
      return null;
    } catch {
      return null;
    }
  };

  const ingestRepo = async (owner: string, repo: string) => {
    try {
      const response = await fetch(`${getBackendUrl()}/api/ingest?owner=${owner}&repo=${repo}`, { method: 'POST' });
      if (response.ok) {
        const data = await response.json();
        if (!data.sections || !Array.isArray(data.sections)) {
           data.sections = parseSections(data.document);
        }
        return data;
      } else {
        const errData = await response.json().catch(() => ({}));
        throw new Error(errData.error || 'Unknown error');
      }
    } catch (e: any) {
      throw e;
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
  }, []);

  return { isSearching, searchRepos, checkRepo, getRepoData, ingestRepo };
}

export interface RepoSearchResult {
  fullName: string;
  description: string;
  stars: number;
  language: string;
}

export interface DocSection {
  id: string;
  title: string;
  level: number;
  content: string;
  slug: string;
  summary: string;
  type: string;
  sectionNumber?: string;
  sourceCitations?: string[];
}

export interface Diagram {
  title: string;
  type: string;
  content: string;
}

export interface Relation {
  from: string;
  to: string;
  type: string;
  description?: string;
}

export interface RepoMetadata {
  stars: number;
  language: string;
  description?: string;
  sourceUrl?: string;
}

export interface ChatMessage {
  id: string
  role: 'user' | 'assistant'
  content: string
  isStreaming: boolean
}

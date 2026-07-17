export type MemberRole = "Owner" | "Editor" | "Viewer";

export interface Member {
  id: string;
  workspaceId: string;
  name: string;
  email: string;
  role: MemberRole;
}

export interface LoginInput {
  email: string;
  password: string;
}

export interface LoginResponse {
  token: string;
  expiresAt: string;
  member: Member;
}

export interface WorkspaceSummary {
  id: string;
  name: string;
  slug: string;
  memberCount: number;
  pageCount: number;
  createdAt: string;
}

export type PageVisibility = "Private" | "Workspace" | "Public";
export type SharePermission = "View" | "Edit";

export interface PageTreeNode {
  id: string;
  parentId: string | null;
  title: string;
  icon: string | null;
  position: number;
  isFavorite: boolean;
  children: PageTreeNode[];
}

export interface BreadcrumbItem {
  id: string;
  title: string;
  icon: string | null;
}

export interface PageDetail {
  id: string;
  workspaceId: string;
  parentId: string | null;
  title: string;
  icon: string | null;
  position: number;
  visibility: PageVisibility;
  permission: SharePermission;
  publicSlug: string | null;
  isFavorite: boolean;
  createdAt: string;
  updatedAt: string;
  breadcrumb: BreadcrumbItem[];
}

export interface ShareSettings {
  visibility: PageVisibility;
  permission: SharePermission;
  publicSlug: string | null;
}

export interface SearchResult {
  pageId: string;
  title: string;
  icon: string | null;
  matchedTitle: boolean;
  snippet: string | null;
}

export interface TrashItem {
  id: string;
  title: string;
  icon: string | null;
  deletedAt: string | null;
}

export interface Favorite {
  id: string;
  title: string;
  icon: string | null;
}

export interface CreatePageInput {
  title: string;
  parentId?: string | null;
  position?: number;
  icon?: string | null;
}

export interface UpdatePageInput {
  title: string;
  icon?: string | null;
}

export interface MovePageInput {
  parentId: string | null;
  position: number;
}

export type BlockType =
  | "Paragraph"
  | "Heading"
  | "Todo"
  | "Bulleted"
  | "Quote"
  | "Code"
  | "Table"
  | "Toggle"
  | "Callout"
  | "Divider"
  | "Image";

/** Permissive view of a block's type-specific JSON payload. */
export interface BlockContent {
  text?: string;
  level?: number;
  checked?: boolean;
  language?: string;
  // v2 payloads
  rows?: string[][];
  collapsed?: boolean;
  emoji?: string;
  url?: string;
  alt?: string;
}

export interface Block {
  id: string;
  pageId: string;
  parentBlockId: string | null;
  type: BlockType;
  position: number;
  content: BlockContent;
  createdAt: string;
  updatedAt: string;
}

export interface CreateBlockInput {
  type: BlockType;
  content: BlockContent;
  position?: number;
  parentId?: string | null;
}

export interface UpdateBlockInput {
  type?: BlockType;
  content: BlockContent;
}

export interface MoveBlockInput {
  position: number;
  parentId?: string | null;
}

// ---- page history ----

export interface DiffSummary {
  added: number;
  removed: number;
  changed: number;
}

export interface VersionSummary {
  versionNumber: number;
  title: string;
  icon: string | null;
  blockCount: number;
  createdByName: string | null;
  label: string | null;
  createdAt: string;
}

export interface BlockSnapshot {
  id: string;
  parentBlockId: string | null;
  type: BlockType;
  position: number;
  content: BlockContent;
}

export interface VersionDetail {
  versionNumber: number;
  title: string;
  icon: string | null;
  blocks: BlockSnapshot[];
  diff: DiffSummary;
  createdByName: string | null;
  label: string | null;
  createdAt: string;
}

// ---- comments & mentions ----

export interface MentionRef {
  memberId: string;
  name: string;
}

export interface Comment {
  id: string;
  pageId: string;
  blockId: string | null;
  parentCommentId: string | null;
  authorMemberId: string;
  authorName: string;
  body: string;
  isResolved: boolean;
  resolvedAt: string | null;
  mentions: MentionRef[];
  createdAt: string;
  updatedAt: string;
}

export interface CreateCommentInput {
  body: string;
  blockId?: string | null;
  parentCommentId?: string | null;
}

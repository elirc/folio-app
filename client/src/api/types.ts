export interface WorkspaceSummary {
  id: string;
  name: string;
  slug: string;
  memberCount: number;
  pageCount: number;
  createdAt: string;
}

export interface PageTreeNode {
  id: string;
  parentId: string | null;
  title: string;
  icon: string | null;
  position: number;
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
  createdAt: string;
  updatedAt: string;
  breadcrumb: BreadcrumbItem[];
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

export type BlockType = "Paragraph" | "Heading" | "Todo" | "Bulleted" | "Quote" | "Code";

/** Permissive view of a block's type-specific JSON payload. */
export interface BlockContent {
  text?: string;
  level?: number;
  checked?: boolean;
  language?: string;
}

export interface Block {
  id: string;
  pageId: string;
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
}

export interface UpdateBlockInput {
  type?: BlockType;
  content: BlockContent;
}

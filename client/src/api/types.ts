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

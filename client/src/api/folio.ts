import { api } from "./client";
import type {
  CreatePageInput,
  MovePageInput,
  PageDetail,
  PageTreeNode,
  UpdatePageInput,
  WorkspaceSummary,
} from "./types";

export const listWorkspaces = (signal?: AbortSignal) =>
  api.get<WorkspaceSummary[]>("/api/workspaces", signal);

export const getWorkspace = (workspaceId: string, signal?: AbortSignal) =>
  api.get<WorkspaceSummary>(`/api/workspaces/${workspaceId}`, signal);

export const getPageTree = (workspaceId: string, signal?: AbortSignal) =>
  api.get<PageTreeNode[]>(`/api/workspaces/${workspaceId}/pages/tree`, signal);

export const getPage = (pageId: string, signal?: AbortSignal) =>
  api.get<PageDetail>(`/api/pages/${pageId}`, signal);

export const createPage = (workspaceId: string, input: CreatePageInput) =>
  api.post<PageDetail>(`/api/workspaces/${workspaceId}/pages`, input);

export const renamePage = (pageId: string, input: UpdatePageInput) =>
  api.put<PageDetail>(`/api/pages/${pageId}`, input);

export const movePage = (pageId: string, input: MovePageInput) =>
  api.post<PageDetail>(`/api/pages/${pageId}/move`, input);

export const deletePage = (pageId: string) => api.delete<void>(`/api/pages/${pageId}`);

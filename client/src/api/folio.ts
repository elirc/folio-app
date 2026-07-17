import { api } from "./client";
import type {
  Block,
  CreateBlockInput,
  CreatePageInput,
  Favorite,
  LoginInput,
  LoginResponse,
  Member,
  MovePageInput,
  PageDetail,
  PageTreeNode,
  PageVisibility,
  SearchResult,
  SharePermission,
  ShareSettings,
  TrashItem,
  UpdateBlockInput,
  UpdatePageInput,
  WorkspaceSummary,
} from "./types";

// ---- auth ----

export const login = (input: LoginInput) => api.post<LoginResponse>("/api/auth/login", input);

export const getMe = (signal?: AbortSignal) => api.get<Member>("/api/auth/me", signal);

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

// ---- blocks ----

export const getBlocks = (pageId: string, signal?: AbortSignal) =>
  api.get<Block[]>(`/api/pages/${pageId}/blocks`, signal);

export const createBlock = (pageId: string, input: CreateBlockInput) =>
  api.post<Block>(`/api/pages/${pageId}/blocks`, input);

export const updateBlock = (blockId: string, input: UpdateBlockInput) =>
  api.put<Block>(`/api/blocks/${blockId}`, input);

export const moveBlock = (blockId: string, position: number) =>
  api.post<Block>(`/api/blocks/${blockId}/move`, { position });

export const deleteBlock = (blockId: string) => api.delete<void>(`/api/blocks/${blockId}`);

// ---- sharing, search, favorites, trash ----

export const searchPages = (workspaceId: string, query: string, signal?: AbortSignal) =>
  api.get<SearchResult[]>(
    `/api/workspaces/${workspaceId}/search?q=${encodeURIComponent(query)}`,
    signal,
  );

export const setShare = (pageId: string, visibility: PageVisibility, permission: SharePermission) =>
  api.put<ShareSettings>(`/api/pages/${pageId}/share`, { visibility, permission });

export const favoritePage = (pageId: string) => api.post<PageDetail>(`/api/pages/${pageId}/favorite`);

export const unfavoritePage = (pageId: string) =>
  api.delete<PageDetail>(`/api/pages/${pageId}/favorite`);

export const getFavorites = (workspaceId: string, signal?: AbortSignal) =>
  api.get<Favorite[]>(`/api/workspaces/${workspaceId}/favorites`, signal);

export const getTrash = (workspaceId: string, signal?: AbortSignal) =>
  api.get<TrashItem[]>(`/api/workspaces/${workspaceId}/trash`, signal);

export const restorePage = (pageId: string) => api.post<PageDetail>(`/api/pages/${pageId}/restore`);

import { api } from "./client";
import type {
  ActivityItem,
  Backlink,
  Block,
  Comment,
  CreateBlockInput,
  CreateCommentInput,
  CreatePageInput,
  CreateTemplateInput,
  ExportResult,
  Favorite,
  OutgoingLink,
  LoginInput,
  LoginResponse,
  Member,
  MoveBlockInput,
  MovePageInput,
  Notification,
  PageDetail,
  PageTreeNode,
  PageVisibility,
  SearchResult,
  SharePermission,
  ShareSettings,
  TrashItem,
  Template,
  UpdateBlockInput,
  UpdatePageInput,
  VersionDetail,
  VersionSummary,
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

export const moveBlock = (blockId: string, input: MoveBlockInput) =>
  api.post<Block>(`/api/blocks/${blockId}/move`, input);

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

// ---- page history ----

export const getVersions = (pageId: string, signal?: AbortSignal) =>
  api.get<VersionSummary[]>(`/api/pages/${pageId}/versions`, signal);

export const getVersion = (pageId: string, versionNumber: number, signal?: AbortSignal) =>
  api.get<VersionDetail>(`/api/pages/${pageId}/versions/${versionNumber}`, signal);

export const saveVersion = (pageId: string) =>
  api.post<VersionSummary>(`/api/pages/${pageId}/versions`);

export const restoreVersion = (pageId: string, versionNumber: number) =>
  api.post<VersionSummary>(`/api/pages/${pageId}/versions/${versionNumber}/restore`);

// ---- workspace members ----

export const getMembers = (workspaceId: string, signal?: AbortSignal) =>
  api.get<Member[]>(`/api/workspaces/${workspaceId}/members`, signal);

// ---- comments & mentions ----

export const getComments = (pageId: string, signal?: AbortSignal) =>
  api.get<Comment[]>(`/api/pages/${pageId}/comments`, signal);

export const createComment = (pageId: string, input: CreateCommentInput) =>
  api.post<Comment>(`/api/pages/${pageId}/comments`, input);

export const resolveComment = (commentId: string) =>
  api.post<Comment>(`/api/comments/${commentId}/resolve`);

export const unresolveComment = (commentId: string) =>
  api.post<Comment>(`/api/comments/${commentId}/unresolve`);

export const deleteComment = (commentId: string) => api.delete<void>(`/api/comments/${commentId}`);

// ---- links & backlinks ----

export const getBacklinks = (pageId: string, signal?: AbortSignal) =>
  api.get<Backlink[]>(`/api/pages/${pageId}/backlinks`, signal);

export const getOutgoingLinks = (pageId: string, signal?: AbortSignal) =>
  api.get<OutgoingLink[]>(`/api/pages/${pageId}/links`, signal);

// ---- templates, duplicate & export ----

export const getTemplates = (workspaceId: string, signal?: AbortSignal) =>
  api.get<Template[]>(`/api/workspaces/${workspaceId}/templates`, signal);

export const createTemplate = (pageId: string, input: CreateTemplateInput) =>
  api.post<Template>(`/api/pages/${pageId}/templates`, input);

export const instantiateTemplate = (workspaceId: string, templateId: string, parentId?: string | null) =>
  api.post<PageDetail>(`/api/workspaces/${workspaceId}/templates/${templateId}/instantiate`, { parentId: parentId ?? null });

export const deleteTemplate = (workspaceId: string, templateId: string) =>
  api.delete<void>(`/api/workspaces/${workspaceId}/templates/${templateId}`);

export const duplicatePage = (pageId: string) => api.post<PageDetail>(`/api/pages/${pageId}/duplicate`);

export const exportPage = (pageId: string, subtree = false, signal?: AbortSignal) =>
  api.get<ExportResult>(`/api/pages/${pageId}/export?subtree=${subtree}`, signal);

// ---- notifications & activity ----

export const getNotifications = (signal?: AbortSignal) =>
  api.get<Notification[]>("/api/notifications", signal);

export const markNotificationRead = (notificationId: string) =>
  api.post<void>(`/api/notifications/${notificationId}/read`);

export const markAllNotificationsRead = () =>
  api.post<{ count: number }>("/api/notifications/read-all");

export const getActivity = (workspaceId: string, signal?: AbortSignal) =>
  api.get<ActivityItem[]>(`/api/workspaces/${workspaceId}/activity`, signal);

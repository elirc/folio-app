import { useState } from "react";
import type { Comment, Member } from "../api/types";
import {
  createComment,
  deleteComment,
  getComments,
  getMembers,
  resolveComment,
  unresolveComment,
} from "../api/folio";
import { useAsync } from "../hooks/useAsync";

interface CommentSidebarProps {
  pageId: string;
  workspaceId: string;
  onClose: () => void;
}

/** Replaces @[Name](id) mention tokens with a readable @Name for display. */
export function renderCommentBody(body: string): string {
  return body.replace(/@\[([^\]]+)\]\([^)]+\)/g, "@$1");
}

export function CommentSidebar({ pageId, workspaceId, onClose }: CommentSidebarProps) {
  const { data, error, loading, reload } = useAsync<Comment[]>(
    (signal) => getComments(pageId, signal),
    [pageId],
  );
  const members = useAsync<Member[]>((signal) => getMembers(workspaceId, signal), [workspaceId]);

  const roots = (data ?? []).filter((c) => c.parentCommentId === null);
  const repliesOf = (id: string) => (data ?? []).filter((c) => c.parentCommentId === id);
  const pageThreads = roots.filter((c) => c.blockId === null);
  const blockThreads = roots.filter((c) => c.blockId !== null);

  return (
    <aside className="comment-sidebar" aria-label="Comments">
      <div className="comment-header">
        <span className="sidebar-title">Comments</span>
        <button type="button" className="icon-btn" aria-label="Close comments" onClick={onClose}>
          ×
        </button>
      </div>

      <CommentComposer
        members={members.data ?? []}
        placeholder="Comment on this page…"
        onSubmit={async (body) => {
          await createComment(pageId, { body });
          reload();
        }}
      />

      {loading && <p className="muted">Loading comments…</p>}
      {error && <p className="error-text">Could not load comments.</p>}

      {data && pageThreads.length > 0 && (
        <section className="comment-group">
          <h4 className="comment-group-title">Page</h4>
          {pageThreads.map((c) => (
            <CommentThread
              key={c.id}
              comment={c}
              replies={repliesOf(c.id)}
              members={members.data ?? []}
              pageId={pageId}
              onChanged={reload}
            />
          ))}
        </section>
      )}

      {data && blockThreads.length > 0 && (
        <section className="comment-group">
          <h4 className="comment-group-title">On blocks</h4>
          {blockThreads.map((c) => (
            <CommentThread
              key={c.id}
              comment={c}
              replies={repliesOf(c.id)}
              members={members.data ?? []}
              pageId={pageId}
              onChanged={reload}
            />
          ))}
        </section>
      )}

      {data && roots.length === 0 && <p className="muted">No comments yet.</p>}
    </aside>
  );
}

interface CommentThreadProps {
  comment: Comment;
  replies: Comment[];
  members: Member[];
  pageId: string;
  onChanged: () => void;
}

function CommentThread({ comment, replies, members, pageId, onChanged }: CommentThreadProps) {
  const [replying, setReplying] = useState(false);

  async function toggleResolved() {
    if (comment.isResolved) {
      await unresolveComment(comment.id);
    } else {
      await resolveComment(comment.id);
    }
    onChanged();
  }

  async function remove() {
    await deleteComment(comment.id);
    onChanged();
  }

  return (
    <div className={`comment-thread${comment.isResolved ? " resolved" : ""}`}>
      <CommentBubble comment={comment} />
      <div className="comment-actions">
        <button type="button" className="link-btn" onClick={toggleResolved}>
          {comment.isResolved ? "Reopen" : "Resolve"}
        </button>
        <button type="button" className="link-btn" onClick={() => setReplying((v) => !v)}>
          Reply
        </button>
        <button type="button" className="link-btn danger" onClick={remove}>
          Delete
        </button>
      </div>

      {replies.map((r) => (
        <div key={r.id} className="comment-reply">
          <CommentBubble comment={r} />
        </div>
      ))}

      {replying && (
        <CommentComposer
          members={members}
          placeholder="Reply…"
          onSubmit={async (body) => {
            await createComment(pageId, { body, parentCommentId: comment.id });
            setReplying(false);
            onChanged();
          }}
        />
      )}
    </div>
  );
}

function CommentBubble({ comment }: { comment: Comment }) {
  return (
    <div className="comment-bubble">
      <div className="comment-meta">
        <span className="comment-author">{comment.authorName}</span>
        {comment.isResolved && <span className="comment-resolved-badge">Resolved</span>}
      </div>
      <p className="comment-body">{renderCommentBody(comment.body)}</p>
    </div>
  );
}

interface CommentComposerProps {
  members: Member[];
  placeholder: string;
  onSubmit: (body: string) => Promise<void>;
}

function CommentComposer({ members, placeholder, onSubmit }: CommentComposerProps) {
  const [body, setBody] = useState("");
  const [busy, setBusy] = useState(false);

  function mention(member: Member) {
    setBody((current) => `${current}@[${member.name}](${member.id}) `);
  }

  async function submit() {
    const trimmed = body.trim();
    if (trimmed.length === 0) {
      return;
    }
    setBusy(true);
    try {
      await onSubmit(trimmed);
      setBody("");
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="comment-composer">
      <textarea
        className="comment-input"
        aria-label={placeholder}
        placeholder={placeholder}
        rows={2}
        value={body}
        onChange={(e) => setBody(e.target.value)}
      />
      <div className="comment-composer-actions">
        <select
          aria-label="Mention a member"
          className="mention-select"
          value=""
          onChange={(e) => {
            const member = members.find((m) => m.id === e.target.value);
            if (member) {
              mention(member);
            }
          }}
        >
          <option value="">Mention…</option>
          {members.map((m) => (
            <option key={m.id} value={m.id}>
              {m.name}
            </option>
          ))}
        </select>
        <button type="button" className="add-block-btn" onClick={submit} disabled={busy}>
          Comment
        </button>
      </div>
    </div>
  );
}

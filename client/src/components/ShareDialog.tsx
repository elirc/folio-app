import { useState } from "react";
import type { PageDetail, PageVisibility, SharePermission } from "../api/types";
import { setShare } from "../api/folio";

interface ShareDialogProps {
  page: PageDetail;
  onChanged: () => void;
  onClose: () => void;
}

export function ShareDialog({ page, onChanged, onClose }: ShareDialogProps) {
  const [visibility, setVisibility] = useState<PageVisibility>(page.visibility);
  const [permission, setPermission] = useState<SharePermission>(page.permission);
  const [slug, setSlug] = useState<string | null>(page.publicSlug);

  async function apply(nextVisibility: PageVisibility, nextPermission: SharePermission) {
    const result = await setShare(page.id, nextVisibility, nextPermission);
    setSlug(result.publicSlug);
    onChanged();
  }

  return (
    <div className="share-dialog" role="dialog" aria-label="Share page">
      <label className="share-field">
        <span>Visibility</span>
        <select
          aria-label="Visibility"
          value={visibility}
          onChange={(e) => {
            const next = e.target.value as PageVisibility;
            setVisibility(next);
            void apply(next, permission);
          }}
        >
          <option value="Private">Private</option>
          <option value="Workspace">Workspace</option>
          <option value="Public">Public link</option>
        </select>
      </label>

      <label className="share-field">
        <span>Access</span>
        <select
          aria-label="Access level"
          value={permission}
          onChange={(e) => {
            const next = e.target.value as SharePermission;
            setPermission(next);
            void apply(visibility, next);
          }}
        >
          <option value="View">Can view</option>
          <option value="Edit">Can edit</option>
        </select>
      </label>

      {visibility === "Public" && slug && (
        <p className="public-link">
          Public link: <code>/api/public/pages/{slug}</code>
        </p>
      )}

      <button type="button" className="add-block-btn" onClick={onClose}>
        Done
      </button>
    </div>
  );
}

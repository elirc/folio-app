import { useNavigate } from "react-router-dom";
import type { Template } from "../api/types";
import { deleteTemplate, getTemplates, instantiateTemplate } from "../api/folio";
import { useAsync } from "../hooks/useAsync";

interface TemplateGalleryProps {
  workspaceId: string;
  onChanged: () => void;
}

export function TemplateGallery({ workspaceId, onChanged }: TemplateGalleryProps) {
  const { data, error, loading, reload } = useAsync<Template[]>(
    (signal) => getTemplates(workspaceId, signal),
    [workspaceId],
  );
  const navigate = useNavigate();

  async function use(template: Template) {
    const page = await instantiateTemplate(workspaceId, template.id);
    onChanged();
    navigate(`/w/${workspaceId}/p/${page.id}`);
  }

  async function remove(templateId: string) {
    await deleteTemplate(workspaceId, templateId);
    reload();
  }

  if (loading) {
    return <p className="muted">Loading templates…</p>;
  }
  if (error || !data) {
    return <p className="error-text">Could not load templates.</p>;
  }

  return (
    <section className="template-gallery">
      <h2>Templates</h2>
      {data.length === 0 ? (
        <p className="muted">
          No templates yet. Open a page and choose <strong>Save as template</strong>.
        </p>
      ) : (
        <ul className="template-list">
          {data.map((template) => (
            <li key={template.id} className="template-card">
              <div className="template-info">
                <span className="template-name">
                  {template.sourceIcon ?? "📄"} {template.name}
                </span>
                {template.description && <span className="muted">{template.description}</span>}
                <span className="muted template-meta">
                  {template.blockCount} blocks
                  {template.createdByName ? ` · by ${template.createdByName}` : ""}
                </span>
              </div>
              <div className="template-actions">
                <button type="button" className="add-block-btn" onClick={() => use(template)}>
                  Use template
                </button>
                <button
                  type="button"
                  className="icon-btn danger"
                  aria-label={`Delete template ${template.name}`}
                  onClick={() => remove(template.id)}
                >
                  ×
                </button>
              </div>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}

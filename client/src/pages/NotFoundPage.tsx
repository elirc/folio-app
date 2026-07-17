import { Link } from "react-router-dom";

export function NotFoundPage() {
  return (
    <section className="page">
      <h1>Not found</h1>
      <p>That page doesn&apos;t exist.</p>
      <Link to="/">Back home</Link>
    </section>
  );
}

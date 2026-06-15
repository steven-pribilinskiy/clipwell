import Link from 'next/link';
import { ThemedShot } from '@/components/themed-shot';

const features = [
  {
    title: 'Frame-fast picker',
    body: 'A pre-warmed window appears in about one display frame (~13–16 ms measured) on a global hotkey. No cold start, no lag.',
    icon: '⚡',
  },
  {
    title: 'Queryable by anything',
    body: 'REST for one-shot calls, WebSocket/SSE to stream changes live, MCP so AI agents can read and act.',
    icon: '🔌',
  },
  {
    title: 'Cross-platform',
    body: 'Windows, macOS, and Linux behind one clipboard-watcher interface. One daemon, one API.',
    icon: '🖥️',
  },
  {
    title: 'Typed & extensible',
    body: 'Items are classified — url, email, color, path, code, image — by detectors that load as plugins.',
    icon: '🏷️',
  },
];

const protocols = [
  { name: 'REST', desc: 'GET /api/clipboard, settings, image, delete, clear. OpenAPI spec included.' },
  { name: 'WebSocket / SSE', desc: 'A clipboard.changed event on every capture, pushed live to subscribers.' },
  { name: 'MCP', desc: 'A stdio server with clipboard_recent / search / get_text / clear for Claude.' },
];

export default function HomePage() {
  return (
    <main className="flex flex-1 flex-col">
      {/* Hero */}
      <section className="relative flex flex-col items-center gap-6 px-6 pt-24 pb-16 text-center">
        <div
          className="pointer-events-none absolute inset-0 -z-10 opacity-60"
          style={{
            background:
              'radial-gradient(60% 50% at 50% 0%, color-mix(in oklab, var(--color-fd-primary) 22%, transparent), transparent)',
          }}
        />
        <span className="rounded-full border border-fd-border bg-fd-card px-3 py-1 text-sm text-fd-muted-foreground">
          Open source · MIT · .NET 10 + Avalonia
        </span>
        <h1 className="max-w-3xl text-balance text-5xl font-bold tracking-tight sm:text-6xl">
          Your clipboard,
          <br />
          as an API.
        </h1>
        <p className="max-w-2xl text-balance text-lg text-fd-muted-foreground">
          Clipwell is a fast, cross-platform clipboard history built as a headless daemon
          with a thin picker on top. The picker, the CLI, editor extensions, and AI agents
          are all clients of the same public API.
        </p>
        <div className="flex flex-wrap items-center justify-center gap-3">
          <Link
            href="/docs"
            className="rounded-lg bg-fd-primary px-5 py-2.5 font-medium text-fd-primary-foreground transition-opacity hover:opacity-90"
          >
            Read the docs
          </Link>
          <a
            href="https://github.com/steven-pribilinskiy/clipwell"
            className="rounded-lg border border-fd-border bg-fd-card px-5 py-2.5 font-medium transition-colors hover:bg-fd-accent"
          >
            View on GitHub
          </a>
        </div>
        <pre className="mt-4 w-full max-w-xl overflow-x-auto rounded-xl border border-fd-border bg-fd-card p-4 text-left text-sm">
          <code>{`dotnet run --project daemon   # clipboard API on :8787
dotnet run --project ui       # press Alt+Shift+V to summon`}</code>
        </pre>
        <div className="mt-8 w-full max-w-2xl">
          <ThemedShot
            name="picker"
            alt="The Clipwell picker showing typed clipboard items, filter tabs, and an image thumbnail"
            width={862}
            height={1016}
          />
        </div>
      </section>

      {/* Features */}
      <section className="mx-auto grid w-full max-w-5xl grid-cols-1 gap-4 px-6 py-8 sm:grid-cols-2">
        {features.map((f) => (
          <div key={f.title} className="rounded-xl border border-fd-border bg-fd-card p-6">
            <div className="mb-3 text-2xl">{f.icon}</div>
            <h3 className="mb-1.5 text-lg font-semibold">{f.title}</h3>
            <p className="text-sm text-fd-muted-foreground">{f.body}</p>
          </div>
        ))}
      </section>

      {/* Protocols */}
      <section className="mx-auto w-full max-w-5xl px-6 py-12">
        <h2 className="mb-2 text-center text-3xl font-bold">One history, three protocols</h2>
        <p className="mx-auto mb-8 max-w-2xl text-center text-fd-muted-foreground">
          The daemon owns the clipboard and a SQLite history, and exposes it three ways — so
          anything on your machine can read it, listen to it, and drive it.
        </p>
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
          {protocols.map((p) => (
            <div key={p.name} className="rounded-xl border border-fd-border bg-fd-card p-6">
              <h3 className="mb-2 font-mono text-sm font-semibold text-fd-primary">{p.name}</h3>
              <p className="text-sm text-fd-muted-foreground">{p.desc}</p>
            </div>
          ))}
        </div>
      </section>

      {/* CTA */}
      <section className="mx-auto mb-24 w-full max-w-5xl px-6">
        <div className="flex flex-col items-center gap-4 rounded-2xl border border-fd-border bg-fd-card p-10 text-center">
          <h2 className="text-2xl font-bold">Built in the open</h2>
          <p className="max-w-xl text-fd-muted-foreground">
            Read how it's built — architecture, decisions, and the technology behind the
            single-digit-millisecond picker.
          </p>
          <div className="flex flex-wrap justify-center gap-3">
            <Link href="/docs/install" className="rounded-lg bg-fd-primary px-5 py-2.5 font-medium text-fd-primary-foreground transition-opacity hover:opacity-90">
              Install Clipwell
            </Link>
            <a href="/clipwell/engineering" className="rounded-lg border border-fd-border bg-fd-card px-5 py-2.5 font-medium transition-colors hover:bg-fd-accent">
              Engineering docs
            </a>
          </div>
        </div>
      </section>
    </main>
  );
}

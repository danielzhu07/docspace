import { useEffect, useMemo, useState } from "react";

const API_BASE = "http://localhost:5280";

type DocListItem = {
  id: string;
  fileName: string;
  uploadedAt: string;
  charCount: number;
};

type DocDetail = {
  id: string;
  fileName: string;
  uploadedAt: string;
  content: string;
};

type SearchResult = {
  id: string;
  fileName: string;
  uploadedAt: string;
  score: number;
  snippet: string;
};

function highlight(text: string, query: string) {
  if (!query) return text;
  const parts = text.split(new RegExp(`(${query})`, "gi"));

  return (
    <>
      {parts.map((part, i) =>
        part.toLowerCase() === query.toLowerCase() ? (
          <mark key={i} className="bg-yellow-200 text-black rounded px-1">
            {part}
          </mark>
        ) : (
          <span key={i}>{part}</span>
        )
      )}
    </>
  );
}

export default function App() {
  // Paste text
  const [pasteName, setPasteName] = useState("pasted.txt");
  const [pasteText, setPasteText] = useState("");
  const [pasteMsg, setPasteMsg] = useState("");
  const [pasting, setPasting] = useState(false);

  // Upload
  const [file, setFile] = useState<File | null>(null);
  const [uploadMsg, setUploadMsg] = useState("");
  const [uploading, setUploading] = useState(false);

  // Docs list
  const [docs, setDocs] = useState<DocListItem[]>([]);
  const [docsLoading, setDocsLoading] = useState(false);
  const [docsMsg, setDocsMsg] = useState("");

  // Viewer modal
  const [openDocId, setOpenDocId] = useState<string | null>(null);
  const [openDoc, setOpenDoc] = useState<DocDetail | null>(null);
  const [docLoading, setDocLoading] = useState(false);
  const [docMsg, setDocMsg] = useState("");

  // Search
  const [q, setQ] = useState("");
  const [searchMode, setSearchMode] = useState<"keyword" | "semantic">("keyword");
  const [results, setResults] = useState<SearchResult[]>([]);
  const [searchMsg, setSearchMsg] = useState("");
  const [searching, setSearching] = useState(false);

  const canUpload = useMemo(() => !!file && !uploading, [file, uploading]);

  async function loadDocs() {
    setDocsLoading(true);
    setDocsMsg("");
    try {
      const res = await fetch(`${API_BASE}/documents`);
      if (!res.ok) throw new Error(await res.text());
      setDocs(await res.json());
    } catch (e: any) {
      setDocsMsg(`Failed to load documents: ${e.message}`);
    } finally {
      setDocsLoading(false);
    }
  }

  async function loadDoc(id: string) {
    setDocLoading(true);
    setDocMsg("");
    try {
      const res = await fetch(`${API_BASE}/documents/${id}`);
      if (!res.ok) throw new Error(await res.text());
      setOpenDoc(await res.json());
    } catch (e: any) {
      setDocMsg(`Failed to load document: ${e.message}`);
    } finally {
      setDocLoading(false);
    }
  }

  useEffect(() => {
    loadDocs();
  }, []);

  useEffect(() => {
    if (openDocId) loadDoc(openDocId);
  }, [openDocId]);

  async function savePasted() {
    const name = pasteName.trim() || "pasted.txt";
    const text = pasteText.trim();

    if (!text) {
      setPasteMsg("Paste some text first.");
      return;
    }

    setPasting(true);
    setPasteMsg("Saving...");
    try {
      const res = await fetch(`${API_BASE}/documents`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ fileName: name, content: text }),
      });

      if (!res.ok) throw new Error(await res.text());

      const data = await res.json();
      setPasteMsg(`Saved ${data.fileName}`);
      setPasteText("");
      await loadDocs();
    } catch (e: any) {
      setPasteMsg(`Save failed: ${e.message}`);
    } finally {
      setPasting(false);
    }
  }

  async function upload() {
    if (!file) return;
    setUploading(true);
    setUploadMsg("Uploading...");
    try {
      const form = new FormData();
      form.append("file", file);

      const res = await fetch(`${API_BASE}/documents/upload`, {
        method: "POST",
        body: form,
      });

      if (!res.ok) throw new Error(await res.text());

      const data = await res.json();
      setUploadMsg(`Uploaded ${data.fileName}`);
      setFile(null);
      await loadDocs();
    } catch (e: any) {
      setUploadMsg(`Upload failed: ${e.message}`);
    } finally {
      setUploading(false);
    }
  }

  async function search() {
    const query = q.trim();
    if (!query) return;

    setSearching(true);
    setSearchMsg("Searching...");

    try {
      const endpoint =
        searchMode === "semantic"
          ? `${API_BASE}/search/semantic?q=${encodeURIComponent(query)}`
          : `${API_BASE}/search?q=${encodeURIComponent(query)}`;

      const res = await fetch(endpoint);
      if (!res.ok) throw new Error(await res.text());

      const data = await res.json();
      setResults(data.results || []);
      setSearchMsg(`(${searchMode}) Found ${data.count} result(s).`);
    } catch (e: any) {
      setSearchMsg(`Search failed: ${e.message}`);
    } finally {
      setSearching(false);
    }
  }

  function closeModal() {
    setOpenDocId(null);
    setOpenDoc(null);
    setDocMsg("");
  }

  async function deleteDoc(id: string, fileName?: string) {
    const name = fileName ?? "this document";
    if (!confirm(`Delete "${name}"? This cannot be undone.`)) return;

    const res = await fetch(`${API_BASE}/documents/${id}`, { method: "DELETE" });
    if (!res.ok) {
      alert(`Delete failed: ${await res.text()}`);
      return;
    }

    closeModal();
    await loadDocs();
  }

  return (
    <div className="min-h-screen bg-neutral-950 text-neutral-100">
      <div className="mx-auto max-w-5xl px-6 py-10">
        <header className="mb-8">
          <h1 className="text-4xl font-semibold">DocSpace</h1>
          <p className="mt-2 text-neutral-400">
            Upload documents or paste text, then search by keywords or meaning.
          </p>
        </header>

        <div className="grid gap-6 lg:grid-cols-3">
          {/* LEFT */}
          <div className="lg:col-span-2 space-y-6">
            {/* Upload */}
            <div className="rounded-2xl border border-neutral-800 bg-neutral-900/40 p-5">
              <h2 className="text-lg font-semibold">Upload</h2>
              <div className="mt-4 flex gap-3">
                <input
                  type="file"
                  accept=".txt,.md"
                  onChange={(e) => setFile(e.target.files?.[0] ?? null)}
                />
                <button
                  onClick={upload}
                  disabled={!canUpload}
                  className="rounded-xl bg-white px-4 py-2 text-sm font-semibold text-black disabled:opacity-40"
                >
                  {uploading ? "Uploading..." : "Upload"}
                </button>
              </div>
              {uploadMsg && <div className="mt-3 text-sm">{uploadMsg}</div>}
            </div>

            {/* Paste text */}
            <div className="rounded-2xl border border-neutral-800 bg-neutral-900/40 p-5">
              <h2 className="text-lg font-semibold">Paste text</h2>
              <p className="mt-2 text-neutral-400">
                Create a document by pasting content directly.
              </p>

              <div className="mt-4 flex gap-3">
                <input
                  value={pasteName}
                  onChange={(e) => setPasteName(e.target.value)}
                  className="w-full rounded-xl bg-neutral-950 border border-neutral-800 px-4 py-2"
                  placeholder="filename (e.g. notes.txt)"
                />
                <button
                  onClick={savePasted}
                  disabled={pasting}
                  className="rounded-xl bg-white px-4 py-2 text-sm font-semibold text-black disabled:opacity-40"
                >
                  {pasting ? "Saving..." : "Save"}
                </button>
              </div>

              <textarea
                value={pasteText}
                onChange={(e) => setPasteText(e.target.value)}
                className="mt-3 w-full min-h-[140px] rounded-xl bg-neutral-950 border border-neutral-800 px-4 py-2"
                placeholder="Paste your text here..."
              />

              {pasteMsg && <div className="mt-3 text-sm">{pasteMsg}</div>}
            </div>

            {/* Search */}
            <div className="rounded-2xl border border-neutral-800 bg-neutral-900/40 p-5">
              <h2 className="text-lg font-semibold">Search</h2>

              {/* TOGGLE */}
              <div className="mt-3 inline-flex rounded-xl border border-neutral-800 bg-neutral-950 p-1">
                <button
                  onClick={() => setSearchMode("keyword")}
                  className={`px-3 py-1.5 text-sm rounded-lg ${
                    searchMode === "keyword" ? "bg-neutral-800" : ""
                  }`}
                >
                  Keyword
                </button>
                <button
                  onClick={() => setSearchMode("semantic")}
                  className={`px-3 py-1.5 text-sm rounded-lg ${
                    searchMode === "semantic" ? "bg-neutral-800" : ""
                  }`}
                >
                  Semantic
                </button>
              </div>

              <div className="mt-4 flex gap-3">
                <input
                  value={q}
                  onChange={(e) => setQ(e.target.value)}
                  onKeyDown={(e) => e.key === "Enter" && search()}
                  className="w-full rounded-xl bg-neutral-950 border border-neutral-800 px-4 py-2"
                />
                <button
                  onClick={search}
                  disabled={searching}
                  className="rounded-xl bg-neutral-800 px-4 py-2 disabled:opacity-50"
                >
                  {searching ? "Searching..." : "Search"}
                </button>
              </div>

              {searchMsg && <div className="mt-3 text-sm">{searchMsg}</div>}

              <div className="mt-4 divide-y divide-neutral-800">
                {results.map((r) => (
                  <button
                    key={r.id}
                    onClick={() => setOpenDocId(r.id)}
                    className="w-full text-left py-4"
                  >
                    <div className="font-semibold">{r.fileName}</div>
                    <div className="text-xs text-neutral-400">
                      score {r.score.toFixed(3)}
                    </div>
                    <div className="mt-2 text-sm">
                      {searchMode === "keyword" ? highlight(r.snippet, q) : r.snippet}
                    </div>
                  </button>
                ))}
              </div>
            </div>
          </div>

          {/* RIGHT */}
          <div className="rounded-2xl border border-neutral-800 bg-neutral-900/40 p-5">
            <div className="flex items-center justify-between">
              <h2 className="text-lg font-semibold">My Documents</h2>
              <button
                onClick={loadDocs}
                disabled={docsLoading}
                className="rounded-xl bg-neutral-800 px-3 py-2 text-sm disabled:opacity-50"
              >
                {docsLoading ? "Loading..." : "Refresh"}
              </button>
            </div>

            {docsMsg && <div className="mt-3 text-sm text-red-300">{docsMsg}</div>}

            <div className="mt-4 space-y-3">
              {docs.map((d) => (
                <button
                  key={d.id}
                  onClick={() => setOpenDocId(d.id)}
                  className="w-full text-left"
                >
                  <div className="font-semibold">{d.fileName}</div>
                  <div className="text-xs text-neutral-400">{d.charCount} chars</div>
                </button>
              ))}
            </div>
          </div>
        </div>

        {/* MODAL */}
        {openDocId && (
          <div
            className="fixed inset-0 bg-black/60 flex items-center justify-center p-6"
            onClick={closeModal}
          >
            <div
              className="w-full max-w-3xl max-h-[85vh] bg-neutral-950 rounded-2xl flex flex-col"
              onClick={(e) => e.stopPropagation()}
            >
              <div className="p-4 border-b border-neutral-800 flex items-center justify-between gap-3">
                <div className="font-semibold truncate">{openDoc?.fileName}</div>

                <div className="flex items-center gap-2">
                  <button
                    onClick={() => openDocId && deleteDoc(openDocId, openDoc?.fileName)}
                    className="rounded-lg bg-red-600/80 hover:bg-red-600 px-3 py-2 text-sm font-semibold text-white disabled:opacity-40"
                    disabled={!openDocId}
                  >
                    Delete
                  </button>

                  <button
                    onClick={closeModal}
                    className="rounded-lg bg-neutral-800 hover:bg-neutral-700 px-3 py-2 text-sm font-semibold"
                  >
                    Close
                  </button>
                </div>
              </div>

              <div className="p-4 overflow-auto flex-1">
                {docLoading ? (
                  <div className="text-sm text-neutral-400">Loading...</div>
                ) : docMsg ? (
                  <div className="text-sm text-red-300">{docMsg}</div>
                ) : (
                  <pre className="whitespace-pre-wrap break-words text-sm">
                    {openDoc?.content}
                  </pre>
                )}
              </div>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}

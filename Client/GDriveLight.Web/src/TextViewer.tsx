import { useEffect, useRef, useState } from "react";
import styles from "./TextViewer.module.css";
import { Copy, Check } from "lucide-react";

// Lazy-loaded highlighter instance (shared across all renders).
let hljsPromise: Promise<typeof import("highlight.js").default> | null = null;
function getHljs() {
  if (!hljsPromise) {
    hljsPromise = import("highlight.js/lib/core").then(async (mod) => {
      const hljs = mod.default;
      // Load only the grammars we actually use to keep the chunk small.
      const [
        bash,
        c,
        cpp,
        csharp,
        css,
        go,
        java,
        javascript,
        json,
        kotlin,
        markdown,
        plaintext,
        python,
        rust,
        sql,
        typescript,
        xml,
        yaml,
      ] = await Promise.all([
        import("highlight.js/lib/languages/bash"),
        import("highlight.js/lib/languages/c"),
        import("highlight.js/lib/languages/cpp"),
        import("highlight.js/lib/languages/csharp"),
        import("highlight.js/lib/languages/css"),
        import("highlight.js/lib/languages/go"),
        import("highlight.js/lib/languages/java"),
        import("highlight.js/lib/languages/javascript"),
        import("highlight.js/lib/languages/json"),
        import("highlight.js/lib/languages/kotlin"),
        import("highlight.js/lib/languages/markdown"),
        import("highlight.js/lib/languages/plaintext"),
        import("highlight.js/lib/languages/python"),
        import("highlight.js/lib/languages/rust"),
        import("highlight.js/lib/languages/sql"),
        import("highlight.js/lib/languages/typescript"),
        import("highlight.js/lib/languages/xml"),
        import("highlight.js/lib/languages/yaml"),
      ]);
      hljs.registerLanguage("bash", bash.default);
      hljs.registerLanguage("c", c.default);
      hljs.registerLanguage("cpp", cpp.default);
      hljs.registerLanguage("csharp", csharp.default);
      hljs.registerLanguage("css", css.default);
      hljs.registerLanguage("go", go.default);
      hljs.registerLanguage("java", java.default);
      hljs.registerLanguage("javascript", javascript.default);
      hljs.registerLanguage("json", json.default);
      hljs.registerLanguage("kotlin", kotlin.default);
      hljs.registerLanguage("markdown", markdown.default);
      hljs.registerLanguage("plaintext", plaintext.default);
      hljs.registerLanguage("python", python.default);
      hljs.registerLanguage("rust", rust.default);
      hljs.registerLanguage("sql", sql.default);
      hljs.registerLanguage("typescript", typescript.default);
      hljs.registerLanguage("xml", xml.default);
      hljs.registerLanguage("yaml", yaml.default);
      return hljs;
    });
  }
  return hljsPromise;
}

/**
 * Maps MIME types served by the backend to highlight.js language identifiers.
 * The backend normalises all text/* types to text/plain for security, so we
 * use the original mimeType from the file-type catalog (passed as a prop).
 */
function mimeToLanguage(mimeType: string): string | null {
  const m = mimeType.split(";")[0].trim().toLowerCase();
  const map: Record<string, string> = {
    "text/x-csharp": "csharp",
    "text/x-c": "c",
    "text/x-c++": "cpp",
    "text/x-c++-src": "cpp",
    "text/x-java": "java",
    "text/x-kotlin": "kotlin",
    "text/x-python": "python",
    "text/x-go": "go",
    "text/x-rust": "rust",
    "text/javascript": "javascript",
    "application/javascript": "javascript",
    "application/x-javascript": "javascript",
    "text/typescript": "typescript",
    "application/typescript": "typescript",
    "text/css": "css",
    "text/html": "xml",
    "application/xhtml+xml": "xml",
    "text/xml": "xml",
    "application/xml": "xml",
    "application/json": "json",
    "text/markdown": "markdown",
    "text/x-markdown": "markdown",
    "text/x-sh": "bash",
    "application/x-sh": "bash",
    "text/x-sql": "sql",
    "application/sql": "sql",
    "text/yaml": "yaml",
    "application/x-yaml": "yaml",
    "application/yaml": "yaml",
  };
  if (map[m]) return map[m];
  if (m.endsWith("+json")) return "json";
  if (m.endsWith("+xml")) return "xml";
  return null;
}

type HighlightState =
  | { status: "pending" }
  | { status: "done"; html: string; language: string | null };

export function TextViewer({
  text,
  mimeType,
}: {
  text: string;
  mimeType: string;
}) {
  const [highlight, setHighlight] = useState<HighlightState>({ status: "pending" });
  const [copied, setCopied] = useState(false);
  const copyTimer = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => {
    setHighlight({ status: "pending" });
    const language = mimeToLanguage(mimeType);
    let cancelled = false;

    getHljs().then((hljs) => {
      if (cancelled) return;
      let result: string;
      if (language && hljs.getLanguage(language)) {
        result = hljs.highlight(text, { language }).value;
      } else {
        result = hljs.highlightAuto(text).value;
      }
      setHighlight({ status: "done", html: result, language });
    });

    return () => {
      cancelled = true;
    };
  }, [text, mimeType]);

  async function copyText() {
    try {
      await navigator.clipboard.writeText(text);
      setCopied(true);
      if (copyTimer.current) clearTimeout(copyTimer.current);
      copyTimer.current = setTimeout(() => setCopied(false), 2000);
    } catch {
      // Clipboard not available — silently skip.
    }
  }

  const lines = text.split("\n");
  const lineCount = lines.length;

  return (
    <div className={styles.wrapper}>
      <div className={styles.toolbar}>
        {highlight.status === "done" && highlight.language && (
          <span className={styles.langBadge}>{highlight.language}</span>
        )}
        <span className={styles.lineCount}>{lineCount} line{lineCount !== 1 ? "s" : ""}</span>
        <button
          className={styles.copyBtn}
          aria-label="Copy to clipboard"
          onClick={() => void copyText()}
        >
          {copied ? <Check size={15} /> : <Copy size={15} />}
          {copied ? "Copied" : "Copy"}
        </button>
      </div>
      <div className={styles.scroller}>
        <div className={styles.lineNumbers} aria-hidden="true">
          {lines.map((_, i) => (
            <span key={i}>{i + 1}</span>
          ))}
        </div>
        {highlight.status === "done" ? (
          <pre
            className={`${styles.code} hljs`}
            // Safe: HTML comes from highlight.js, which escapes user content.
            dangerouslySetInnerHTML={{ __html: highlight.html }}
          />
        ) : (
          <pre className={styles.code}>{text}</pre>
        )}
      </div>
    </div>
  );
}

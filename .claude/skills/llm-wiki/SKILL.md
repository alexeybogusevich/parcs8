---
name: llm-wiki
description: Ingest sources into and query the project's LLM-built wiki under `docs/`. Use whenever the user asks to ingest / process / add a document into the wiki, to answer a question from the wiki, to rebuild or lint the wiki, or otherwise works with `docs/raw/`, `docs/wiki/`, `docs/wiki/index.md`, or `docs/wiki/log.md`. Do NOT invoke for unrelated doc edits (README, CLAUDE.md, code-adjacent docs).
---

# LLM Wiki

Implements the pattern from Karpathy's gist (https://gist.github.com/karpathy/442a6bf555914893e9891c11519de94f): a **persistent, compounding markdown knowledge base** that the LLM incrementally builds from curated raw sources, rather than RAG-ing over raw documents each query.

## Layout

```
docs/
├── raw/                 # immutable curated sources — pdf, docx, md
│   ├── assets/          # images extracted/downloaded from sources
│   ├── documents/       # bulk of the research corpus
│   └── modules/         # module-adjacent documentation
└── wiki/                # LLM-generated markdown (created on first ingest)
    ├── index.md         # content-oriented catalogue, updated every ingest
    ├── log.md           # append-only chronological operation record
    ├── <entity>.md      # one page per recurring entity (person, system, algorithm)
    ├── <concept>.md     # one page per recurring concept
    └── <summary>.md     # per-source distilled summary
```

`raw/` is **immutable**. Never rewrite source files; extract into wiki pages instead. If a source is superseded, leave the raw file alone and note the supersession on the relevant wiki page.

`wiki/` is the only place the LLM writes knowledge. Do not create a second parallel hierarchy.

## Core operations

### 1. Ingest (one source at a time)

Triggered by: "ingest X", "add X to the wiki", "process the new doc", or a user pasting a new file into `raw/`.

Process per source:

1. **Read the source.** For `.pdf` use the `pdf` skill; for `.docx` use the `docx` skill; for `.md` use Read directly. Extract any embedded images into `docs/raw/assets/` if they're referenced by the wiki.
2. **Open `docs/wiki/index.md`** (create if missing, see template below) and identify which existing pages this source touches. Aim to **update 10–15 existing pages** before creating new ones — reuse is the point.
3. **Write/update wiki pages.** For each touched entity/concept:
   - Add a new section or paragraph with the new information.
   - Add an inline citation back to the source: `[source: raw/documents/Foo.md#section]` or `[source: raw/FooPaper.pdf, p.12]`.
   - Cross-link other wiki pages with relative links: `see [Genetic Algorithm](genetic-algorithm.md)`.
4. **Create a per-source summary page** at `docs/wiki/sources/<slug>.md` — a 10-30 line distillation: key claims, methods, numbers, who/when, and `Links:` back to every wiki page it fed into.
5. **Update `index.md`** — add the source under its category, add any new entity/concept pages to the relevant sections.
6. **Append to `log.md`** — one line, parseable format:
   ```
   ## [YYYY-MM-DD] ingest | <source filename> — <one-sentence takeaway> → updated: page1.md, page2.md; new: page3.md
   ```
7. **Stop and report.** Show the user what was updated; ask before batching more. Per the gist: "I prefer to ingest sources one at a time and stay involved."

Do **not** ingest an entire folder in one turn unless the user explicitly asks. Default is one source per operation.

### 2. Query

Triggered by: any question that plausibly overlaps the corpus ("what does the TSP analysis say about…", "summarise our GCP migration notes").

Process:

1. **Read `docs/wiki/index.md` first.** That's the entry point — it tells you which pages are relevant. Do not grep `raw/` blindly.
2. Read the 3–8 most relevant wiki pages.
3. Synthesise the answer with inline citations (`[wiki: page.md]` or `[source: raw/...]`).
4. If the query surfaces genuinely new structure (a cross-cutting concept you had to piece together from scratch), **file the result back**: create a new wiki page for it and link from `index.md`. Append a `query` line to `log.md` only if a new page was created — don't log every read.

### 3. Lint

Triggered by: "lint the wiki", "audit docs", or after a batch of ingests.

Walk `docs/wiki/` and report (do not silently fix):

- **Contradictions** — two pages claiming different values/conclusions for the same fact.
- **Orphans** — pages not linked from `index.md` or any other page.
- **Dangling links** — relative links pointing to non-existent pages.
- **Source gaps** — pages with claims but no `[source: …]` citations.
- **Stale supersessions** — pages citing a raw file that has been marked superseded.
- **Index drift** — raw sources not listed in `index.md`, or index entries whose pages don't exist.

Present findings as a checklist; let the user approve fixes before editing.

## Page conventions

Every wiki page starts with YAML frontmatter:

```yaml
---
title: <Human-readable title>
type: entity | concept | summary | index
status: unreviewed | pending | validated
sources:
  - raw/documents/Foo.md
  - raw/BarPaper.pdf
updated: YYYY-MM-DD
---
```

`status: validated` means **human-locked** — do not modify without explicit instruction. Treat it as read-only during ingest and lint.

Filenames: lowercase kebab-case, `.md`. Keep them short — `tsp-genetic-algorithm.md`, not `traveling-salesman-problem-genetic-algorithm-implementation.md`.

Keep pages focused. If a page grows past ~300 lines or covers multiple distinct concepts, split it and cross-link.

## `index.md` template

Organise by category, not alphabetically. Each entry is one line: `- [Title](page.md) — one-line hook`.

```markdown
# Wiki Index

## Systems & Architecture
- [PARCS Core](parcs-core.md) — module loading, daemon resolution, channels

## Algorithms
- [TSP Genetic Algorithm](tsp-genetic-algorithm.md) — parallel GA with island model

## Deployments
- [GCP Migration](gcp-migration.md) — Pub/Sub + KEDA scale-out notes

## Sources
- [raw/documents/TSP_Complete_Example.md](../raw/documents/TSP_Complete_Example.md) → see [tsp-genetic-algorithm.md]
- …
```

## `log.md` format

Append-only. Never rewrite history.

```markdown
# Wiki Log

## [2026-04-18] bootstrap | Wiki created, 40 raw sources staged under docs/raw/
## [2026-04-18] ingest | TSP_Complete_Example.md — parallel GA benchmark results → updated: tsp-genetic-algorithm.md, parcs-core.md; new: sources/tsp-complete-example.md
## [2026-04-19] lint | Found 2 orphans, 1 dangling link — see chat
```

## Scope boundaries

- **At ~100 sources / hundreds of pages, the index approach is enough.** If the wiki grows beyond that, propose adding a BM25/vector search layer — don't silently retrofit one.
- Ukrainian-language sources exist in `raw/documents/` — summarise in English in the wiki by default, but keep direct quotes in the original language with a translation beside them.
- Source code files (`.cs`, `.puml`, `.json`) exist in `raw/documents/modules/…` alongside markdown. Treat them as **reference material for the wiki** (cite them) but do not turn code files into wiki pages.

## What this skill does NOT do

- It does not rewrite `CLAUDE.md`, the root `README.md`, or code-adjacent READMEs outside `raw/`.
- It does not move files into or out of `raw/` without explicit user instruction.
- It does not delete wiki pages — mark `status: pending` and let the user decide.

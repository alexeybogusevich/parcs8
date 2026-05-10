# Figures

Source-of-truth for diagrams: `*.mmd` (Mermaid). PNG/PDF renders are committed
alongside the source under the same basename (e.g., `fig1-architecture.png`)
and are referenced from `sections/03-main.tex` via `\includegraphics`.

To re-render after editing a `.mmd` file (Mermaid CLI):

```bash
npx -y @mermaid-js/mermaid-cli -i fig1-architecture.mmd -o fig1-architecture.png -b transparent -s 2
```

Use `-s 2` (or higher) for print-quality output. Captions are not embedded in
the image — they are produced by `\citfigcaption{N}{<uk>}{<en>}` from
`cit-style.sty`.

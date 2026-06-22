# Sources for CIT Journal Article

Venue: [Комп'ютерно-інтегровані технології: освіта, наука, виробництво](https://cit.lntu.edu.ua/)
Requirements: [./requirements.pdf](./requirements.pdf)

## Layout

```
.
├── main.tex                    # entry point
├── cit-style.sty               # local style (geometry, captions, headings)
├── sections/                   # one file per paper section
├── figures/                    # .mmd source + .png/.pdf renders
├── bib/
│   ├── references.bib          # Roman-alphabet References (BibTeX)
└── requirements.pdf            # journal formatting spec
```

## Working rules

1. Edit in `sections/*.tex`. Once the LaTeX version stabilises, edits
   may move into `.tex`;
2. **Final delivery is `.docx`** per the journal. Convert via Pandoc only
   when the LaTeX version is locked; verify margins/fonts in Word against
   the `ЗРАЗОК` example on page 2 of `requirements.pdf`.

## Toolchain

- Engine: **pdfLaTeX** (with babel `[english,ukrainian]` and `T2A`/`T1`
  font encodings for Cyrillic).
- Bibliography: **BibTeX** for `References`; 
- Build:

  ```bash
  cd articles/cit2026
  latexmk -pdf main.tex
  # or, manually:
  pdflatex main && bibtex main && pdflatex main && pdflatex main
  ```

- Build artefacts (`*.aux`, `*.bbl`, `main.pdf`, …) are gitignored — only
  sources are committed.

## Required section order (per §1 of `requirements.pdf`)

1. Постановка наукової проблеми
2. Аналіз останніх досліджень і публікацій
3. Виділення невирішених раніше частин (folded into §1 or §2)
4. Формулювання мети дослідження (folded into the end of §2)
5. Виклад основного матеріалу + висновки з науковою новизною
6. Перспективи подальших досліджень

## Journal formatting checklist

| Item | Spec |
|---|---|
| Page size | A4, mirror margins |
| Margins | top/bottom 1.5 cm, inner 2.5 cm, outer 2.0 cm |
| Page numbers | none |
| Body font | Times New Roman 11 pt, single line spacing |
| Body indent | 1.25 cm |
| Abstract | 9 pt, 1 cm indent, **200 words** each (UK + EN) |
| Keywords | 5–10 |
| References font | 9 pt |
| Quotes | Ukrainian angle quotes « » (not "") |
| Formulas | centred, ≤ 5/6 line width |
| Figure captions | bilingual, centred: `Рис. N. … / Fig. N. …` |
| Table captions | bilingual, centred: `Таблиця N. … / Table N. …`; table width = text width − 1 cm |
| Reference blocks | **two** — ДСТУ 8302:2015 (Ukrainian) **and** Roman References, identical content |
| Length | 5–10 full pages |

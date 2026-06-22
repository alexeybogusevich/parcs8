/**
 * generate_hf_datasets.mjs
 * ─────────────────────────
 * Generates the PARCS-Agent benchmark dataset and pushes it to HuggingFace.
 *
 * Usage:
 *   npm install @huggingface/hub
 *   node generate_hf_datasets.mjs --token hf_YOURTOKEN --org parcs-benchmark
 *   node generate_hf_datasets.mjs --token hf_YOURTOKEN --org parcs-benchmark --dry-run
 *
 * Schema (benchmark split — one row per task):
 *   task_id   number  — 1–15
 *   question  string  — self-contained prompt given to the agent
 *   answer    string  — JSON with reference / ground-truth values
 *
 * Supplementary splits (input data for seed-based tasks):
 *   task_03_cities, task_05_data, task_05_configs, task_06_patients,
 *   task_07_sequences, task_08_train, task_08_test, task_10_portfolio,
 *   task_10_scenarios, task_11_library, task_13_nodes, task_13_edges,
 *   task_13_sources, task_15_sequences, task_04_sir_reference
 */

import { createRepo, uploadFiles } from "@huggingface/hub";

// ── CLI args ──────────────────────────────────────────────────────────────────
const args = Object.fromEntries(
  process.argv.slice(2).reduce((acc, a, i, arr) => {
    if (a.startsWith("--")) acc.push([a.slice(2), arr[i + 1] ?? true]);
    return acc;
  }, [])
);
const ORG    = args.org    ?? "parcs-benchmark";
const TOKEN  = args.token  ?? process.env.HF_TOKEN ?? "";
const DRY    = !!args["dry-run"];
const REPO   = `${ORG}/parcs-agent-benchmark`;

if (!DRY && !TOKEN) {
  console.error("ERROR: provide --token hf_YOURTOKEN or set HF_TOKEN env var");
  process.exit(1);
}

// ── Seeded PRNG (Mulberry32) ──────────────────────────────────────────────────
function mulberry32(seed) {
  let s = seed >>> 0;
  return () => {
    s += 0x6d2b79f5;
    let t = Math.imul(s ^ (s >>> 15), 1 | s);
    t ^= t + Math.imul(t ^ (t >>> 7), 61 | t);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

// Box-Muller normal variate
function makeNormal(rng) {
  let spare = null;
  return () => {
    if (spare !== null) { const v = spare; spare = null; return v; }
    let u, v;
    do { u = rng() * 2 - 1; v = rng() * 2 - 1; } while (u*u + v*v >= 1);
    const mag = Math.sqrt(-2 * Math.log(u*u + v*v) / (u*u + v*v));
    spare = v * mag;
    return u * mag;
  };
}

// Poisson variate (Knuth method, suitable for small λ)
function poissonSample(rng, lambda) {
  const L = Math.exp(-lambda);
  let k = 0, p = 1;
  do { k++; p *= rng(); } while (p > L);
  return k - 1;
}

// Lognormal variate
function lognormal(rng, mu, sigma) {
  const norm = makeNormal(rng);
  return Math.exp(mu + sigma * norm());
}

// ── JSONL helpers ─────────────────────────────────────────────────────────────
function toJsonl(rows) {
  return rows.map(r => JSON.stringify(r)).join("\n") + "\n";
}

function toBlob(str) {
  return new Blob([str], { type: "application/jsonlines" });
}

// ── HuggingFace upload ────────────────────────────────────────────────────────
const uploads = {};  // path → Blob

function addSplit(name, rows) {
  const path = `data/${name}.jsonl`;
  uploads[path] = toBlob(toJsonl(rows));
  console.log(`  ${name}: ${rows.length} rows`);
}

const HF_REPO = { type: "dataset", name: REPO };

async function push() {
  if (DRY) { console.log("\n[dry-run] skipping push."); return; }
  console.log(`\nCreating repo ${REPO} …`);
  await createRepo({ repo: HF_REPO, accessToken: TOKEN }).catch(() => {});
  const files = Object.entries(uploads).map(([path, blob]) => ({ path, content: blob }));
  await uploadFiles({ repo: HF_REPO, files, accessToken: TOKEN, commitTitle: "Add benchmark dataset" });
  console.log(`✓ https://huggingface.co/datasets/${REPO}`);
}

// ══════════════════════════════════════════════════════════════════════════════
// Data generators
// ══════════════════════════════════════════════════════════════════════════════

// ── Task 1: Monte Carlo VaR ───────────────────────────────────────────────────
function buildTask01() {
  // Compute a reference VaR with 50k scenarios (fast proxy)
  const rng = mulberry32(42);
  const norm = makeNormal(rng);
  const n = 50, nScen = 50_000;
  // A ∈ ℝ^{50×50}
  const A = Array.from({ length: n }, () => Array.from({ length: n }, () => norm()));
  // Σ = AᵀA/n + 0.01·I
  const cov = Array.from({ length: n }, (_, i) =>
    Array.from({ length: n }, (__, j) => {
      let s = 0; for (let k = 0; k < n; k++) s += A[k][i] * A[k][j];
      return s / n + (i === j ? 0.01 : 0);
    })
  );
  // Cholesky L (lower)
  const L = Array.from({ length: n }, () => new Array(n).fill(0));
  for (let i = 0; i < n; i++) {
    for (let j = 0; j <= i; j++) {
      let s = cov[i][j];
      for (let k = 0; k < j; k++) s -= L[i][k] * L[j][k];
      L[i][j] = j === i ? Math.sqrt(s) : s / L[j][j];
    }
  }
  const w = 1 / n;
  const losses = [];
  const rng2 = mulberry32(42042);
  const norm2 = makeNormal(rng2);
  for (let s = 0; s < nScen; s++) {
    const z = Array.from({ length: n }, () => norm2());
    let ret = 0;
    for (let i = 0; i < n; i++) {
      let ri = 0; for (let j = 0; j <= i; j++) ri += L[i][j] * z[j];
      ret += w * ri;
    }
    losses.push(-ret);
  }
  losses.sort((a, b) => a - b);
  const var99 = losses[Math.floor(0.99 * nScen)];
  const cvar99 = losses.slice(Math.floor(0.99 * nScen)).reduce((a, b) => a + b, 0) /
                 losses.slice(Math.floor(0.99 * nScen)).length;
  return { var_99_approx: +var99.toFixed(6), cvar_99_approx: +cvar99.toFixed(6),
           note: "Reference computed with 50k scenarios; full task uses 2M." };
}

// ── Task 3: TSP ───────────────────────────────────────────────────────────────
function buildTask03Cities() {
  const rng = mulberry32(12345);
  const rows = [];
  for (let i = 0; i < 300; i++)
    rows.push({ city_id: i, x: +(rng() * 1000).toFixed(4), y: +(rng() * 1000).toFixed(4) });
  return rows;
}

function nearestNeighbourTour(cities) {
  const n = cities.length;
  const visited = new Uint8Array(n);
  const tour = [0]; visited[0] = 1;
  for (let step = 1; step < n; step++) {
    const cur = tour[tour.length - 1];
    let best = Infinity, bestJ = -1;
    for (let j = 0; j < n; j++) {
      if (!visited[j]) {
        const dx = cities[cur].x - cities[j].x, dy = cities[cur].y - cities[j].y;
        const d = Math.sqrt(dx*dx + dy*dy);
        if (d < best) { best = d; bestJ = j; }
      }
    }
    tour.push(bestJ); visited[bestJ] = 1;
  }
  let total = 0;
  for (let i = 0; i < n; i++) {
    const a = cities[tour[i]], b = cities[tour[(i+1) % n]];
    const dx = a.x-b.x, dy = a.y-b.y;
    total += Math.sqrt(dx*dx + dy*dy);
  }
  return +total.toFixed(2);
}

// ── Task 4: SIR Sweep ─────────────────────────────────────────────────────────
function buildTask04SIR() {
  const R0vals = Array.from({ length: 20 }, (_, i) => 0.8 + (3.2 / 19) * i);
  const thVals = Array.from({ length: 20 }, (_, i) => 0.01 + (0.19 / 19) * i);
  const gamma = 1 / 14;
  const N = 1_000_000;
  const results = [];
  for (const R0 of R0vals) {
    for (const theta of thVals) {
      const beta = R0 * gamma;
      let S = 999_900, I = 100, R = 0;
      let peakI = I, daysToP = 0, intDays = 0;
      for (let d = 0; d < 365; d++) {
        const betaEff = I / N >= theta ? beta * 0.5 : beta;
        if (I / N >= theta) intDays++;
        const newI = betaEff * S * I / N;
        const newR = gamma * I;
        S -= newI; I += newI - newR; R += newR;
        if (I > peakI) { peakI = I; daysToP = d + 1; }
      }
      results.push({
        R0: +R0.toFixed(4), theta: +theta.toFixed(4),
        peak_infected_frac: +(peakI / N).toFixed(6),
        attack_rate: +(R / N).toFixed(6),
        days_to_peak: daysToP, intervention_days: intDays,
      });
    }
  }
  return results;
}

// ── Task 5: ML data ───────────────────────────────────────────────────────────
function buildTask05Data() {
  const rng = mulberry32(42);
  const norm = makeNormal(rng);
  const N = 80_000, F = 25;
  const beta = Array.from({ length: F }, () => norm());
  const rows = [];
  const rng2 = mulberry32(420);
  const norm2 = makeNormal(rng2);
  const rng3 = mulberry32(4200);
  for (let i = 0; i < N; i++) {
    const x = Array.from({ length: F }, () => norm2());
    const logit = x.reduce((s, xi, j) => s + xi * beta[j], 0);
    const p = 1 / (1 + Math.exp(-logit));
    const label = rng3() < p ? 1 : 0;
    const row = {};
    x.forEach((v, j) => { row[`f${j}`] = +v.toFixed(5); });
    row.label = label;
    rows.push(row);
  }
  return rows;
}

function buildTask05Configs() {
  const configs = [];
  let idx = 0;
  for (const lr of [0.01, 0.05, 0.1])
    for (const md of [3, 5, 7])
      for (const ne of [100, 300])
        for (const ss of [0.8, 1.0])
          configs.push({ config_index: idx++, learning_rate: lr, max_depth: md, n_estimators: ne, subsample: ss });
  return configs;
}

// ── Task 6: Survival data ─────────────────────────────────────────────────────
function buildTask06Patients() {
  const rng = mulberry32(42);
  const norm = makeNormal(rng);
  const beta = [0.5, -0.3, 0.8, 0.1, -0.6];
  const N = 8_000;
  const rng2 = mulberry32(4242);
  const norm2 = makeNormal(rng2);
  const rng3 = mulberry32(42420);
  const eventTimes = [];
  const rows = [];
  for (let i = 0; i < N; i++) {
    const cov = Array.from({ length: 5 }, () => norm());
    const logH = cov.reduce((s, c, j) => s + c * beta[j], 0);
    const rate = Math.exp(logH);
    const et = -Math.log(rng2()) / rate;   // Exponential via inverse CDF
    eventTimes.push(et);
    const row = {};
    cov.forEach((v, j) => { row[`cov_${j}`] = +v.toFixed(5); });
    row._et = et;
    rows.push(row);
  }
  eventTimes.sort((a, b) => a - b);
  const maxT = eventTimes[Math.floor(0.70 * N)];
  for (const row of rows) {
    const ct = rng3() * maxT;
    const ot = Math.min(row._et, ct);
    row.time = +ot.toFixed(5);
    row.event = row._et <= ct ? 1 : 0;
    delete row._et;
  }
  return rows;
}

// ── Task 7: Protein sequences ─────────────────────────────────────────────────
function buildTask07Sequences() {
  const AA = "ACDEFGHIKLMNPQRSTVWY";
  const freq = [0.074,0.025,0.054,0.054,0.047,0.074,0.026,0.068,
                0.058,0.099,0.025,0.045,0.039,0.034,0.052,0.057,
                0.051,0.073,0.013,0.032];
  const cum = freq.reduce((a, f, i) => { a.push((a[i-1]??0)+f); return a; }, []);
  function sampleAA(r) {
    for (let i = 0; i < cum.length; i++) if (r < cum[i]) return AA[i];
    return AA[AA.length-1];
  }
  const rows = [];
  for (let i = 0; i < 600; i++) {
    const rngL = mulberry32(i);
    const len = Math.floor(rngL() * 451) + 50;   // Uniform(50,500)
    const rngS = mulberry32(i * 31 + 17);
    let seq = "";
    for (let j = 0; j < len; j++) seq += sampleAA(rngS());
    rows.push({ seq_id: i, length: len, sequence: seq });
  }
  return rows;
}

// ── Task 8: Random forest data ────────────────────────────────────────────────
function buildTask08Data() {
  const rng = mulberry32(99);
  const norm = makeNormal(rng);
  const N = 70_000, F = 30;
  const beta = Array.from({ length: F }, () => norm());
  const rng2 = mulberry32(9999);
  const norm2 = makeNormal(rng2);
  const rng3 = mulberry32(99990);
  const norm3 = makeNormal(rng3);
  const train = [], test = [];
  for (let i = 0; i < N; i++) {
    const x = Array.from({ length: F }, () => norm2());
    const dot = x.reduce((s, xi, j) => s + xi * beta[j], 0);
    const y = Math.sin(dot) + 0.1 * norm3();
    const row = {};
    x.forEach((v, j) => { row[`f${j}`] = +v.toFixed(5); });
    row.target = +y.toFixed(6);
    (i < 60_000 ? train : test).push(row);
  }
  return { train, test };
}

// ── Task 10: Stress testing ───────────────────────────────────────────────────
function buildTask10() {
  const rngP = mulberry32(77);
  const normP = makeNormal(rngP);
  const rngS = mulberry32(99);
  const normS = makeNormal(rngS);
  const NI = 300, NF = 15, NS = 500;
  const deltas = Array.from({ length: NI }, () => Array.from({ length: NF }, () => normP()));
  const rawW = Array.from({ length: NI }, () => rngP());
  const sumW = rawW.reduce((a, b) => a + b, 0);
  const w = rawW.map(v => v / sumW);
  const shocks = Array.from({ length: NS }, () => Array.from({ length: NF }, () => normS() * 0.03));

  // P&L for each scenario
  const pnl = shocks.map(shock =>
    w.reduce((sum, wi, i) => sum + wi * deltas[i].reduce((s, d, j) => s + d * shock[j], 0), 0)
  );
  const pnlSorted = [...pnl].sort((a, b) => a - b);
  const var99 = pnlSorted[Math.floor(0.01 * NS)];
  const worst10 = pnl.map((v, i) => ({ scenario_id: i, pnl: +v.toFixed(6) }))
                     .sort((a, b) => a.pnl - b.pnl).slice(0, 10);

  const portfolio = w.map((wi, i) => {
    const row = { instrument_id: i, weight: +wi.toFixed(6) };
    deltas[i].forEach((d, j) => { row[`delta_${j}`] = +d.toFixed(5); });
    return row;
  });
  const scenarios = shocks.map((sh, s) => {
    const row = { scenario_id: s };
    sh.forEach((v, j) => { row[`shock_${j}`] = +v.toFixed(5); });
    return row;
  });
  const ref = { pnl_var_99: +var99.toFixed(6), pnl_std: +(Math.sqrt(pnl.reduce((s,v)=>s+v*v,0)/NS)).toFixed(6),
                worst_10_scenario_ids: worst10.map(r => r.scenario_id) };
  return { portfolio, scenarios, ref };
}

// ── Task 11: Virtual screening ────────────────────────────────────────────────
function buildTask11() {
  const rng = mulberry32(55);
  const rngR = mulberry32(0);
  const N = 50_000, BITS = 2048;
  const WORDS = BITS / 32;

  function genFP(r) {
    const fp = new Uint32Array(WORDS);
    for (let i = 0; i < WORDS; i++) {
      let w = 0;
      for (let b = 0; b < 32; b++) if (r() < 0.05) w |= (1 << b);
      fp[i] = w;
    }
    return fp;
  }

  function tanimoto(a, b) {
    let inter = 0, union = 0;
    for (let i = 0; i < WORDS; i++) {
      const ab = a[i] & b[i], au = a[i] | b[i];
      inter += popcount(ab); union += popcount(au);
    }
    return union === 0 ? 0 : inter / union;
  }

  function popcount(x) {
    x = x - ((x >> 1) & 0x55555555);
    x = (x & 0x33333333) + ((x >> 2) & 0x33333333);
    return (((x + (x >> 4)) & 0x0f0f0f0f) * 0x01010101) >>> 24;
  }

  function fpHex(fp) {
    return Array.from(fp).map(w => w.toString(16).padStart(8, "0")).join("");
  }

  const refFP = genFP(rngR);
  const rows = [];
  let lipPass = 0, taniSum = 0;
  const scored = [];

  for (let i = 0; i < N; i++) {
    const fp = genFP(rng);
    const mw   = Math.max(100, Math.min(700, 350 + (rng()-0.5)*2*240));
    const hbd  = Math.floor(rng() * 8);
    const hba  = Math.floor(rng() * 12);
    const logp = Math.max(-3, Math.min(7, 2.5 + (rng()-0.5)*2*4.5));
    const tani = tanimoto(fp, refFP);
    taniSum += tani;
    const pass = mw <= 500 && hbd <= 5 && hba <= 10 && logp <= 5;
    if (pass) lipPass++;
    const score = 0.7*tani + 0.1*(1-mw/700) + 0.1*(1-hbd/8) + 0.1*(1-logp/7);
    scored.push({ mol_id: i, score });
    rows.push({ mol_id: i, fingerprint: fpHex(fp),
                mw: +mw.toFixed(2), hbd, hba, logp: +logp.toFixed(3) });
  }

  scored.sort((a, b) => b.score - a.score);
  const top100 = scored.slice(0, 100).map(r => r.mol_id);
  const ref = { top_100_mol_ids: top100, lipinski_pass_rate: +(lipPass/N).toFixed(4),
                mean_tanimoto: +(taniSum/N).toFixed(6) };
  return { library: rows, ref };
}

// ── Task 13: Graph ────────────────────────────────────────────────────────────
function buildTask13() {
  const rng = mulberry32(314);
  const N = 5_000;
  const xs = Array.from({ length: N }, () => rng());
  const ys = Array.from({ length: N }, () => rng());
  const D = 0.055;

  // Simple grid-bucket approach to find pairs within distance D
  const edgeRows = [];
  const buckets = {};
  const bsize = D;
  for (let i = 0; i < N; i++) {
    const bx = Math.floor(xs[i] / bsize);
    const by = Math.floor(ys[i] / bsize);
    const key = `${bx},${by}`;
    if (!buckets[key]) buckets[key] = [];
    buckets[key].push(i);
  }

  const seen = new Set();
  for (let i = 0; i < N; i++) {
    const bx = Math.floor(xs[i] / bsize);
    const by = Math.floor(ys[i] / bsize);
    for (let dbx = -1; dbx <= 1; dbx++) for (let dby = -1; dby <= 1; dby++) {
      const nb = buckets[`${bx+dbx},${by+dby}`];
      if (!nb) continue;
      for (const j of nb) {
        if (j <= i) continue;
        const dx = xs[i]-xs[j], dy = ys[i]-ys[j];
        if (dx*dx + dy*dy <= D*D) {
          const w = +(1 + rng() * 29).toFixed(3);
          edgeRows.push({ u: i, v: j, weight_minutes: w });
        }
      }
    }
  }

  const nodeRows = Array.from({ length: N }, (_, i) =>
    ({ node_id: i, x: +xs[i].toFixed(5), y: +ys[i].toFixed(5) }));
  const sourceRows = Array.from({ length: 400 }, (_, i) =>
    ({ source_id: i, node: i % N, worker_id: Math.floor(i / 20) }));

  return { nodes: nodeRows, edges: edgeRows, sources: sourceRows,
           ref: { n_edges: edgeRows.length } };
}

// ── Task 15: Genome k-mers ────────────────────────────────────────────────────
function buildTask15Sequences() {
  const BASES = ["A","C","G","T"];
  const PROBS = [0.30, 0.20, 0.20, 0.30];
  const CUM   = PROBS.reduce((a,p,i) => { a.push((a[i-1]??0)+p); return a; }, []);
  function base(r) { for (let i=0;i<CUM.length;i++) if(r<CUM[i]) return BASES[i]; return "T"; }

  const rows = [];
  for (let i = 0; i < 20; i++) {
    const rng = mulberry32(i * 104729 + 1);
    let seq = "";
    let gc = 0;
    for (let j = 0; j < 500_000; j++) {
      const b = base(rng());
      seq += b;
      if (b === "C" || b === "G") gc++;
    }
    rows.push({ seq_id: i, length: 500_000, gc_content: +(gc/500_000).toFixed(4), sequence: seq });
  }
  return rows;
}

// ══════════════════════════════════════════════════════════════════════════════
// Questions
// ══════════════════════════════════════════════════════════════════════════════
const Q = {
  1: `You have a portfolio of 50 assets with equal weights (w_i = 1/50). The return covariance matrix Σ ∈ ℝ^{50×50} is generated from seed=42: A = randn(50,50,seed=42); Σ = Aᵀ·A/50 + 0.01·I. Using 2,000,000 Monte Carlo scenarios (each of 20 workers generates 100,000 using seed WorkerIndex*1000+42 and the shared Cholesky factor L of Σ), estimate the 1-day 99% Value-at-Risk (VaR) and Conditional VaR (CVaR) of the portfolio loss distribution. Loss = –(portfolio return). Return {var_99, cvar_99} as floats.`,

  2: `Price 200 down-and-out European call options on a (S,σ,T) grid: S ∈ {80,85,90,95,100,105,110,115,120,125}, σ ∈ {0.10,0.15,0.20,0.25}, T ∈ {0.25,0.5,1.0,2.0,4.0} years. Parameters: K=100, r=0.05, B=0.85·S. Use 500,000 GBM paths per grid point; each of 20 workers prices 10 points using seed WorkerIndex*999+7. Compute delta and vega by central finite difference. Return a JSON array of {S,sigma,T,price,delta,vega} for all 200 points.`,

  3: `Find the shortest Hamiltonian tour through 300 cities. City coordinates: x[i]=rng()*1000, y[i]=rng()*1000 generated from seed=12345 (Mulberry32 PRNG). Run 20 independent Simulated Annealing trials (α=0.9995, T₀=1000, 500,000 iterations, 2-opt neighbourhood), each worker using seed WorkerIndex*777. Return {best_tour_length, best_worker_seed, all_trial_lengths}. The nearest-neighbour baseline is provided in the answer for reference.`,

  4: `Simulate a discrete-time SIR epidemic on N=1,000,000 individuals for 365 days across a 20×20 parameter grid: R₀ ∈ linspace(0.8,4.0,20), θ ∈ linspace(0.01,0.20,20). Parameters: γ=1/14, β=R₀·γ; when I/N ≥ θ apply 50% contact-rate reduction. Initial conditions: S₀=999900, I₀=100, R₀_init=0. Each of 20 workers simulates 20 parameter combinations. Return a JSON array of {R0,theta,peak_infected_frac,attack_rate,days_to_peak,intervention_days} for all 400 combinations.`,

  5: `Train a gradient boosting classifier on a synthetic dataset (80,000 rows, 25 features). Dataset: features X~N(0,1)^{80000×25} from seed=42, labels via logit(P(y=1))=X·β, β~N(0,1) from seed=42. Evaluate all 20 hyperparameter configs (learning_rate∈{0.01,0.05,0.1}, max_depth∈{3,5,7}, n_estimators∈{100,300}, subsample∈{0.8,1.0}) with 5-fold CV. Each worker trains one config. Return {best_config_index, best_auc_roc, ranked_configs:[{config_index,auc_roc}]}.`,

  6: `Fit a Cox proportional hazards model on a synthetic patient dataset (N=8,000; 5 covariates cov_0…cov_4; time; event) and compute 95% bootstrap confidence intervals for all 5 hazard ratios using B=10,000 resamples. Dataset: cov~N(0,1) seed=42; β=(0.5,−0.3,0.8,0.1,−0.6); event times~Exponential(exp(X·β)); 30% censoring. Each of 20 workers runs 500 resamples. Return {cov_i:{ci_lower,ci_upper,point_estimate}} for i=0..4.`,

  7: `Compute the 600×600 pairwise similarity matrix for 600 synthetic protein sequences using simplified Smith-Waterman (match=+2, mismatch=−1, gap=−2). Sequence i: length~Uniform(50,500) from seed=i, amino acids from 20-letter alphabet with biological frequencies from seed=i*31+17. Normalise scores to [0,1] by dividing by min(len_i,len_j)*2. Return {top_20_pairs:[{seq_i,seq_j,score}], matrix_checksum (sum of upper-triangular normalised scores, 4 d.p.), n_families_above_0p7}.`,

  8: `Train a random forest of 200 decision trees on a synthetic regression dataset (60,000 train rows, 30 features, 10,000 test rows). Dataset: X~N(0,1)^{70000×30} from seed=99; y=sin(X·β)+0.1·ε, β~N(0,1) seed=99. Each tree: bootstrap sample, √30≈5 features/split, max_depth=15, min_leaf=5. Each of 20 workers grows 10 trees using seeds WorkerIndex*200+treeIndex. Return {oob_rmse, test_rmse, feature_importances:[{feature,importance}] top 10}.`,

  9: `Generate and test 10,000 candidate 512-bit odd integers for primality using Miller-Rabin (k=20 witness rounds, FP rate < 4^{−20}). For each confirmed prime p, check if (p−1)/2 is also prime (safe prime). Each of 20 workers tests 500 candidates using seed WorkerIndex*9973 to generate 512-bit odd numbers. Return {prime_count, safe_prime_count, empirical_prime_density, pnt_predicted_density (=1/ln(2^512)), iteration_histogram}.`,

  10: `Reprice a portfolio of 300 instruments under 500 stress scenarios using delta-linear approximation: P&L_s = Σ_i w_i·(δ_i·shock_s). Portfolio: sensitivities δ∈ℝ^{300×15} and weights w from seed=77. Scenarios: 500 shock vectors∈ℝ^{15}, entries~N(0,0.03) from seed=99. Each of 20 workers prices the full portfolio under 25 scenarios. Return {worst_10_scenarios:[{scenario_id,pnl}], pnl_var_99 (1st percentile), pnl_std, top_3_loss_drivers per worst scenario}.`,

  11: `Score 50,000 molecules against a reference kinase inhibitor fingerprint using Tanimoto similarity on 2048-bit Morgan fingerprints. Library: fps (bit-set prob 0.05 each bit) from seed=55; MW~N(350,80), HBD~Uniform(0,7), HBA~Uniform(0,11), logP~N(2.5,1.5). Reference fp from seed=0. Proxy score=0.7·Tanimoto+0.1·(1−MW/700)+0.1·(1−HBD/8)+0.1·(1−logP/7). Apply Lipinski filters: MW≤500, HBD≤5, HBA≤10, logP≤5. Each of 20 workers scores 2,500 molecules. Return {top_100_mol_ids, lipinski_pass_rate, mean_tanimoto}.`,

  12: `Simulate 2,000,000 policy years under a compound Poisson risk process: claim count N~Poisson(λ=200), severity X~Lognormal(μ=8,σ=1.5). Premium P=(1+0.2)·E[S]; initial surplus U=500,000. Each of 20 workers simulates 100,000 policy years using seed_counts=WorkerIndex*2053+1, seed_severities=WorkerIndex*3571+2. Return {ruin_prob_1yr, ruin_prob_5yr, ruin_prob_10yr, scr_995 (99.5th-percentile annual loss), expected_deficit_given_ruin}.`,

  13: `On a synthetic road-network graph (5,000 nodes, ~25,000 edges): compute shortest-path lengths from 400 sampled source nodes (Dijkstra), approximate betweenness centrality for all nodes, network diameter and average path length (hop count), and top-20 most critical edges by removal impact. Graph: nodes in [0,1]² from seed=314; edges between nodes within distance 0.055; weights~Uniform(1,30) minutes from seed=314. Each of 20 workers runs Dijkstra from 20 source nodes (WorkerIndex*20…WorkerIndex*20+19). Return {top_20_nodes_by_betweenness, top_20_critical_edges, avg_path_length_hops, diameter_hops}.`,

  14: `Simulate photon transport through a 50-layer plane-parallel atmosphere. Layer l: τ_scat[l]=0.1·exp(−0.05·l), τ_abs[l]=0.02·exp(−0.08·l), ω[l]=τ_scat/(τ_scat+τ_abs). Solar zenith θ=30°. Rayleigh phase function. Simulate 5,000,000 photons; each of 20 workers traces 250,000 using seed WorkerIndex*6271+3. Return {toa_radiance, surface_irradiance, heating_rates:[50 floats], ssa_retrieval_rmse}.`,

  15: `Compute the k=8 frequency spectrum (4^8=65,536 k-mers) for each of 20 synthetic genomic sequences (500,000 bp, composition A:0.30,C:0.20,G:0.20,T:0.30, sequence i from seed=i*104729+1) and for the combined corpus. For each sequence: top-50 over/under-represented k-mers vs. null expectation; repeat regions (k-mer freq>100 in consecutive 10kb windows). Final layer: Jensen-Shannon divergence between each spectrum and corpus mean. Return {js_divergences:[20 floats], corpus_top_10_kmers, frequency_table_checksums:[20 ints], expected_checksum_per_seq:499993}.`,
};

// ══════════════════════════════════════════════════════════════════════════════
// Main
// ══════════════════════════════════════════════════════════════════════════════
async function main() {
  console.log("Building datasets …\n");

  // --- Reference answers ---
  console.log("Task 1: Monte Carlo VaR …");
  const ref1 = buildTask01();

  console.log("Task 3: TSP cities …");
  const cities3 = buildTask03Cities();
  const nn3 = nearestNeighbourTour(cities3);
  addSplit("task_03_cities", cities3);

  console.log("Task 4: SIR sweep (400 simulations) …");
  const sir4 = buildTask04SIR();
  addSplit("task_04_sir_reference", sir4);
  const ref4 = { max_peak_infected_frac: +Math.max(...sir4.map(r=>r.peak_infected_frac)).toFixed(6),
                 min_attack_rate: +Math.min(...sir4.map(r=>r.attack_rate)).toFixed(6) };

  console.log("Task 5: ML data (80k rows) …");
  addSplit("task_05_data", buildTask05Data());
  addSplit("task_05_configs", buildTask05Configs());

  console.log("Task 6: Survival data (8k patients) …");
  addSplit("task_06_patients", buildTask06Patients());

  console.log("Task 7: Protein sequences (600) …");
  addSplit("task_07_sequences", buildTask07Sequences());

  console.log("Task 8: Random forest data (70k rows) …");
  const { train: tr8, test: te8 } = buildTask08Data();
  addSplit("task_08_train", tr8);
  addSplit("task_08_test", te8);
  const testVar8 = te8.reduce((s,r) => s + r.target*r.target, 0) / te8.length;

  console.log("Task 10: Stress testing …");
  const t10 = buildTask10();
  addSplit("task_10_portfolio", t10.portfolio);
  addSplit("task_10_scenarios", t10.scenarios);

  console.log("Task 11: Virtual screening (50k molecules) …");
  const t11 = buildTask11();
  addSplit("task_11_library", t11.library);

  console.log("Task 13: Road network graph …");
  const t13 = buildTask13();
  addSplit("task_13_nodes",   t13.nodes);
  addSplit("task_13_edges",   t13.edges);
  addSplit("task_13_sources", t13.sources);

  console.log("Task 15: Genomic sequences (20 × 500k bp) …");
  addSplit("task_15_sequences", buildTask15Sequences());

  // --- Benchmark split ---
  const pntDensity = 1 / (512 * Math.log(2));
  const E_S12 = 200 * Math.exp(8 + 1.5*1.5/2);

  const benchmark = [
    { task_id: 1,  question: Q[1],  answer: JSON.stringify(ref1) },
    { task_id: 2,  question: Q[2],  answer: JSON.stringify({ reference_price_S100_sig020_T1: 7.9153, note: "Analytical barrier option price at S=100,σ=0.20,T=1." }) },
    { task_id: 3,  question: Q[3],  answer: JSON.stringify({ nearest_neighbour_tour_length: nn3, note: "SA with 20 trials should beat NN by 10–20%." }) },
    { task_id: 4,  question: Q[4],  answer: JSON.stringify(ref4) },
    { task_id: 5,  question: Q[5],  answer: JSON.stringify({ note: "Best AUC-ROC typically 0.82–0.90 for max_depth≥5 configs." }) },
    { task_id: 6,  question: Q[6],  answer: JSON.stringify({ true_betas: [0.5,-0.3,0.8,0.1,-0.6], note: "95% CIs should bracket true betas." }) },
    { task_id: 7,  question: Q[7],  answer: JSON.stringify({ n_pairs: 179700, note: "Expect 5–15 high-similarity family clusters." }) },
    { task_id: 8,  question: Q[8],  answer: JSON.stringify({ target_variance_test: +testVar8.toFixed(4), note: "Expect test_rmse << sqrt(target_variance)." }) },
    { task_id: 9,  question: Q[9],  answer: JSON.stringify({ pnt_predicted_density: +pntDensity.toFixed(6), expected_primes_in_10000: +(10000*pntDensity).toFixed(1) }) },
    { task_id: 10, question: Q[10], answer: JSON.stringify(t10.ref) },
    { task_id: 11, question: Q[11], answer: JSON.stringify(t11.ref) },
    { task_id: 12, question: Q[12], answer: JSON.stringify({ annual_premium: +((1.2*E_S12)).toFixed(2), E_S: +E_S12.toFixed(2), note: "Cramér-Lundberg: ruin prob ≤ exp(−R·U) for θ=0.2." }) },
    { task_id: 13, question: Q[13], answer: JSON.stringify({ n_edges: t13.ref.n_edges }) },
    { task_id: 14, question: Q[14], answer: JSON.stringify({ total_optical_depth: +(Array.from({length:50},(_,l)=>0.1*Math.exp(-0.05*l)+0.02*Math.exp(-0.08*l)).reduce((a,b)=>a+b,0)).toFixed(4) }) },
    { task_id: 15, question: Q[15], answer: JSON.stringify({ expected_checksum_per_seq: 499993, note: "checksum = seq_length − k + 1 = 499993." }) },
  ];
  addSplit("benchmark", benchmark);

  // --- README ---
  const readme = `---
license: mit
task_categories:
  - other
tags:
  - parcs
  - benchmark
  - parallel-computing
  - ai-agents
pretty_name: "PARCS-Agent Benchmark — 15 Parallel Computing Tasks"
---

# PARCS-Agent Benchmark

15 tasks for evaluating PARCS-enabled (parallel) vs. sequential AI agents.

## Main split — \`benchmark\`

| Column | Type | Description |
|--------|------|-------------|
| \`task_id\` | int | Task number 1–15 |
| \`question\` | string | Self-contained prompt given to the agent |
| \`answer\` | string | JSON with reference / ground-truth values |

## Supplementary splits

Seed-based tasks include pre-generated input data:
\`task_03_cities\`, \`task_04_sir_reference\`, \`task_05_data\`, \`task_05_configs\`,
\`task_06_patients\`, \`task_07_sequences\`, \`task_08_train\`, \`task_08_test\`,
\`task_10_portfolio\`, \`task_10_scenarios\`, \`task_11_library\`,
\`task_13_nodes\`, \`task_13_edges\`, \`task_13_sources\`, \`task_15_sequences\`

## Citation

PARCS-Agent benchmark (Bohusevych, 2026). Forthcoming.
`;
  uploads["README.md"] = toBlob(readme);

  // --- Push ---
  await push();

  // --- Summary ---
  console.log(`\nSplits generated: ${Object.keys(uploads).length}`);
  console.log(`Benchmark rows:   ${benchmark.length}`);
  console.log("Done.");
}

main().catch(e => { console.error(e); process.exit(1); });

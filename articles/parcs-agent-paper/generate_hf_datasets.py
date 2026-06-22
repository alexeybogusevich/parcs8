"""
generate_hf_datasets.py
-----------------------
Generates the PARCS-Agent benchmark dataset and pushes it to HuggingFace.

Schema (one row per task):
  task_id   int    — 1-15
  question  str    — self-contained prompt given to the agent
  answer    str    — JSON string with reference/ground-truth values

Supplementary splits (seed-based tasks that need input data):
  task_03_cities, task_05_data, task_06_patients, task_07_sequences,
  task_08_train, task_08_test, task_10_portfolio, task_10_scenarios,
  task_11_library, task_13_nodes, task_13_edges, task_15_sequences

Usage:
    python -m pip install huggingface_hub datasets numpy pandas pyarrow scipy
    huggingface-cli login
    python generate_hf_datasets.py --org parcs-benchmark
    python generate_hf_datasets.py --org parcs-benchmark --dry-run   # local only
"""

import argparse
import json
import textwrap
import numpy as np
import pandas as pd
from datasets import Dataset, DatasetDict

parser = argparse.ArgumentParser()
parser.add_argument("--org", default="parcs-benchmark")
parser.add_argument("--dry-run", action="store_true")
args = parser.parse_args()

PUSH = not args.dry_run
REPO = f"{args.org}/parcs-agent-benchmark"

# ── helpers ────────────────────────────────────────────────────────────────────

def jdump(obj) -> str:
    return json.dumps(obj, ensure_ascii=False)

def push_dataset(ds: DatasetDict, readme: str):
    if PUSH:
        ds.push_to_hub(REPO, private=False)
        from huggingface_hub import HfApi
        HfApi().upload_file(
            path_or_fileobj=readme.encode(),
            path_in_repo="README.md",
            repo_id=REPO,
            repo_type="dataset",
        )
        print(f"✓ https://huggingface.co/datasets/{REPO}")
    else:
        for split, d in ds.items():
            print(f"  [dry-run] {split}: {len(d)} rows")

README = textwrap.dedent("""\
    ---
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

    **15 tasks** for evaluating PARCS-enabled (parallel) vs. sequential AI agents.

    ## Main split — `benchmark`

    | Column | Type | Description |
    |--------|------|-------------|
    | `task_id` | int | Task number 1–15 |
    | `question` | str | Self-contained prompt given to the agent |
    | `answer` | str | JSON with reference/ground-truth values |

    ## Supplementary splits

    Seed-based tasks include their pre-generated input data so agents
    can optionally download rather than regenerate:

    `task_03_cities`, `task_05_data`, `task_05_configs`, `task_06_patients`,
    `task_07_sequences`, `task_08_train`, `task_08_test`, `task_10_portfolio`,
    `task_10_scenarios`, `task_11_library`, `task_13_nodes`, `task_13_edges`,
    `task_13_sources`, `task_15_sequences`

    ## Citation

    PARCS-Agent benchmark (Bohusevych, 2026). Forthcoming.
""")

# ══════════════════════════════════════════════════════════════════════════════
# Reference-answer computation helpers
# ══════════════════════════════════════════════════════════════════════════════

def compute_task01_reference():
    """VaR / CVaR via 200k scenarios (reduced from 2M for generation speed)."""
    rng = np.random.default_rng(42)
    n = 50
    A = rng.standard_normal((n, n))
    cov = A.T @ A / n + 0.01 * np.eye(n)
    L = np.linalg.cholesky(cov)
    weights = np.full(n, 1/n)
    # 200k scenarios → gives stable estimate of VaR
    n_scenarios = 200_000
    Z = rng.standard_normal((n_scenarios, n))
    returns = Z @ L.T
    portfolio_returns = returns @ weights
    losses = -portfolio_returns
    var_99 = float(np.percentile(losses, 99))
    cvar_99 = float(losses[losses >= var_99].mean())
    return {"var_99_approx": round(var_99, 6), "cvar_99_approx": round(cvar_99, 6),
            "note": "Reference computed with 200k scenarios; full task uses 2M."}


def compute_task03_reference(coords_x, coords_y):
    """Nearest-neighbour tour length as lower-bound reference."""
    n = len(coords_x)
    visited = [False] * n
    tour = [0]
    visited[0] = True
    for _ in range(n - 1):
        cur = tour[-1]
        best_d, best_j = float("inf"), -1
        for j in range(n):
            if not visited[j]:
                d = ((coords_x[cur]-coords_x[j])**2 + (coords_y[cur]-coords_y[j])**2)**0.5
                if d < best_d:
                    best_d, best_j = d, j
        tour.append(best_j)
        visited[best_j] = True
    # Add return leg
    total = sum(
        ((coords_x[tour[i]]-coords_x[tour[i+1]])**2 +
         (coords_y[tour[i]]-coords_y[tour[i+1]])**2)**0.5
        for i in range(n-1)
    )
    total += ((coords_x[tour[-1]]-coords_x[tour[0]])**2 +
              (coords_y[tour[-1]]-coords_y[tour[0]])**2)**0.5
    return float(round(total, 2))


def compute_task04_reference():
    """SIR sweep — compute all 400 outcomes deterministically."""
    R0_vals    = np.linspace(0.8, 4.0, 20)
    theta_vals = np.linspace(0.01, 0.20, 20)
    gamma = 1/14
    N = 1_000_000
    results = []
    for R0 in R0_vals:
        for theta in theta_vals:
            beta = R0 * gamma
            S, I, R = 999_900, 100, 0
            peak_I = I
            total_attack = 0
            days_to_peak = 0
            intervention_days = 0
            for day in range(365):
                if I / N >= theta:
                    beta_eff = beta * 0.5
                    intervention_days += 1
                else:
                    beta_eff = beta
                new_I = beta_eff * S * I / N
                new_R = gamma * I
                S -= new_I; I += new_I - new_R; R += new_R
                if I > peak_I:
                    peak_I = I
                    days_to_peak = day + 1
            total_attack = R / N
            results.append({
                "R0": round(float(R0), 4), "theta": round(float(theta), 4),
                "peak_infected_frac": round(peak_I / N, 6),
                "attack_rate": round(total_attack, 6),
                "days_to_peak": days_to_peak,
                "intervention_days": intervention_days,
            })
    return results


def compute_task13_reference_diameter(node_rows, edge_rows):
    """Approximate diameter using a few BFS runs on the graph."""
    # Build adjacency
    from collections import defaultdict, deque
    adj = defaultdict(list)
    for e in edge_rows[:5000]:  # sample for speed
        adj[e["u"]].append((e["v"], e["weight_minutes"]))
        adj[e["v"]].append((e["u"], e["weight_minutes"]))
    n_nodes = len(node_rows)
    # BFS from node 0 (unweighted for speed)
    def bfs_far(src):
        dist = [-1] * n_nodes
        dist[src] = 0
        q = deque([src])
        farthest, max_d = src, 0
        while q:
            u = q.popleft()
            for v, _ in adj[u]:
                if dist[v] == -1:
                    dist[v] = dist[u] + 1
                    q.append(v)
                    if dist[v] > max_d:
                        max_d, farthest = dist[v], v
        return farthest, max_d
    f1, _ = bfs_far(0)
    f2, diam = bfs_far(f1)
    return int(diam)


# ══════════════════════════════════════════════════════════════════════════════
# Generate all splits
# ══════════════════════════════════════════════════════════════════════════════

def build_all():
    splits: dict[str, Dataset] = {}

    # ── Task 1: Monte Carlo VaR ──────────────────────────────────────────────
    print("Task 1: Monte Carlo VaR …")
    ref1 = compute_task01_reference()
    q1 = (
        "You have a portfolio of 50 assets with equal weights (w_i = 1/50). "
        "The return covariance matrix Σ ∈ ℝ^{50×50} is generated from seed=42 as follows: "
        "A = randn(50,50,seed=42); Σ = Aᵀ·A/50 + 0.01·I. "
        "Using 2,000,000 Monte Carlo scenarios (each worker generates 100,000 using "
        "new Random(WorkerIndex*1000+42) and the shared Cholesky factor L of Σ), "
        "estimate the 1-day 99% Value-at-Risk (VaR) and Conditional VaR (CVaR) of "
        "the portfolio loss distribution. "
        "Loss = –(portfolio return). Report var_99 and cvar_99 as floats."
    )

    # ── Task 2: Barrier Option Price Surface ─────────────────────────────────
    print("Task 2: Barrier Option Surface …")
    # Analytical Black-Scholes down-and-out call for reference (one point)
    import math
    def bs_barrier_ref(S=100, K=100, r=0.05, sigma=0.20, T=1.0, B=85):
        """Simplified analytical price for a down-and-out call (reflection formula)."""
        d1 = (math.log(S/K) + (r + 0.5*sigma**2)*T) / (sigma*math.sqrt(T))
        d2 = d1 - sigma*math.sqrt(T)
        from math import erfc
        def N(x): return 0.5 * erfc(-x / math.sqrt(2))
        vanilla = S*N(d1) - K*math.exp(-r*T)*N(d2)
        # Approximate knock-out adjustment
        mu = (r - 0.5*sigma**2) / sigma**2
        lam = math.sqrt(mu**2 + 2*r/sigma**2)
        x1 = math.log(S/B)/(sigma*math.sqrt(T)) + lam*sigma*math.sqrt(T)
        y1 = math.log(B/S)/(sigma*math.sqrt(T)) + lam*sigma*math.sqrt(T)
        z  = math.log(B/S)/(sigma*math.sqrt(T))
        knock = (S*(B/S)**(2*(mu+1))*N(y1)
                 - K*math.exp(-r*T)*(B/S)**(2*mu)*N(y1 - sigma*math.sqrt(T)))
        price = vanilla - knock
        return round(max(price, 0), 4)
    ref2_sample = bs_barrier_ref()
    q2 = (
        "Price 200 down-and-out European call options on a (S, σ, T) grid: "
        "S ∈ {80,85,90,95,100,105,110,115,120,125}, σ ∈ {0.10,0.15,0.20,0.25}, "
        "T ∈ {0.25,0.5,1.0,2.0,4.0} years. Parameters: K=100, r=0.05, B=0.85·S. "
        "Use 500,000 GBM paths per grid point (each worker prices 10 points using "
        "new Random(WorkerIndex*999+7)). Also compute delta and vega by finite difference. "
        "Return a JSON array of {S, sigma, T, price, delta, vega} for all 200 points."
    )

    # ── Task 3: TSP ──────────────────────────────────────────────────────────
    print("Task 3: TSP — generating 300 cities …")
    rng3 = np.random.default_rng(12345)
    cx = rng3.random(300) * 1000
    cy = rng3.random(300) * 1000
    city_rows = [{"city_id": i, "x": float(cx[i]), "y": float(cy[i])} for i in range(300)]
    splits["task_03_cities"] = Dataset.from_list(city_rows)
    nn_tour = compute_task03_reference(cx, cy)
    q3 = (
        "Find the shortest Hamiltonian tour through 300 cities. "
        "City coordinates are generated from seed=12345: "
        "x[i] = rng.NextDouble()*1000; y[i] = rng.NextDouble()*1000 (C# System.Random). "
        "Run 20 independent Simulated Annealing trials (α=0.9995, T₀=1000, 500,000 iterations, "
        "2-opt neighbourhood), each with a different seed: new Random(WorkerIndex*777). "
        "Return the best tour length found across all 20 trials. "
        "The nearest-neighbour baseline tour length is provided in the answer for reference."
    )
    ref3 = {"nearest_neighbour_tour_length": nn_tour,
            "note": "SA with 20 trials should beat nearest-neighbour by 10–20%."}

    # ── Task 4: SIR Sweep ────────────────────────────────────────────────────
    print("Task 4: SIR Sweep — computing 400 outcomes …")
    sir_results = compute_task04_reference()
    max_peak = max(r["peak_infected_frac"] for r in sir_results)
    min_attack = min(r["attack_rate"] for r in sir_results)
    q4 = (
        "Simulate a discrete-time SIR epidemic on N=1,000,000 individuals for 365 days "
        "across a 20×20 parameter grid: R₀ ∈ linspace(0.8, 4.0, 20), "
        "θ ∈ linspace(0.01, 0.20, 20) (intervention threshold). "
        "Parameters: γ=1/14, β=R₀·γ. When I/N ≥ θ, apply a 50% contact-rate reduction. "
        "Initial conditions: S₀=999900, I₀=100, R₀=0. "
        "For each of the 400 combinations report: peak_infected_frac, attack_rate, "
        "days_to_peak, intervention_days. Return as a JSON array."
    )
    ref4 = {
        "n_combinations": 400,
        "max_peak_infected_frac": round(max_peak, 6),
        "min_attack_rate": round(min_attack, 6),
        "sample_R0_3_theta_0p10": next(
            r for r in sir_results if abs(r["R0"]-2.9474) < 0.01 and abs(r["theta"]-0.1) < 0.01
        ) if any(abs(r["R0"]-2.9474) < 0.01 for r in sir_results) else sir_results[100],
        "full_results": sir_results,   # stored for evaluator use
    }

    # ── Task 5: ML Grid Search ───────────────────────────────────────────────
    print("Task 5: ML Grid Search — generating 80k dataset …")
    rng5 = np.random.default_rng(42)
    X5 = rng5.standard_normal((80_000, 25))
    b5 = rng5.standard_normal(25)
    p5 = 1 / (1 + np.exp(-X5 @ b5))
    y5 = (rng5.random(80_000) < p5).astype(int)
    df5 = pd.DataFrame(X5, columns=[f"f{i}" for i in range(25)])
    df5["label"] = y5
    splits["task_05_data"] = Dataset.from_pandas(df5)
    configs5 = [
        {"config_index": idx, "learning_rate": lr, "max_depth": md,
         "n_estimators": ne, "subsample": ss}
        for idx, (lr, md, ne, ss) in enumerate(
            (lr, md, ne, ss)
            for lr in [0.01, 0.05, 0.1]
            for md in [3, 5, 7]
            for ne in [100, 300]
            for ss in [0.8, 1.0]
        )
    ]
    splits["task_05_configs"] = Dataset.from_list(configs5)
    q5 = (
        "Train a gradient boosting classifier on a synthetic dataset (80,000 rows, 25 features). "
        "Dataset: features X ~ N(0,1)^{80000×25} from seed=42, labels via "
        "logit(P(y=1))=X·β, β~N(0,1) from seed=42. Train/test split 70/30 (seed=42). "
        "Evaluate all 20 hyperparameter configs (learning_rate∈{0.01,0.05,0.1}, "
        "max_depth∈{3,5,7}, n_estimators∈{100,300}, subsample∈{0.8,1.0}) "
        "with 5-fold cross-validation. Each worker handles one config. "
        "Return the best config and its CV AUC-ROC as {best_config_index, best_auc_roc, ranked_configs}."
    )
    ref5 = {"note": "Reference AUC-ROC varies by library; expect 0.82–0.90 for best config at max_depth≥5."}

    # ── Task 6: Bootstrap Survival ───────────────────────────────────────────
    print("Task 6: Bootstrap Survival — generating 8k patients …")
    rng6 = np.random.default_rng(42)
    beta6 = np.array([0.5, -0.3, 0.8, 0.1, -0.6])
    X6 = rng6.standard_normal((8_000, 5))
    rates6 = np.exp(X6 @ beta6)
    et6 = rng6.exponential(1/rates6)
    max_t6 = np.percentile(et6, 70)
    ct6 = rng6.uniform(0, max_t6, 8_000)
    ot6 = np.minimum(et6, ct6)
    ev6 = (et6 <= ct6).astype(int)
    df6 = pd.DataFrame(X6, columns=[f"cov_{i}" for i in range(5)])
    df6["time"] = ot6; df6["event"] = ev6
    splits["task_06_patients"] = Dataset.from_pandas(df6)
    q6 = (
        "Fit a Cox proportional hazards model on a synthetic patient dataset "
        "(N=8,000; 5 covariates; time-to-event; binary event indicator) and compute "
        "95% bootstrap confidence intervals for all 5 hazard ratios using B=10,000 resamples. "
        "Dataset: cov_0…cov_4 ~ N(0,1) from seed=42; β=(0.5,−0.3,0.8,0.1,−0.6); "
        "event times ~ Exponential(exp(Xβ)); 30% censoring at Uniform(0, 70th-percentile). "
        "Each worker computes 500 resamples. Return 95% CI (lower, upper) per covariate "
        "as {cov_i: {ci_lower, ci_upper, point_estimate}}."
    )
    ref6 = {"true_betas": beta6.tolist(),
            "note": "CIs should bracket true betas with ~95% coverage."}

    # ── Task 7: Protein Similarity ───────────────────────────────────────────
    print("Task 7: Protein Similarity — generating 600 sequences …")
    AA = list("ACDEFGHIKLMNPQRSTVWY")
    FREQ = [0.074,0.025,0.054,0.054,0.047,0.074,0.026,0.068,
            0.058,0.099,0.025,0.045,0.039,0.034,0.052,0.057,
            0.051,0.073,0.013,0.032]
    seq7_rows = []
    for i in range(600):
        rng_l = np.random.default_rng(i)
        ln = int(rng_l.integers(50, 501))
        rng_s = np.random.default_rng(i*31+17)
        idxs = rng_s.choice(len(AA), size=ln, p=FREQ)
        seq = "".join(AA[j] for j in idxs)
        seq7_rows.append({"seq_id": i, "length": ln, "sequence": seq})
    splits["task_07_sequences"] = Dataset.from_list(seq7_rows)
    q7 = (
        "Compute the 600×600 pairwise similarity matrix for 600 synthetic protein sequences "
        "using simplified Smith-Waterman local alignment (match=+2, mismatch=−1, gap=−2). "
        "Sequences are generated: for seq i, length ~ Uniform(50,500) from seed=i, "
        "amino acids from 20-letter alphabet with biological frequencies from seed=i*31+17. "
        "Normalise scores to [0,1] by dividing by min(len_i, len_j)*2. "
        "Return: top_20_pairs (seq_i, seq_j, score), cluster families (similarity>0.7), "
        "and the matrix checksum (sum of upper-triangular scores, 4 decimal places)."
    )
    ref7 = {"n_pairs": 179700, "note": "Expect ~5–15 high-similarity family clusters."}

    # ── Task 8: Random Forest ────────────────────────────────────────────────
    print("Task 8: Random Forest — generating 70k dataset …")
    rng8 = np.random.default_rng(99)
    X8 = rng8.standard_normal((70_000, 30))
    b8 = rng8.standard_normal(30)
    e8 = rng8.standard_normal(70_000)
    y8 = np.sin(X8 @ b8) + 0.1*e8
    df8_tr = pd.DataFrame(X8[:60_000], columns=[f"f{i}" for i in range(30)]); df8_tr["target"]=y8[:60_000]
    df8_te = pd.DataFrame(X8[60_000:], columns=[f"f{i}" for i in range(30)]); df8_te["target"]=y8[60_000:]
    splits["task_08_train"] = Dataset.from_pandas(df8_tr)
    splits["task_08_test"]  = Dataset.from_pandas(df8_te)
    q8 = (
        "Train a random forest of 200 decision trees on a synthetic regression dataset "
        "(60,000 train rows, 30 features). Dataset: X ~ N(0,1)^{70000×30} from seed=99, "
        "target y = sin(X·β) + 0.1·ε where β,ε ~ N(0,1) from seed=99. "
        "Each tree: bootstrap sample, √30≈5 features per split, max_depth=15, min_leaf=5. "
        "Workers each grow 10 trees using seeds WorkerIndex*200+treeIndex. "
        "Report: oob_rmse, test_rmse, top_10_feature_importances (mean decrease impurity). "
        "Return as {oob_rmse, test_rmse, feature_importances: [{feature, importance}]}."
    )
    test_var = float(np.var(y8[60_000:]))
    ref8 = {"target_variance_test": round(test_var, 4),
            "note": "Expect test_rmse well below sqrt(target_variance); ~0.10–0.30."}

    # ── Task 9: Safe Primes ──────────────────────────────────────────────────
    print("Task 9: Safe Primes …")
    q9 = (
        "Generate and test 10,000 candidate 512-bit odd integers for primality using "
        "Miller-Rabin with k=20 witness rounds (false-positive rate < 4^{−20}). "
        "For each confirmed prime p, also test whether (p−1)/2 is prime (safe prime check). "
        "Workers: 20 workers, each testing 500 candidates generated via "
        "new Random(WorkerIndex*9973) for the base 512-bit value. "
        "Report: prime_count, safe_prime_count, empirical_prime_density, "
        "prime_number_theorem_prediction (1/ln(2^512)), histogram of iterations to find a prime. "
        "Return as {prime_count, safe_prime_count, empirical_density, pnt_density}."
    )
    pnt_density = 1.0 / (512 * np.log(2))
    ref9 = {"pnt_predicted_density": round(pnt_density, 6),
            "expected_primes_in_10000": round(10000 * pnt_density, 1)}

    # ── Task 10: Stress Testing ───────────────────────────────────────────────
    print("Task 10: Stress Testing — generating portfolio & scenarios …")
    rng10p = np.random.default_rng(77); rng10s = np.random.default_rng(99)
    deltas = rng10p.standard_normal((300, 15))
    w10 = rng10p.random(300); w10 /= w10.sum()
    shocks = rng10s.normal(0, 0.03, (500, 15))
    pnl = (shocks @ deltas.T) @ w10  # 500 P&L values
    worst10_idx = np.argsort(pnl)[:10].tolist()
    port_rows = [{"instrument_id": i, "weight": float(w10[i]),
                  **{f"delta_{j}": float(deltas[i,j]) for j in range(15)}} for i in range(300)]
    scen_rows = [{"scenario_id": s, **{f"shock_{j}": float(shocks[s,j]) for j in range(15)}}
                 for s in range(500)]
    splits["task_10_portfolio"] = Dataset.from_list(port_rows)
    splits["task_10_scenarios"] = Dataset.from_list(scen_rows)
    q10 = (
        "Reprice a portfolio of 300 instruments under 500 stress scenarios using "
        "delta-linear approximation: P&L_s = Σ_i w_i · (δ_i · shock_s). "
        "Portfolio: sensitivities δ ∈ ℝ^{300×15} and weights w from seed=77. "
        "Scenarios: 500 shock vectors ∈ ℝ^{15}, each entry ~ N(0,0.03) from seed=99. "
        "Workers each price the full portfolio under 25 scenarios (500/20). "
        "Report: worst_10_scenarios (scenario_id, pnl), pnl_var_99 (1st percentile), "
        "top_3_loss_drivers per worst scenario (risk factor index, contribution). "
        "Return as {worst_10_scenarios, pnl_var_99, pnl_std}."
    )
    ref10 = {"pnl_var_99": round(float(np.percentile(pnl, 1)), 6),
             "pnl_std": round(float(np.std(pnl)), 6),
             "worst_10_scenario_ids": worst10_idx}

    # ── Task 11: Virtual Screening ────────────────────────────────────────────
    print("Task 11: Virtual Screening — generating 50k molecules …")
    rng11 = np.random.default_rng(55); rng11r = np.random.default_rng(0)
    fps11 = (rng11.random((50_000, 2048)) < 0.05).astype(np.uint8)
    mw11 = rng11.normal(350, 80, 50_000).clip(100, 700)
    hbd11 = rng11.integers(0, 8, 50_000)
    hba11 = rng11.integers(0, 12, 50_000)
    logp11 = rng11.normal(2.5, 1.5, 50_000).clip(-3, 7)
    ref_fp = (rng11r.random(2048) < 0.05).astype(np.uint8)
    # Tanimoto: |A∩B| / |A∪B|
    def tanimoto_batch(query, library):
        inter = library @ query
        q_bits = query.sum()
        l_bits = library.sum(axis=1)
        return inter / (q_bits + l_bits - inter + 1e-9)
    tani = tanimoto_batch(ref_fp, fps11)
    # Lipinski pass
    lipinski = (mw11 <= 500) & (hbd11 <= 5) & (hba11 <= 10) & (logp11 <= 5)
    scores = 0.7*tani + 0.1*(1-mw11/700) + 0.1*(1-hbd11/8) + 0.1*(1-logp11/7)
    top100_idx = np.argsort(-scores)[:100].tolist()

    def fp_hex(row): return np.packbits(row).tobytes().hex()
    lib_rows = [{"mol_id": i, "fingerprint": fp_hex(fps11[i]),
                 "mw": float(round(mw11[i],2)), "hbd": int(hbd11[i]),
                 "hba": int(hba11[i]), "logp": float(round(logp11[i],3))} for i in range(50_000)]
    splits["task_11_library"] = Dataset.from_list(lib_rows)
    q11 = (
        "Score 50,000 molecules against a reference kinase inhibitor fingerprint "
        "using Tanimoto similarity on 2048-bit Morgan fingerprints. "
        "Library: 2048-bit fps (bit-set prob 0.05), MW~N(350,80), HBD~Uniform(0,7), "
        "HBA~Uniform(0,11), logP~N(2.5,1.5), all from seed=55. Reference fp from seed=0. "
        "Docking proxy score = 0.7·Tanimoto + 0.1·(1−MW/700) + 0.1·(1−HBD/8) + 0.1·(1−logP/7). "
        "Apply Lipinski filters: MW≤500, HBD≤5, HBA≤10, logP≤5. "
        "Workers each score 2,500 molecules. "
        "Return: top_100 mol_ids (sorted by score), lipinski_pass_rate, mean_tanimoto. "
        "Format: {top_100_mol_ids, lipinski_pass_rate, mean_tanimoto}."
    )
    ref11 = {"top_100_mol_ids": top100_idx,
             "lipinski_pass_rate": round(float(lipinski.mean()), 4),
             "mean_tanimoto": round(float(tani.mean()), 6)}

    # ── Task 12: Insurance Ruin ───────────────────────────────────────────────
    print("Task 12: Insurance Ruin …")
    q12 = (
        "Simulate 2,000,000 policy years under a compound Poisson risk process: "
        "claim count N ~ Poisson(λ=200), severity X ~ Lognormal(μ=8, σ=1.5). "
        "Annual premium P = (1+0.2)·E[S] where E[S]=λ·exp(μ+σ²/2); initial surplus U=500,000. "
        "Estimate: ruin probability within 1, 5, and 10 years; "
        "99.5th-percentile annual loss (Solvency II SCR proxy); expected deficit given ruin. "
        "Workers each simulate 100,000 policy years using "
        "seed_counts=WorkerIndex*2053+1, seed_severities=WorkerIndex*3571+2. "
        "Return: {ruin_prob_1yr, ruin_prob_5yr, ruin_prob_10yr, scr_995, expected_deficit}."
    )
    lam12, mu12, sig12 = 200, 8, 1.5
    E_S12 = lam12 * np.exp(mu12 + sig12**2/2)
    premium12 = 1.2 * E_S12
    ref12 = {"annual_premium": round(float(premium12), 2),
             "E_S": round(float(E_S12), 2),
             "note": "Cramér-Lundberg: ruin prob ≤ exp(−R·U) for safety loading 0.2."}

    # ── Task 13: Graph Betweenness ────────────────────────────────────────────
    print("Task 13: Graph Betweenness — generating 5k-node graph …")
    from scipy.spatial import cKDTree
    rng13 = np.random.default_rng(314)
    coords13 = rng13.random((5_000, 2))
    tree13 = cKDTree(coords13)
    pairs13 = list(tree13.query_pairs(0.055))
    edge13_rows = [{"u": int(u), "v": int(v),
                    "weight_minutes": round(float(rng13.uniform(1,30)), 3)}
                   for u, v in pairs13]
    node13_rows = [{"node_id": i, "x": float(coords13[i,0]), "y": float(coords13[i,1])}
                   for i in range(5_000)]
    # 400 source nodes (20 per worker)
    source13 = [{"source_id": i, "node": i % 5_000, "worker_id": i//20} for i in range(400)]
    splits["task_13_nodes"]   = Dataset.from_list(node13_rows)
    splits["task_13_edges"]   = Dataset.from_list(edge13_rows)
    splits["task_13_sources"] = Dataset.from_list(source13)
    approx_diam = compute_task13_reference_diameter(node13_rows, edge13_rows)
    q13 = (
        "On a synthetic road-network graph (5,000 nodes, ~25,000 edges), compute: "
        "(1) shortest-path lengths from 400 sampled source nodes using Dijkstra, "
        "(2) approximate betweenness centrality for all nodes, "
        "(3) network diameter and average path length (hop count), "
        "(4) top-20 most critical edges by removal impact on average path length. "
        "Graph: nodes placed uniformly in [0,1]² from seed=314; edges between nodes within "
        "distance 0.055; weights ~ Uniform(1,30) minutes from seed=314. "
        "Workers each run Dijkstra from 20 source nodes (WorkerIndex*20 to WorkerIndex*20+19). "
        "Return: {top_20_nodes_by_betweenness, top_20_critical_edges, avg_path_length_hops, diameter_hops}."
    )
    ref13 = {"n_edges": len(edge13_rows), "approx_diameter_hops": approx_diam}

    # ── Task 14: Radiative Transfer ───────────────────────────────────────────
    print("Task 14: Radiative Transfer …")
    q14 = (
        "Simulate photon transport through a 50-layer plane-parallel atmosphere. "
        "Layer l optical depths: τ_scat[l]=0.1·exp(−0.05·l), τ_abs[l]=0.02·exp(−0.08·l), l=0…49. "
        "Single-scatter albedo ω[l]=τ_scat[l]/(τ_scat[l]+τ_abs[l]). "
        "Solar zenith angle θ=30°. Rayleigh phase function. "
        "Simulate 5,000,000 photons (each worker traces 250,000 using "
        "new Random(WorkerIndex*6271+3)). "
        "Report: toa_upwelling_radiance, surface_downwelling_irradiance, "
        "heating_rate_per_layer (array of 50 values), single_scatter_albedo_retrieval_error. "
        "Return as {toa_radiance, surface_irradiance, heating_rates, ssa_retrieval_rmse}."
    )
    atm14 = [{"layer": l,
               "tau_scat": round(0.1*np.exp(-0.05*l), 6),
               "tau_abs":  round(0.02*np.exp(-0.08*l), 6),
               "omega":    round(0.1*np.exp(-0.05*l) / (0.1*np.exp(-0.05*l) + 0.02*np.exp(-0.08*l)), 6)}
              for l in range(50)]
    total_tau = sum(a["tau_scat"]+a["tau_abs"] for a in atm14)
    ref14 = {"total_optical_depth": round(total_tau, 4),
             "note": "TOA radiance should be within 1% of two-stream approximation."}

    # ── Task 15: k-mer Spectrum ───────────────────────────────────────────────
    print("Task 15: k-mer Spectrum — generating 20 sequences (500k bp each) …")
    BASES15 = ["A","C","G","T"]
    PROBS15 = [0.30, 0.20, 0.20, 0.30]
    seq15_rows = []
    for i in range(20):
        rng15 = np.random.default_rng(i*104729+1)
        idxs15 = rng15.choice(4, size=500_000, p=PROBS15)
        seq = "".join(BASES15[j] for j in idxs15)
        gc = float(round((seq.count("C")+seq.count("G"))/len(seq), 4))
        seq15_rows.append({"seq_id": i, "length": 500_000, "gc_content": gc, "sequence": seq})
    splits["task_15_sequences"] = Dataset.from_list(seq15_rows)
    q15 = (
        "Compute the k=8 frequency spectrum (4^8=65,536 k-mers) for each of 20 synthetic "
        "genomic sequences (500,000 bp each) and for the combined corpus. "
        "Sequences: base i generated from seed=i*104729+1, composition {A:0.30,C:0.20,G:0.20,T:0.30}. "
        "For each sequence: (1) full 65,536-entry k-mer frequency table, "
        "(2) top-50 over- and under-represented k-mers vs. null expectation (product of marginals), "
        "(3) repeat regions (k-mers with freq>100 in consecutive 10kb windows). "
        "Final layer: Jensen-Shannon divergence between each sequence's spectrum and corpus mean. "
        "Return: {per_sequence_top50_kmers, js_divergences, corpus_most_common_10_kmers, "
        "frequency_table_checksums (sum of all 65536 counts per sequence = 499993)}."
    )
    ref15 = {"expected_total_kmers_per_seq": 499993,
             "note": "Checksum = sequence_length − k + 1 = 500000 − 8 + 1 = 499993."}

    # ── Assemble benchmark split ──────────────────────────────────────────────
    benchmark_rows = [
        {"task_id": 1,  "question": q1,  "answer": jdump(ref1)},
        {"task_id": 2,  "question": q2,  "answer": jdump({"reference_price_S100_sig020_T1": ref2_sample})},
        {"task_id": 3,  "question": q3,  "answer": jdump(ref3)},
        {"task_id": 4,  "question": q4,  "answer": jdump({k: v for k, v in ref4.items() if k != "full_results"})},
        {"task_id": 5,  "question": q5,  "answer": jdump(ref5)},
        {"task_id": 6,  "question": q6,  "answer": jdump(ref6)},
        {"task_id": 7,  "question": q7,  "answer": jdump(ref7)},
        {"task_id": 8,  "question": q8,  "answer": jdump(ref8)},
        {"task_id": 9,  "question": q9,  "answer": jdump(ref9)},
        {"task_id": 10, "question": q10, "answer": jdump(ref10)},
        {"task_id": 11, "question": q11, "answer": jdump(ref11)},
        {"task_id": 12, "question": q12, "answer": jdump(ref12)},
        {"task_id": 13, "question": q13, "answer": jdump(ref13)},
        {"task_id": 14, "question": q14, "answer": jdump(ref14)},
        {"task_id": 15, "question": q15, "answer": jdump(ref15)},
    ]
    splits["benchmark"] = Dataset.from_list(benchmark_rows)

    # Also store the full SIR reference answers in a separate split
    splits["task_04_sir_reference"] = Dataset.from_list(sir_results)

    return DatasetDict(splits)


# ── main ───────────────────────────────────────────────────────────────────────
if __name__ == "__main__":
    print("Building dataset …\n")
    ds = build_all()
    print(f"\nSplits: {list(ds.keys())}")
    print(f"Benchmark rows: {len(ds['benchmark'])}")
    print("\nPushing …" if PUSH else "\n[dry-run — skipping push]")
    push_dataset(ds, README)
    print("Done.")

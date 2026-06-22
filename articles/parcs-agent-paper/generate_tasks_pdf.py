from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from reportlab.lib.units import cm
from reportlab.lib import colors
from reportlab.platypus import (
    SimpleDocTemplate, Paragraph, Spacer, Table, TableStyle,
    HRFlowable, PageBreak, KeepTogether
)
from reportlab.lib.enums import TA_CENTER, TA_JUSTIFY

OUTPUT = "/sessions/sweet-pensive-thompson/mnt/parcs7/articles/parcs-agent-paper/benchmark_tasks_v3.pdf"

# ── Styles ─────────────────────────────────────────────────────────────────────
styles = getSampleStyleSheet()

def make_style(name, parent='Normal', **kw):
    return ParagraphStyle(name, parent=styles[parent], **kw)

title_style    = make_style('DocTitle',    'Title',   fontSize=22, leading=28, spaceAfter=6, alignment=TA_CENTER)
subtitle_style = make_style('DocSubtitle','Normal',   fontSize=12, leading=16, spaceAfter=4, textColor=colors.HexColor('#444444'), alignment=TA_CENTER)
meta_style     = make_style('Meta',       'Normal',   fontSize=10, leading=14, spaceAfter=2, textColor=colors.HexColor('#666666'), alignment=TA_CENTER)
h1_style       = make_style('H1',         'Heading1', fontSize=14, leading=18, spaceBefore=18, spaceAfter=6,  textColor=colors.HexColor('#1a237e'))
body_style     = make_style('Body',       'Normal',   fontSize=10, leading=15, spaceAfter=6,  alignment=TA_JUSTIFY)
label_style    = make_style('Label',      'Normal',   fontSize=9,  leading=13, spaceAfter=3,  textColor=colors.HexColor('#1565c0'), fontName='Helvetica-Bold')
value_style    = make_style('Value',      'Normal',   fontSize=10, leading=14, spaceAfter=5,  alignment=TA_JUSTIFY)
data_label     = make_style('DataLabel',  'Normal',   fontSize=9,  leading=13, spaceAfter=3,  textColor=colors.HexColor('#2e7d32'), fontName='Helvetica-Bold')
data_value     = make_style('DataValue',  'Normal',   fontSize=9.5,leading=14, spaceAfter=5,  alignment=TA_JUSTIFY, textColor=colors.HexColor('#1b5e20'))
caption_style  = make_style('Caption',    'Normal',   fontSize=9,  leading=12, textColor=colors.HexColor('#555555'), alignment=TA_CENTER, spaceAfter=12)
link_label     = make_style('LinkLabel',  'Normal',   fontSize=9,  leading=13, spaceAfter=3,  textColor=colors.HexColor('#6a1b9a'), fontName='Helvetica-Bold')
link_value     = make_style('LinkValue',  'Normal',   fontSize=9,  leading=13, spaceAfter=5,  textColor=colors.HexColor('#6a1b9a'))
prompt_label   = make_style('PromptLbl',  'Normal',   fontSize=9,  leading=13, spaceAfter=3,  textColor=colors.HexColor('#4a148c'), fontName='Helvetica-Bold')
prompt_value   = make_style('PromptVal',  'Normal',   fontSize=9,  leading=14, spaceAfter=5,  alignment=TA_JUSTIFY, textColor=colors.HexColor('#1a237e'))

ACCENT  = colors.HexColor('#1a237e')
LIGHT   = colors.HexColor('#e8eaf6')
MID     = colors.HexColor('#c5cae9')
GREEN_L = colors.HexColor('#e8f5e9')
GREEN_B = colors.HexColor('#2e7d32')

# ── Task data ──────────────────────────────────────────────────────────────────
# data_strategy values:
#   "seed"   – workers regenerate the full synthetic dataset from a fixed seed
#   "layer0" – a dedicated Layer 0 worker generates and passes data via previousLayerResultJson
#   "params" – data is fully defined by mathematical parameters; no external dataset needed

tasks = [
    {
        "id": 1,
        "title": "Monte Carlo Value-at-Risk for a Multi-Asset Portfolio",
        "domain": "Quantitative Finance",
        "definition": (
            "Given a portfolio of 50 assets with weights <b>w</b> \u2208 \u211d<super>50</super> "
            "and a return covariance matrix <b>\u03a3</b> \u2208 \u211d<super>50\u00d750</super>, "
            "estimate the 1-day 99% Value-at-Risk and Conditional VaR by simulating 2,000,000 "
            "correlated market scenarios via Cholesky decomposition of <b>\u03a3</b>. "
            "VaR is the 1st percentile of the empirical loss distribution."
        ),
        "data_strategy": "params",
        "data": (
            "No external dataset. Portfolio weights <b>w</b><sub>i</sub> = 1/50 (equal-weight). "
            "Covariance matrix <b>\u03a3</b> generated once by each worker from seed = 42 as: "
            "A = randn(50,50, seed=42); \u03a3 = A\u1d40A / 50 + 0.01\u00b7I. "
            "Each worker generates its 100,000 scenarios using "
            "<tt>new Random(WorkerIndex * 1000 + 42)</tt> and the shared Cholesky factor L."
        ),
        "parallelism": (
            "Each worker simulates 100,000 of the 2,000,000 scenarios and returns its loss vector. "
            "A final single-worker layer pools all 20 loss vectors and computes percentiles."
        ),
        "applicability": (
            "Required daily by every major bank under Basel III internal models. Sequential "
            "simulation takes ~20 minutes; 20 parallel workers finish in ~1 minute."
        ),
        "metric": "99% VaR, 99% CVaR, empirical loss distribution histogram.",
        "seq": "~20 min", "par": "~1 min",
    },
    {
        "id": 2,
        "title": "Barrier Option Price Surface via Monte Carlo",
        "domain": "Derivatives Pricing",
        "definition": (
            "Price 200 down-and-out European call options across a grid of (S, \u03c3, T) "
            "using geometric Brownian motion with 500,000 simulated paths per grid point. "
            "Barrier B = 0.85\u00b7S. Compute the full price surface and first-order Greeks "
            "(delta, vega) by central finite difference."
        ),
        "data_strategy": "params",
        "data": (
            "No external dataset. Grid defined by parameters: S \u2208 {80,85,90,95,100,105,110,115,120,125}, "
            "\u03c3 \u2208 {0.10,0.15,0.20,0.25}, T \u2208 {0.25,0.5,1.0,2.0,4.0} \u2014 "
            "200 combinations in total. Risk-free rate r = 0.05. Strike K = 100. "
            "Each worker receives its 10 grid points via <tt>parametersJson</tt> and draws "
            "paths using <tt>new Random(WorkerIndex * 999 + 7)</tt>."
        ),
        "parallelism": (
            "Each worker prices 10 grid points (200 / 20), running 500,000 paths per point. "
            "A final layer assembles the 200-entry price surface."
        ),
        "applicability": (
            "Used by options market-makers to maintain real-time hedging tables. "
            "Sequential evaluation takes ~40 minutes; parallel ~2 minutes."
        ),
        "metric": "Price surface within 0.5% of Black-Scholes closed-form, full delta/vega grid.",
        "seq": "~40 min", "par": "~2 min",
    },
    {
        "id": 3,
        "title": "Traveling Salesman \u2014 Parallel Simulated Annealing",
        "domain": "Combinatorial Optimisation / Logistics",
        "definition": (
            "Find the shortest Hamiltonian tour through 300 cities using Simulated Annealing "
            "with a 2-opt neighbourhood, geometric cooling T(k) = T<sub>0</sub>\u00b7\u03b1<super>k</super> "
            "(\u03b1 = 0.9995, 500,000 iterations). Run 20 independent trials with different "
            "random seeds; return the best tour found."
        ),
        "data_strategy": "dataset",
        "data": (
            "300 city coordinates pre-generated and published on HuggingFace. "
            "Fields: city_id (int), x (float), y (float). "
            "Pass the dataset URL as <tt>datasetUrl</tt> to <tt>run_layer</tt>; "
            "workers read it with <tt>File.ReadAllText(input.DatasetPath!)</tt> and parse JSONL. "
            "Each worker runs one independent SA trial with seed = WorkerIndex × 777."
        ),
        "parallelism": (
            "Each worker runs one independent SA trial. A final layer compares all 20 "
            "best-tour lengths and returns the global minimum."
        ),
        "applicability": (
            "Underpins last-mile delivery routing, field service scheduling, PCB drilling. "
            "One sequential SA run takes ~3 minutes and explores a single trajectory; "
            "20 parallel trials with different seeds find substantially better solutions."
        ),
        "metric": "Best tour length, convergence curve per seed, % improvement over nearest-neighbour.",
        "seq": "1 run (poor quality)", "par": "20 diverse runs",
    },
    {
        "id": 4,
        "title": "Epidemic SIR Parameter Sweep",
        "domain": "Public Health / Epidemiology",
        "definition": (
            "Simulate a discrete-time SIR epidemic on N = 1,000,000 individuals over 365 days. "
            "Sweep a 20\u00d720 grid: R<sub>0</sub> \u2208 [0.8, 4.0] and intervention "
            "threshold \u03b8 \u2208 [0.01, 0.20] (fraction infected triggering a 50% "
            "contact-rate reduction). Compute peak infected fraction, total attack rate, "
            "days to peak, and intervention days for all 400 combinations."
        ),
        "data_strategy": "params",
        "data": (
            "No external dataset. Grid is fully specified by the sweep bounds above. "
            "Initial conditions: S<sub>0</sub> = 999,900, I<sub>0</sub> = 100, R<sub>0</sub> = 0. "
            "Recovery rate \u03b3 = 1/14. Each worker receives its 20 grid points via "
            "<tt>parametersJson</tt> (row index encoded as a string). The simulation is "
            "deterministic \u2014 no random seed required."
        ),
        "parallelism": (
            "Each worker simulates 20 parameter combinations (one row of the 20\u00d720 grid). "
            "A final layer assembles results into a heatmap."
        ),
        "applicability": (
            "Public health agencies use sweeps to evaluate lockdown trigger policies. "
            "400 simulations take ~30 minutes sequentially; 20 workers finish in ~2 minutes."
        ),
        "metric": "Full 20\u00d720 heatmaps of peak infected %, attack rate, days to peak, intervention days.",
        "seq": "~30 min", "par": "~2 min",
    },
    {
        "id": 5,
        "title": "Hyperparameter Grid Search for Gradient Boosting",
        "domain": "Machine Learning",
        "definition": (
            "Train a gradient boosting classifier on a synthetic tabular dataset (80,000 rows, "
            "25 features) for each of 20 hyperparameter configurations: learning rate "
            "\u2208 {0.01, 0.05, 0.1}, max_depth \u2208 {3, 5, 7}, n_estimators \u2208 {100, 300}, "
            "subsample \u2208 {0.8, 1.0}. Evaluate each with 5-fold cross-validation. "
            "Return the best configuration and its CV AUC-ROC."
        ),
        "data_strategy": "dataset",
        "data": (
            "Training dataset (80,000 rows) pre-generated and published on HuggingFace. "
            "Fields: f0\u2026f24 (float), label (0/1). "
            "Pass the dataset URL as <tt>datasetUrl</tt> to <tt>run_layer</tt>. "
            "The 20 configurations are the ordered cross-product of "
            "learning_rate\u2208{0.01,0.05,0.1}, max_depth\u2208{3,5,7}, "
            "n_estimators\u2208{100,300}, subsample\u2208{0.8,1.0}, indexed 0\u201319. "
            "Each worker evaluates one configuration (config_index = WorkerIndex)."
        ),
        "parallelism": (
            "Each worker trains one hyperparameter configuration (all 5 CV folds). "
            "A final layer ranks the 20 results by CV AUC-ROC."
        ),
        "applicability": (
            "Hyperparameter optimisation dominates ML pipeline runtime in production. "
            "Sequential 5-fold CV across 20 configs takes ~60 minutes; parallel ~3 minutes."
        ),
        "metric": "Best CV AUC-ROC, ranked configuration table, training time per configuration.",
        "seq": "~60 min", "par": "~3 min",
    },
    {
        "id": 6,
        "title": "Bootstrap Confidence Intervals for Survival Analysis",
        "domain": "Biostatistics / Clinical Research",
        "definition": (
            "Fit a Cox proportional hazards model on a synthetic patient survival dataset "
            "(N = 8,000, time-to-event, binary event indicator, 5 covariates) and compute "
            "95% bootstrap CIs for all hazard ratios using B = 10,000 resamples. "
            "Each resample draws N patients with replacement and fits the Cox model."
        ),
        "data_strategy": "dataset",
        "data": (
            "Patient dataset (8,000 rows) pre-generated and published on HuggingFace. "
            "Fields: cov_0\u2026cov_4 (float), time (float), event (0/1). "
            "True hazard ratios: \u03b2 = (0.5, \u22120.3, 0.8, 0.1, \u22120.6). "
            "Pass the dataset URL as <tt>datasetUrl</tt> to <tt>run_layer</tt>. "
            "Workers each run 500 bootstrap resamples with seed = WorkerIndex \u00d7 1000 + resample_index."
        ),
        "parallelism": (
            "Each worker runs 500 bootstrap resamples (10,000 / 20). Results \u2014 "
            "coefficient matrices \u2014 are concatenated; a final layer extracts percentiles."
        ),
        "applicability": (
            "Accurate CIs are required for regulatory submission in Phase III clinical trials. "
            "Sequential B = 10,000 bootstrap takes ~20 minutes; parallel ~1 minute."
        ),
        "metric": "95% CI per hazard ratio, bootstrap distribution histograms, comparison to Wald CIs.",
        "seq": "~20 min", "par": "~1 min",
    },
    {
        "id": 7,
        "title": "Protein Sequence All-vs-All Similarity",
        "domain": "Bioinformatics / Drug Discovery",
        "definition": (
            "Compute the full 600\u00d7600 pairwise similarity matrix for 600 synthetic protein "
            "sequences using a simplified Smith-Waterman local alignment scoring "
            "(match +2, mismatch \u22121, gap \u22122). Output the top-20 most similar pairs, "
            "cluster by average-linkage hierarchical clustering, and identify families "
            "with similarity &gt; 0.7."
        ),
        "data_strategy": "dataset",
        "data": (
            "600 protein sequences pre-generated and published on HuggingFace. "
            "Fields: seq_id (int), length (int), sequence (string, 20-letter AA alphabet). "
            "Pass the dataset URL as <tt>datasetUrl</tt> to <tt>run_layer</tt>. "
            "Workers each compute an equal block of the 179,700-pair upper-triangular "
            "similarity matrix (~9,000 pairs per worker at parallelism=20)."
        ),
        "parallelism": (
            "Workers compute blocks of the 179,700-pair upper-triangular matrix "
            "(\u223c9,000 pairs per worker). A final layer merges and ranks results."
        ),
        "applicability": (
            "Used in drug discovery to identify protein homologs as alternative targets. "
            "Sequential computation of 180,000 alignments takes ~35 minutes; parallel ~2 minutes."
        ),
        "metric": "Full similarity matrix, top-20 pairs with scores, cluster dendrogram data.",
        "seq": "~35 min", "par": "~2 min",
    },
    {
        "id": 8,
        "title": "Random Forest Construction from Scratch",
        "domain": "Machine Learning",
        "definition": (
            "Train a random forest of 200 decision trees on a synthetic regression dataset "
            "(60,000 rows, 30 features). Each tree uses a bootstrap sample, "
            "\u221a30 \u2248 5 features per split, max depth = 15, min leaf size = 5. "
            "Report OOB RMSE, feature importance (mean decrease in impurity), "
            "and test predictions on 10,000 held-out rows."
        ),
        "data_strategy": "pipeline",
        "data": (
            "Training (60,000 rows) and test (10,000 rows) datasets pre-generated on HuggingFace. "
            "Fields: f0\u2026f29 (float), target (float). "
            "<b>Two-layer pipeline:</b> "
            "Layer 1 workers grow trees from <tt>task_08_train.jsonl</tt> "
            "(tree seed = WorkerIndex \u00d7 200 + treeIndex); "
            "Layer 2 (1 worker) evaluates the ensemble using <tt>task_08_test.jsonl</tt> "
            "passed as <tt>datasetUrl</tt>."
        ),
        "parallelism": (
            "Each worker grows 10 trees independently (200 / 20). A final layer averages "
            "all 200 trees\u2019 predictions and aggregates feature importance vectors."
        ),
        "applicability": (
            "Random forests are production models in credit scoring, fraud detection, "
            "and insurance pricing. Growing 200 trees sequentially takes ~20 minutes; parallel ~1 minute."
        ),
        "metric": "OOB RMSE, test RMSE, feature importance ranking, agreement with reference.",
        "seq": "~20 min", "par": "~1 min",
    },
    {
        "id": 9,
        "title": "Safe Prime Generation and Primality Testing",
        "domain": "Cryptography / Security Engineering",
        "definition": (
            "Generate and test 10,000 candidate 512-bit odd integers for primality using "
            "Miller-Rabin with k = 20 witness rounds "
            "(false-positive probability &lt; 4<super>\u221220</super>). "
            "For each confirmed prime p, check whether (p\u22121)/2 is also prime (safe prime). "
            "Report counts, empirical prime density vs. prime number theorem prediction, "
            "and distribution of iterations."
        ),
        "data_strategy": "params",
        "data": (
            "No external dataset. Each worker generates its 500 candidate integers using "
            "<tt>new Random(WorkerIndex * 9973)</tt> to produce 512-bit odd numbers. "
            "The Miller-Rabin witnesses are drawn from <tt>new Random(candidate_index)</tt> "
            "for reproducibility. Fully self-contained \u2014 no data transfer."
        ),
        "parallelism": (
            "Workers each test 500 candidates. Embarrassingly parallel \u2014 "
            "no inter-worker communication. A final layer aggregates counts and statistics."
        ),
        "applicability": (
            "Safe prime generation is the bottleneck in RSA and Diffie-Hellman key generation. "
            "Demonstrates throughput difference between sequential and cluster-based cryptography."
        ),
        "metric": "Prime count, safe-prime count, empirical density vs. PNT, iteration histogram.",
        "seq": "~15 min", "par": "~1 min",
    },
    {
        "id": 10,
        "title": "Financial Stress Testing \u2014 Historical Scenario Repricing",
        "domain": "Banking / Risk Management",
        "definition": (
            "Reprice a portfolio of 300 financial instruments under 500 stress scenarios "
            "(percentage shocks to 15 risk factors: equity indices, interest rates, FX, "
            "credit spreads) using delta-linear approximation. Identify the 10 worst "
            "P&amp;L scenarios and attribute losses to primary risk-factor drivers."
        ),
        "data_strategy": "pipeline",
        "data": (
            "Portfolio (300 instruments) and scenario (500 shocks) data pre-generated on HuggingFace. "
            "Portfolio fields: instrument_id, weight, delta_0\u2026delta_14. "
            "Scenario fields: scenario_id, shock_0\u2026shock_14. "
            "<b>Two-layer pipeline:</b> "
            "Layer 1 (1 worker) loads the portfolio from <tt>task_10_portfolio.jsonl</tt> "
            "and passes it via <tt>resultJson</tt>; "
            "Layer 2 workers each price the portfolio under 25 scenarios from "
            "<tt>task_10_scenarios.jsonl</tt> (passed as <tt>datasetUrl</tt>)."
        ),
        "parallelism": (
            "Each worker reprices the portfolio under 25 scenarios (500 / 20). "
            "A final layer assembles the P&amp;L distribution and ranks scenarios."
        ),
        "applicability": (
            "Required quarterly by regulators (FRTB, Basel III) for all trading-book portfolios "
            "at major financial institutions."
        ),
        "metric": "Full P&amp;L distribution, worst-10 scenarios, driver attribution, stressed VaR at 99%.",
        "seq": "~25 min", "par": "~2 min",
    },
    {
        "id": 11,
        "title": "Drug-Like Molecule Virtual Screening",
        "domain": "Drug Discovery / Cheminformatics",
        "definition": (
            "Score 50,000 molecules against a reference kinase inhibitor fingerprint using "
            "Tanimoto similarity on 2048-bit Morgan fingerprints. Apply Lipinski Rule-of-Five "
            "filters (MW \u2264 500, HBD \u2264 5, HBA \u2264 10, logP \u2264 5). "
            "Compute a docking score proxy as a weighted sum of fingerprint similarity "
            "and 4 physicochemical descriptors. Return the top-100 ranked candidates."
        ),
        "data_strategy": "dataset",
        "data": (
            "Library of 50,000 molecules pre-generated and published on HuggingFace. "
            "Fields: mol_id (int), fingerprint (512-char hex, 2048 bits), mw, hbd, hba, logp. "
            "Reference fingerprint: 2048-bit vector generated from seed=0 (bit-set prob 0.05). "
            "Pass the dataset URL as <tt>datasetUrl</tt> to <tt>run_layer</tt>. "
            "Workers each score an equal partition of the library (mol_ids WorkerIndex×2500 … +2499 at parallelism=20)."
        ),
        "parallelism": (
            "Workers each score 2,500 molecules (50,000 / 20). A final layer merges "
            "ranked lists, applies Lipinski filters, and returns the global top-100."
        ),
        "applicability": (
            "Virtual screening reduces wet-lab funnel from millions of compounds to hundreds. "
            "Sequential scoring of 50,000 molecules takes ~15 minutes; parallel ~1 minute."
        ),
        "metric": "Top-100 candidate list, Tanimoto scores, filter-pass rate, score distribution.",
        "seq": "~15 min", "par": "~1 min",
    },
    {
        "id": 12,
        "title": "Insurance Collective Risk Model \u2014 Ruin Probability",
        "domain": "Actuarial Science / Insurance",
        "definition": (
            "Simulate 2,000,000 policy years under a compound Poisson risk process: "
            "claim count N ~ Poisson(\u03bb = 200), severity X ~ Lognormal(\u03bc = 8, \u03c3 = 1.5). "
            "Premium P = (1 + \u03b8)\u00b7E[S] with \u03b8 = 0.2; initial surplus U = 500,000. "
            "Estimate probability of ruin within 1, 5, and 10 years; 99.5th-percentile annual "
            "loss (Solvency II SCR proxy); expected deficit given ruin."
        ),
        "data_strategy": "params",
        "data": (
            "No external dataset. All parameters above fully specify the process. "
            "Each worker simulates 100,000 policy years using "
            "<tt>new Random(WorkerIndex * 2053 + 1)</tt> for claim counts and "
            "<tt>new Random(WorkerIndex * 3571 + 2)</tt> for severities. "
            "Fully self-contained."
        ),
        "parallelism": (
            "Each worker simulates 100,000 policy years (2,000,000 / 20). A final layer "
            "pools results and computes empirical ruin probabilities and percentiles."
        ),
        "applicability": (
            "Used by insurers to determine required capital reserves under Solvency II. "
            "Sequential simulation at 2,000,000 runs takes ~25 minutes."
        ),
        "metric": "Ruin probabilities at 3 horizons, SCR estimate, time-to-ruin distribution, Cram\u00e9r-Lundberg comparison.",
        "seq": "~25 min", "par": "~2 min",
    },
    {
        "id": 13,
        "title": "Large-Scale Graph Betweenness Centrality",
        "domain": "Network Science / Infrastructure Planning",
        "definition": (
            "On a synthetic weighted undirected road-network graph (N = 5,000 nodes, "
            "E = 25,000 edges, weights = travel time in minutes), compute: shortest-path "
            "lengths from 400 sampled source nodes (Dijkstra); approximate betweenness "
            "centrality for all nodes; network diameter and average path length; "
            "and the top-20 most critical edges by removal impact."
        ),
        "data_strategy": "pipeline",
        "data": (
            "Graph (5,000 nodes, ~25,000 edges) pre-generated and published on HuggingFace. "
            "Node fields: node_id, x, y. Edge fields: u, v, weight_minutes. "
            "<b>Two-layer pipeline:</b> "
            "Layer 1 (1 worker) loads nodes from <tt>task_13_nodes.jsonl</tt> "
            "and passes the node coordinate map via <tt>resultJson</tt>; "
            "Layer 2 workers each run Dijkstra from 20 source nodes "
            "(source indices WorkerIndex×20 … +19), loading edges from "
            "<tt>task_13_edges.jsonl</tt> as <tt>datasetUrl</tt>."
        ),
        "parallelism": (
            "Each worker runs Dijkstra from 20 source nodes and accumulates partial "
            "betweenness centrality. A final layer sums centrality vectors globally."
        ),
        "applicability": (
            "Used in urban planning (bottleneck roads), telecom design, and supply chain "
            "resilience analysis."
        ),
        "metric": "Betweenness centrality ranking, top-20 critical edges, average path length, diameter.",
        "seq": "~30 min", "par": "~2 min",
    },
    {
        "id": 14,
        "title": "Monte Carlo Radiative Transfer \u2014 Atmospheric Simulation",
        "domain": "Atmospheric Science / Remote Sensing",
        "definition": (
            "Simulate photon transport through a 50-layer plane-parallel atmosphere. "
            "Each photon is launched at solar zenith angle \u03b8 = 30\u00b0. "
            "At each layer, the photon scatters (Rayleigh phase function), is absorbed "
            "(absorption optical depth \u03c4<sub>abs</sub>), or exits. Simulate 5,000,000 "
            "photons to estimate TOA upwelling radiance, surface downwelling irradiance, "
            "per-layer heating rates, and single-scatter albedo retrieval accuracy."
        ),
        "data_strategy": "params",
        "data": (
            "No external dataset. Atmospheric profile fully specified: "
            "layer optical depths \u03c4<sub>scat</sub>[l] = 0.1\u00b7exp(\u22120.05\u00b7l), "
            "\u03c4<sub>abs</sub>[l] = 0.02\u00b7exp(\u22120.08\u00b7l) for l = 0\u202649. "
            "Single-scatter albedo \u03c9 = \u03c4<sub>scat</sub> / (\u03c4<sub>scat</sub> + \u03c4<sub>abs</sub>). "
            "Each worker traces 250,000 photons using "
            "<tt>new Random(WorkerIndex * 6271 + 3)</tt>."
        ),
        "parallelism": (
            "Workers each trace 250,000 photon paths (5,000,000 / 20). Exit radiances "
            "and layer absorption events are accumulated in a final layer."
        ),
        "applicability": (
            "Used to calibrate satellite-borne radiometers (MODIS, Sentinel) and in "
            "climate model parameterisation. Sequential 5 M-photon simulation takes ~30 minutes."
        ),
        "metric": "TOA radiance within 1% of benchmark, correct heating rate profile, retrieval accuracy.",
        "seq": "~30 min", "par": "~2 min",
    },
    {
        "id": 15,
        "title": "Parallel Genome k-mer Frequency Spectrum",
        "domain": "Bioinformatics / Genomics",
        "definition": (
            "Compute the complete k = 8 frequency spectrum (4<super>8</super> = 65,536 k-mers) "
            "for each of 20 synthetic genomic sequences (length 500,000 bp each) and for "
            "the combined corpus. Identify the 50 most over- and under-represented k-mers "
            "vs. null expectation, detect repeat regions (k-mers with frequency &gt; 100 "
            "in consecutive windows), and compute Jensen-Shannon divergence between each "
            "sequence\u2019s spectrum and the corpus mean."
        ),
        "data_strategy": "dataset",
        "data": (
            "20 genomic sequences (500,000 bp each) pre-generated and published on HuggingFace. "
            "Fields: seq_id (int), length, gc_content, sequence (string). "
            "Pass the dataset URL as <tt>datasetUrl</tt> to <tt>run_layer</tt>. "
            "Each worker processes one sequence (seq_id = WorkerIndex). "
            "Layer 2 (1 worker) receives all 20 frequency tables via <tt>previousLayerResultJson</tt> "
            "and computes corpus-level Jensen-Shannon divergences."
        ),
        "parallelism": (
            "Each worker computes one sequence\u2019s full 65,536-entry k-mer table. "
            "A final Layer 2 worker merges tables and runs statistical tests."
        ),
        "applicability": (
            "k-mer spectra underlie metagenomic classification, repeat detection, "
            "and genome assembly QC. Sequential processing of 20 \u00d7 500,000 bp "
            "sequences takes ~20 minutes."
        ),
        "metric": "Frequency tables (verifiable by checksum), top over/under k-mers, JS-divergence per sequence.",
        "seq": "~20 min", "par": "~1 min",
    },
]

# ── Complete agent prompts (verbatim text given to the agent) ──────────────────
HF_RAW = "https://huggingface.co/datasets/parcs-benchmark/parcs-agent-benchmark/resolve/main/data"

PROMPTS = {
    1: (
        "Estimate the 1-day 99% Value-at-Risk (VaR) and Conditional VaR (CVaR) for an "
        "equal-weight portfolio of 50 assets via Monte Carlo simulation.\n\n"
        "Setup: generate A\u2208\u211d^{50\u00d750} with N(0,1) entries from seed 42 (Box-Muller); "
        "compute \u03a3=A\u1d40A/50+0.01\u00b7I and its lower Cholesky factor L. "
        "Simulate 2,000,000 scenarios total. "
        "Each worker draws its batch using seed=WorkerIndex\u00b71000+42; "
        "for each scenario z~N(0,1)^{50}, portfolio loss=\u2212(1/50)\u00b7\u03a3(L\u00b7z)\u1d62.\n\n"
        "Aggregate across workers: VaR\u2089\u2089=99th-percentile loss; "
        "CVaR\u2089\u2089=mean of losses above VaR\u2089\u2089.\n\n"
        "Return: {var_99, cvar_99}"
    ),
    2: (
        "Price 200 down-and-out European call options on the grid "
        "S\u2208{80,85,...,125}, \u03c3\u2208{0.10,0.15,0.20,0.25}, T\u2208{0.25,0.5,1.0,2.0,4.0} years. "
        "Parameters: K=100, r=0.05, barrier B=0.85\u00b7S. "
        "Use 500,000 GBM paths per grid point; "
        "each worker prices an equal slice of the 200 points using seed=WorkerIndex\u00b7999+7. "
        "Compute delta (\u0394S=1) and vega (\u0394\u03c3=0.01) by central finite difference.\n\n"
        "Return: [{S, sigma, T, price, delta, vega}] for all 200 grid points."
    ),
    3: (
        "Find the shortest Hamiltonian tour through 300 cities using Simulated Annealing "
        "(\u03b1=0.9995, T\u2080=1000, 500,000 iterations, 2-opt neighbourhood). "
        "Run multiple independent trials in parallel \u2014 each worker runs one trial "
        "with seed=WorkerIndex\u00b7777 and returns its best tour.\n\n"
        "City coordinates (city_id, x, y):\n"
        f"  {HF_RAW}/task_03_cities.jsonl\n\n"
        "Pass this URL as datasetUrl to run_layer. "
        "Workers read via File.ReadAllText(input.DatasetPath!) and parse JSONL.\n\n"
        "Return: {best_tour_length, best_worker_index, all_trial_lengths:[float]}"
    ),
    4: (
        "Simulate a discrete-time SIR epidemic on N=1,000,000 individuals for 365 days "
        "across a 20\u00d720 parameter grid: "
        "R\u2080\u2208linspace(0.8,4.0,20), \u03b8\u2208linspace(0.01,0.20,20). "
        "Model: \u03b2=R\u2080/14, \u03b3=1/14; when I/N\u2265\u03b8, halve \u03b2. "
        "Initial state: S=999,900, I=100, R=0. "
        "Each worker simulates one row of the 20\u00d720 grid (20 combinations).\n\n"
        "Return: [{R0, theta, peak_infected_frac, attack_rate, days_to_peak, "
        "intervention_days}] for all 400 combinations."
    ),
    5: (
        "Evaluate all 20 gradient-boosting configurations on a binary classification dataset "
        "(80,000 rows, 25 features f0\u2026f24, label) using 5-fold cross-validated AUC-ROC. "
        "Each worker evaluates one configuration (config_index=WorkerIndex).\n\n"
        "Training data:\n"
        f"  {HF_RAW}/task_05_data.jsonl\n\n"
        "Pass this URL as datasetUrl. Workers read via File.ReadAllText(input.DatasetPath!).\n\n"
        "The 20 configurations are the ordered cross-product of:\n"
        "  learning_rate \u2208 {0.01, 0.05, 0.1}\n"
        "  max_depth \u2208 {3, 5, 7}\n"
        "  n_estimators \u2208 {100, 300}\n"
        "  subsample \u2208 {0.8, 1.0}\n\n"
        "Return: {best_config_index, best_auc_roc, all_configs:[{config_index, auc_roc}]}"
    ),
    6: (
        "Fit a Cox proportional hazards model on 8,000 patients (covariates cov_0\u2026cov_4, "
        "observed time, event indicator) and compute 95% bootstrap confidence intervals "
        "for all 5 hazard ratios using B=10,000 bootstrap resamples. "
        "Workers each run an equal share of resamples; "
        "use seed=WorkerIndex\u00b71000+resample_index per resample.\n\n"
        "Patient data (cov_0\u2026cov_4, time, event):\n"
        f"  {HF_RAW}/task_06_patients.jsonl\n\n"
        "Pass this URL as datasetUrl. Workers read via File.ReadAllText(input.DatasetPath!).\n\n"
        "Return: {cov_0\u2026cov_4: {ci_lower, ci_upper, point_estimate}}"
    ),
    7: (
        "Compute the full 600\u00d7600 pairwise similarity matrix for 600 protein sequences "
        "using simplified Smith-Waterman (match=+2, mismatch=\u22121, gap=\u22122). "
        "Normalise scores to [0,1] by dividing by 2\u00b7min(len_i,len_j). "
        "Workers each compute an equal block of the upper-triangular matrix (~9,000 pairs at parallelism=20). "
        "Identify the top-20 most similar pairs and count clusters with similarity>0.7.\n\n"
        "Protein sequences (seq_id, length, sequence):\n"
        f"  {HF_RAW}/task_07_sequences.jsonl\n\n"
        "Pass this URL as datasetUrl. Workers read via File.ReadAllText(input.DatasetPath!).\n\n"
        "Return: {top_20_pairs:[{seq_i, seq_j, score}], n_clusters_above_0p7, matrix_checksum}"
    ),
    8: (
        "Train a random forest of 200 decision trees on a regression dataset and evaluate "
        "on a held-out test set. Tree config: bootstrap sample, \u221a30\u22485 features/split, "
        "max_depth=15, min_leaf_size=5. Use a two-layer pipeline:\n\n"
        "  Layer 1 \u2014 workers each grow a subset of trees from the training data "
        "(tree seed=WorkerIndex\u00b7200+treeIndex):\n"
        f"    {HF_RAW}/task_08_train.jsonl  (60,000 rows, f0\u2026f29, target)\n\n"
        "  Layer 2 \u2014 one worker aggregates all trees and evaluates the ensemble:\n"
        f"    {HF_RAW}/task_08_test.jsonl  (10,000 rows)\n\n"
        "Pass each file as datasetUrl to its respective run_layer call.\n\n"
        "Return: {oob_rmse, test_rmse, top_10_feature_importances:[{feature, importance}]}"
    ),
    9: (
        "Among 10,000 randomly generated 512-bit odd integers, count how many pass a "
        "Miller-Rabin primality test (k=20 witness rounds) and how many are safe primes "
        "((p\u22121)/2 also prime). "
        "Workers each test an independent batch using seed=WorkerIndex\u00b79973 "
        "to generate candidates. Compare the empirical prime density to the "
        "prime number theorem prediction 1/ln(2\u2075\u00b9\u00b2).\n\n"
        "Return: {prime_count, safe_prime_count, empirical_density, pnt_density, "
        "iteration_histogram:[int]}"
    ),
    10: (
        "Reprice a portfolio of 300 financial instruments under 500 stress scenarios using "
        "delta-linear approximation: P&L_s=\u03a3\u1d62 w\u1d62\u00b7(\u03b4\u1d62\u00b7shock_s). "
        "Identify the 10 worst-P&L scenarios and attribute losses to primary risk drivers. "
        "Use a two-layer pipeline:\n\n"
        "  Layer 1 (1 worker) \u2014 load the portfolio and pass it via resultJson:\n"
        f"    {HF_RAW}/task_10_portfolio.jsonl  (instrument_id, weight, delta_0\u2026delta_14)\n\n"
        "  Layer 2 (parallel workers) \u2014 each reprices the portfolio under its share of scenarios;\n"
        "  receive portfolio from previousLayerResultJson and load scenarios as datasetUrl:\n"
        f"    {HF_RAW}/task_10_scenarios.jsonl  (scenario_id, shock_0\u2026shock_14)\n\n"
        "Return: {worst_10_scenarios:[{scenario_id, pnl}], pnl_var_99, pnl_std}"
    ),
    11: (
        "Score 50,000 drug-like molecules against a reference kinase inhibitor fingerprint "
        "using Tanimoto similarity on 2048-bit Morgan fingerprints. "
        "Proxy score=0.7\u00b7Tanimoto+0.1\u00b7(1\u2212MW/700)+0.1\u00b7(1\u2212HBD/8)+0.1\u00b7(1\u2212logP/7). "
        "Apply Lipinski Rule-of-Five filters (MW\u2264500, HBD\u22645, HBA\u226410, logP\u22645). "
        "Workers each score an equal partition of the library. "
        "Reference fingerprint: 2048-bit vector from seed=0 (bit-set prob 0.05).\n\n"
        "Molecule library (mol_id, fingerprint as 512-char hex, mw, hbd, hba, logp):\n"
        f"  {HF_RAW}/task_11_library.jsonl\n\n"
        "Pass this URL as datasetUrl. Workers read via File.ReadAllText(input.DatasetPath!).\n\n"
        "Return: {top_100_mol_ids:[int], lipinski_pass_rate, mean_tanimoto}"
    ),
    12: (
        "Simulate 2,000,000 annual insurance policy years under a compound Poisson risk process: "
        "claim count N~Poisson(\u03bb=200), severity X~LogNormal(\u03bc=8,\u03c3=1.5). "
        "Premium P=1.2\u00b7E[S]; initial surplus U=500,000. "
        "Workers each simulate an independent batch using "
        "seed_counts=WorkerIndex\u00b72053+1, seed_severities=WorkerIndex\u00b73571+2.\n\n"
        "Estimate:\n"
        "  \u2022 Ruin probability within 1, 5, and 10 years\n"
        "  \u2022 99.5th-percentile annual aggregate loss (Solvency II SCR proxy)\n"
        "  \u2022 Expected deficit given ruin\n\n"
        "Return: {ruin_prob_1yr, ruin_prob_5yr, ruin_prob_10yr, scr_995, expected_deficit_given_ruin}"
    ),
    13: (
        "On a synthetic weighted road-network graph (5,000 nodes, ~25,000 edges, "
        "weights in minutes), compute Dijkstra shortest paths from 400 source nodes, "
        "approximate betweenness centrality, diameter and average path length (hops), "
        "and the top-20 most critical edges by removal impact.\n\n"
        "Use a two-layer pipeline:\n\n"
        "  Layer 1 (1 worker) \u2014 load the node coordinate map and pass it via resultJson:\n"
        f"    {HF_RAW}/task_13_nodes.jsonl  (node_id, x, y)\n\n"
        "  Layer 2 (parallel workers) \u2014 load edges as datasetUrl, receive node map "
        "from previousLayerResultJson, and each run Dijkstra from source nodes "
        "WorkerIndex\u00d720 through WorkerIndex\u00d720+19:\n"
        f"    {HF_RAW}/task_13_edges.jsonl  (u, v, weight_minutes)\n\n"
        "Return: {top_20_betweenness_nodes, top_20_critical_edges, avg_path_length_hops, diameter_hops}"
    ),
    14: (
        "Simulate photon transport through a 50-layer plane-parallel atmosphere. "
        "Layer l: \u03c4_scat[l]=0.1\u00b7e^(\u22120.05l), \u03c4_abs[l]=0.02\u00b7e^(\u22120.08l); "
        "\u03c9[l]=\u03c4_scat/(\u03c4_scat+\u03c4_abs). Solar zenith \u03b8=30\u00b0; Rayleigh phase function. "
        "Simulate 5,000,000 photons total; each worker traces its batch "
        "using seed=WorkerIndex\u00b76271+3.\n\n"
        "Return: {toa_upwelling_radiance, surface_downwelling_irradiance, "
        "per_layer_heating_rates:[50 floats], ssa_retrieval_rmse}"
    ),
    15: (
        "Compute the k=8 frequency spectrum (4\u2078=65,536 k-mers) for each of 20 synthetic "
        "genomic sequences and for the combined corpus. For each sequence identify the "
        "top-50 over/under-represented k-mers vs. null expectation and any repeat regions "
        "(k-mer freq>100 in consecutive 10 kb windows). "
        "Each worker processes one sequence (seq_id=WorkerIndex). "
        "A final aggregation layer computes Jensen-Shannon divergence between each "
        "spectrum and the corpus mean.\n\n"
        "Sequences (seq_id, length, gc_content, sequence):\n"
        f"  {HF_RAW}/task_15_sequences.jsonl\n\n"
        "Pass this URL as datasetUrl. Workers read via File.ReadAllText(input.DatasetPath!).\n\n"
        "Return: {js_divergences:[20 floats], corpus_top_10_kmers:[str], "
        "frequency_table_checksums:[20 ints]}"
    ),
}

for t in tasks:
    t['prompt'] = PROMPTS[t['id']]

# ── Attach HuggingFace dataset references ─────────────────────────────────────
HF_BASE = "https://huggingface.co/datasets/parcs-benchmark/parcs-agent-benchmark"
HF_SPLITS = {
    1:  "benchmark (task_id=1)",
    2:  "benchmark (task_id=2)",
    3:  "task_03_cities · benchmark (task_id=3)",
    4:  "task_04_sir_reference · benchmark (task_id=4)",
    5:  "task_05_data · task_05_configs · benchmark (task_id=5)",
    6:  "task_06_patients · benchmark (task_id=6)",
    7:  "task_07_sequences · benchmark (task_id=7)",
    8:  "task_08_train · task_08_test · benchmark (task_id=8)",
    9:  "benchmark (task_id=9)",
    10: "task_10_portfolio · task_10_scenarios · benchmark (task_id=10)",
    11: "task_11_library · benchmark (task_id=11)",
    12: "benchmark (task_id=12)",
    13: "task_13_nodes · task_13_edges · task_13_sources · benchmark (task_id=13)",
    14: "benchmark (task_id=14)",
    15: "task_15_sequences · benchmark (task_id=15)",
}
for t in tasks:
    t["dataset"] = HF_SPLITS[t["id"]]

# ── Build PDF ──────────────────────────────────────────────────────────────────
doc = SimpleDocTemplate(
    OUTPUT,
    pagesize=A4,
    leftMargin=2.5*cm, rightMargin=2.5*cm,
    topMargin=2.5*cm, bottomMargin=2.5*cm,
    title="PARCS-Agent Benchmark Task Dataset",
    author="Oleksii Bohusevych",
)

story = []

# Cover
story.append(Spacer(1, 1.5*cm))
story.append(Paragraph("PARCS-Agent Benchmark", title_style))
story.append(Paragraph("Task Dataset for Evaluating Distributed AI Agents", subtitle_style))
story.append(Spacer(1, 0.4*cm))
story.append(HRFlowable(width="100%", thickness=2, color=ACCENT))
story.append(Spacer(1, 0.3*cm))
story.append(Paragraph("Oleksii Bohusevych &nbsp;&nbsp;|&nbsp;&nbsp; Taras Shevchenko National University of Kyiv", meta_style))
story.append(Paragraph("2026", meta_style))
story.append(Spacer(1, 1.2*cm))

# Abstract
abstract_text = (
    "This document defines 15 benchmark tasks for evaluating PARCS-enabled AI agents against "
    "standard sequential agents. Every task is fully self-contained: agents receive only "
    "the task prompt and access to the PARCS MCP server. "
    "Six tasks use closed-form mathematical parameters (no data transfer). "
    "Six tasks provide pre-generated input data via a single HuggingFace dataset URL "
    "passed as <tt>datasetUrl</tt> to <tt>run_layer</tt>. "
    "Three tasks require a two-layer pipeline where one layer loads a file and passes it "
    "to the next layer via <tt>previousLayerResultJson</tt>. "
    "All datasets and reference answers are published at "
    "<b>parcs-benchmark/parcs-agent-benchmark</b> on HuggingFace. "
    "Tasks span quantitative finance, machine learning, bioinformatics, combinatorial "
    "optimisation, epidemiology, cryptography, atmospheric science, and actuarial science."
)
abstract_table = Table(
    [[Paragraph("<b>Abstract.</b> " + abstract_text, body_style)]],
    colWidths=[15.5*cm],
)
abstract_table.setStyle(TableStyle([
    ('BACKGROUND', (0,0), (-1,-1), LIGHT),
    ('BOX', (0,0), (-1,-1), 1, ACCENT),
    ('LEFTPADDING', (0,0), (-1,-1), 12),
    ('RIGHTPADDING', (0,0), (-1,-1), 12),
    ('TOPPADDING', (0,0), (-1,-1), 10),
    ('BOTTOMPADDING', (0,0), (-1,-1), 10),
]))
story.append(abstract_table)
story.append(Spacer(1, 0.8*cm))

# Data strategy legend
story.append(Paragraph("Data Access Strategies", h1_style))
legend_data = [
    [Paragraph("<b>Strategy</b>", label_style), Paragraph("<b>Description</b>", label_style), Paragraph("<b>Tasks</b>", label_style)],
    [Paragraph("Mathematical parameters", data_label),
     Paragraph("All data is defined by closed-form formulas in the prompt. Workers generate everything internally from the given seeds — no files, no downloads.", value_style),
     Paragraph("1, 2, 4, 9, 12, 14", value_style)],
    [Paragraph("HuggingFace dataset", make_style('DL_hf', 'Normal', fontSize=9, leading=13, fontName='Helvetica-Bold', textColor=colors.HexColor('#6a1b9a'))),
     Paragraph("Input data is loaded by passing a single HuggingFace raw URL as <tt>datasetUrl</tt> to <tt>run_layer</tt>. The MCP server downloads it once; all workers read from the shared NFS path in <tt>input.DatasetPath</tt>.", value_style),
     Paragraph("3, 5, 6, 7, 11, 15", value_style)],
    [Paragraph("Two-layer pipeline", make_style('DL_pipe', 'Normal', fontSize=9, leading=13, fontName='Helvetica-Bold', textColor=colors.HexColor('#e65100'))),
     Paragraph("Requires two files. Layer 1 (1 worker) loads the smaller file via <tt>datasetUrl</tt> and passes it via <tt>resultJson</tt>. Layer 2 (parallel workers) loads the larger file as <tt>datasetUrl</tt> and reads Layer 1 output from <tt>previousLayerResultJson</tt>.", value_style),
     Paragraph("8, 10, 13", value_style)],
]
legend_table = Table(legend_data, colWidths=[3.8*cm, 8.7*cm, 3.0*cm])
legend_table.setStyle(TableStyle([
    ('BACKGROUND', (0,0), (-1,0), ACCENT),
    ('TEXTCOLOR', (0,0), (-1,0), colors.white),
    ('FONTNAME', (0,0), (-1,0), 'Helvetica-Bold'),
    ('FONTSIZE', (0,0), (-1,0), 9),
    ('ROWBACKGROUNDS', (0,1), (-1,-1), [colors.white, GREEN_L]),
    ('GRID', (0,0), (-1,-1), 0.5, MID),
    ('VALIGN', (0,0), (-1,-1), 'TOP'),
    ('LEFTPADDING', (0,0), (-1,-1), 6),
    ('RIGHTPADDING', (0,0), (-1,-1), 6),
    ('TOPPADDING', (0,0), (-1,-1), 5),
    ('BOTTOMPADDING', (0,0), (-1,-1), 5),
]))
story.append(legend_table)
story.append(Spacer(1, 0.5*cm))

# Evaluation setup
story.append(Paragraph("Evaluation Setup", h1_style))
story.append(Paragraph(
    "<b>Agent A (PARCS-enabled):</b> Has access to PARCS MCP tools "
    "(<i>get_cluster_info</i>, <i>create_session</i>, <i>run_layer</i>) and can fan work "
    "out across up to 21 parallel C# workers on a GKE cluster via Pub/Sub + KEDA autoscaling.",
    body_style))
story.append(Paragraph(
    "<b>Agent B (baseline):</b> No cluster access. Must compute everything sequentially. "
    "Given the same time budget, Agent B must either dramatically undersample, time out, "
    "or explicitly acknowledge incomplete results.",
    body_style))
story.append(Paragraph(
    "Both agents receive identical prompts. Primary metrics: <b>solution quality</b> "
    "(correctness and completeness), <b>time-to-result</b>, and <b>effective sample size</b> "
    "(fraction of required computation actually performed).",
    body_style))
story.append(Paragraph(
    "<b>Dataset distribution:</b> For tasks with pre-generated input data, Agent A passes the "
    "HuggingFace dataset URL as the <tt>datasetUrl</tt> parameter of <tt>run_layer</tt>. "
    "The MCP server downloads the file once and writes it to the shared cluster NFS volume; "
    "all daemon workers read it directly from <tt>input.DatasetPath</tt> \u2014 "
    "no per-worker download, no data transfer overhead.",
    body_style))
story.append(Spacer(1, 0.3*cm))

# Summary table
story.append(Paragraph("Task Overview", h1_style))
header = ['#', 'Task', 'Domain', 'Data', 'Seq.', 'Par.']
rows = [header]
strategy_abbrev = {"params": "Params", "dataset": "Dataset", "pipeline": "Pipeline"}
for t in tasks:
    rows.append([
        str(t['id']),
        t['title'],
        t['domain'],
        strategy_abbrev[t['data_strategy']],
        t['seq'],
        t['par'],
    ])

col_widths = [0.6*cm, 5.8*cm, 3.5*cm, 1.4*cm, 1.9*cm, 2.3*cm]
summary_table = Table(rows, colWidths=col_widths, repeatRows=1)
summary_table.setStyle(TableStyle([
    ('BACKGROUND', (0,0), (-1,0), ACCENT),
    ('TEXTCOLOR', (0,0), (-1,0), colors.white),
    ('FONTNAME', (0,0), (-1,0), 'Helvetica-Bold'),
    ('FONTSIZE', (0,0), (-1,0), 8),
    ('ALIGN', (0,0), (-1,0), 'CENTER'),
    ('FONTSIZE', (0,1), (-1,-1), 8),
    ('FONTNAME', (0,1), (-1,-1), 'Helvetica'),
    ('ROWBACKGROUNDS', (0,1), (-1,-1), [colors.white, LIGHT]),
    ('ALIGN', (0,1), (0,-1), 'CENTER'),
    ('ALIGN', (3,1), (-1,-1), 'CENTER'),
    ('VALIGN', (0,0), (-1,-1), 'MIDDLE'),
    ('GRID', (0,0), (-1,-1), 0.5, MID),
    ('LEFTPADDING', (0,0), (-1,-1), 4),
    ('RIGHTPADDING', (0,0), (-1,-1), 4),
    ('TOPPADDING', (0,0), (-1,-1), 4),
    ('BOTTOMPADDING', (0,0), (-1,-1), 4),
]))
story.append(summary_table)
story.append(Paragraph(
    "Table 1. \u2018Data\u2019 column: how workers obtain the dataset. "
    "\u2018Seq.\u2019 = estimated wall-clock time for Agent B on one CPU core. "
    "\u2018Par.\u2019 = estimated wall-clock time for Agent A using 20 PARCS workers.",
    caption_style))

story.append(PageBreak())

# Individual task pages
story.append(Paragraph("Task Definitions", h1_style))
story.append(Spacer(1, 0.3*cm))

strategy_label = {
    "params":   "\u2022 Mathematical parameters",
    "dataset":  "\u2022 HuggingFace dataset",
    "pipeline": "\u2022 Two-layer pipeline",
}
strategy_color = {
    "params":   colors.HexColor('#e3f2fd'),
    "dataset":  colors.HexColor('#f3e5f5'),
    "pipeline": colors.HexColor('#fff3e0'),
}
strategy_border = {
    "params":   colors.HexColor('#1565c0'),
    "dataset":  colors.HexColor('#6a1b9a'),
    "pipeline": colors.HexColor('#e65100'),
}

for t in tasks:
    sc = strategy_color[t['data_strategy']]
    sb = strategy_border[t['data_strategy']]

    # Header bar
    header_data = [[
        Paragraph(f"Task {t['id']}", make_style(f'TN{t["id"]}', 'Normal',
            fontSize=11, fontName='Helvetica-Bold', textColor=colors.white)),
        Paragraph(t['title'], make_style(f'TT{t["id"]}', 'Normal',
            fontSize=12, fontName='Helvetica-Bold', textColor=colors.white, leading=16)),
        Paragraph(t['domain'], make_style(f'TD{t["id"]}', 'Normal',
            fontSize=9, textColor=colors.HexColor('#c5cae9'), alignment=2)),
    ]]
    header_tbl = Table(header_data, colWidths=[1.8*cm, 10.0*cm, 3.7*cm])
    header_tbl.setStyle(TableStyle([
        ('BACKGROUND', (0,0), (-1,-1), ACCENT),
        ('VALIGN', (0,0), (-1,-1), 'MIDDLE'),
        ('LEFTPADDING', (0,0), (-1,-1), 10),
        ('RIGHTPADDING', (0,0), (-1,-1), 10),
        ('TOPPADDING', (0,0), (-1,-1), 8),
        ('BOTTOMPADDING', (0,0), (-1,-1), 8),
    ]))

    # Main fields
    ds_text = (
        f'<link href="{HF_BASE}" color="#6a1b9a">{HF_BASE}</link>'
        f'<br/>Splits: {t["dataset"]}'
    )
    fields_data = [
        [Paragraph("\u2022 Agent Prompt", prompt_label),
         Paragraph(t['prompt'], prompt_value)],
        [Paragraph("Formal Definition", label_style),
         Paragraph(t['definition'], value_style)],
        [Paragraph(strategy_label[t['data_strategy']], make_style(f'DL{t["id"]}', 'Normal',
            fontSize=9, leading=13, fontName='Helvetica-Bold',
            textColor=sb)),
         Paragraph(t['data'], make_style(f'DV{t["id"]}', 'Normal',
            fontSize=9.5, leading=14, alignment=TA_JUSTIFY))],
        [Paragraph("\u2022 HuggingFace Dataset", link_label),
         Paragraph(ds_text, link_value)],
        [Paragraph("Parallelisation", label_style),
         Paragraph(t['parallelism'], value_style)],
        [Paragraph("Applicability", label_style),
         Paragraph(t['applicability'], value_style)],
        [Paragraph("Success Metric", label_style),
         Paragraph(t['metric'], value_style)],
        [Paragraph("Timing", label_style),
         Paragraph(f"Sequential (Agent B): <b>{t['seq']}</b> &nbsp;&nbsp;&nbsp; Parallel 20\u00d7 (Agent A): <b>{t['par']}</b>", value_style)],
    ]

    fields_tbl = Table(fields_data, colWidths=[3.8*cm, 11.7*cm])
    fields_tbl.setStyle(TableStyle([
        ('VALIGN', (0,0), (-1,-1), 'TOP'),
        ('LEFTPADDING', (0,0), (-1,-1), 6),
        ('RIGHTPADDING', (0,0), (-1,-1), 6),
        ('TOPPADDING', (0,0), (-1,-1), 5),
        ('BOTTOMPADDING', (0,0), (-1,-1), 5),
        # Prompt row — dark indigo tint
        ('BACKGROUND', (0,0), (-1,0), colors.HexColor('#ede7f6')),
        # Data row highlighted
        ('BACKGROUND', (0,2), (-1,2), sc),
        ('ROWBACKGROUNDS', (0,1), (-1,-1), [colors.white, LIGHT]),
        ('BACKGROUND', (0,2), (-1,2), sc),   # override for data row
        ('BACKGROUND', (0,3), (-1,3), colors.HexColor('#f3e5f5')),  # HF dataset row
        ('LINEBELOW', (0,0), (-1,-2), 0.5, MID),
        ('BOX', (0,0), (-1,-1), 0.8, ACCENT),
        ('LINEAFTER', (0,0), (0,-1), 0.5, MID),
    ]))

    story.append(KeepTogether([header_tbl, Spacer(1, 0.15*cm)]))
    story.append(fields_tbl)
    story.append(Spacer(1, 0.6*cm))

doc.build(story)
print(f"PDF written to {OUTPUT}")

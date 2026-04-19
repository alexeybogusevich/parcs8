from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from reportlab.lib.units import cm
from reportlab.lib import colors
from reportlab.platypus import (
    SimpleDocTemplate, Paragraph, Spacer, Table, TableStyle,
    HRFlowable, PageBreak, KeepTogether
)
from reportlab.lib.enums import TA_CENTER, TA_JUSTIFY

OUTPUT = "/sessions/sweet-pensive-thompson/mnt/parcs7/articles/parcs-agent-paper/benchmark_tasks_v2.pdf"

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
        "data_strategy": "seed",
        "data": (
            "300 city coordinates generated deterministically: "
            "<tt>var rng = new Random(12345);</tt> "
            "<tt>x[i] = rng.NextDouble() * 1000; y[i] = rng.NextDouble() * 1000;</tt> "
            "Every worker regenerates the identical 300-city instance from seed = 12345, "
            "then runs SA with its own trial seed <tt>new Random(WorkerIndex * 777)</tt>. "
            "No data transfer needed."
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
        "data_strategy": "seed",
        "data": (
            "Dataset generated deterministically inside each worker from seed = 42: "
            "features X ~ N(0,1) \u2208 \u211d<super>80000\u00d725</super>, "
            "logit(P(y=1)) = X\u00b7\u03b2 where \u03b2 ~ N(0,1) from seed = 42. "
            "Train/test split 70/30 using seed = 42. Each worker receives its configuration "
            "index via <tt>parametersJson[\"configIndex\"]</tt> and rebuilds the dataset "
            "independently \u2014 no data transfer."
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
        "data_strategy": "seed",
        "data": (
            "Patient data generated from seed = 42: covariates X \u2208 \u211d<super>8000\u00d75</super> "
            "~ N(0,1); log-hazard = X\u00b7\u03b2, \u03b2 = (0.5, \u22120.3, 0.8, 0.1, \u22120.6); "
            "event times ~ Exponential(exp(log-hazard)); 30% censoring at Uniform(0, max_time). "
            "All workers regenerate the full dataset from seed = 42. "
            "Bootstrap resamples use <tt>new Random(WorkerIndex * 1000 + b)</tt> per resample b."
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
        "data_strategy": "seed",
        "data": (
            "600 amino acid sequences generated deterministically: sequence i drawn from the "
            "20-letter alphabet with biologically realistic composition frequencies, "
            "using <tt>new Random(i * 31 + 17)</tt>, lengths sampled from "
            "Uniform(50, 500) via <tt>new Random(i)</tt>. "
            "Every worker regenerates all 600 sequences independently from these seeds "
            "and computes its assigned block of the upper-triangular similarity matrix."
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
        "data_strategy": "seed",
        "data": (
            "Dataset generated from seed = 99: features X ~ N(0,1) \u2208 \u211d<super>70000\u00d730</super>; "
            "target y = sin(X\u00b7\u03b2) + 0.1\u00b7\u03b5, \u03b2 ~ N(0,1) from seed = 99, "
            "\u03b5 ~ N(0,1). First 60,000 rows = train; last 10,000 = test. "
            "Every worker regenerates the full dataset from seed = 99 "
            "and grows its 10 trees using bootstrap seeds "
            "<tt>WorkerIndex * 200 + treeIndex</tt>."
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
        "data_strategy": "seed",
        "data": (
            "Portfolio generated from seed = 77: instrument sensitivities (deltas) drawn "
            "from N(0,1) \u2208 \u211d<super>300\u00d715</super>; instrument weights "
            "~ Uniform(0,1) normalised. Stress scenarios generated from seed = 99: "
            "500 shock vectors \u2208 \u211d<super>15</super>, each entry ~ N(0, 0.03) "
            "representing a 3% volatility shock. Every worker regenerates portfolio and "
            "scenario matrix from their seeds and prices its 25 scenarios independently."
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
        "data_strategy": "seed",
        "data": (
            "Library of 50,000 molecules generated from seed = 55: each fingerprint is a "
            "2048-bit vector with bit-set probability 0.05 (sparse, realistic). "
            "Physicochemical descriptors (MW, HBD, HBA, logP) drawn from realistic "
            "distributions parameterised from seed = 55. Reference fingerprint drawn "
            "from seed = 0. Each worker regenerates its molecule partition "
            "(<tt>WorkerIndex * 2500</tt> to <tt>(WorkerIndex+1) * 2500</tt>) from seed = 55."
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
        "data_strategy": "seed",
        "data": (
            "Graph generated from seed = 314 using a random geometric model: nodes placed "
            "uniformly in [0,1]<super>2</super>; edges added between nodes within distance "
            "threshold d = 0.055 (yields ~25,000 edges); weights ~ Uniform(1, 30) minutes "
            "from seed = 314. Every worker regenerates the full graph from seed = 314 "
            "and runs Dijkstra from its 20 assigned source nodes "
            "(indices <tt>WorkerIndex*20</tt> to <tt>WorkerIndex*20+19</tt>)."
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
        "data_strategy": "seed",
        "data": (
            "Sequence i (i = 0\u202619) generated inside worker i from "
            "<tt>new Random(i * 104729 + 1)</tt> by sampling each base from "
            "{A:0.30, C:0.20, G:0.20, T:0.30} (realistic GC content). "
            "Each worker regenerates only its own sequence \u2014 no cross-worker data "
            "transfer in Layer 1. A final layer receives all 20 frequency tables "
            "(65,536 integers each, ~512 KB total) via <tt>previousLayerResultJson</tt> "
            "and computes corpus-level statistics."
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
        "You have a portfolio of 50 assets with equal weights (w_i = 1/50). "
        "The return covariance matrix \u03a3 \u2208 \u211d^{50\u00d750} is generated from seed=42: "
        "A = randn(50,50,seed=42); \u03a3 = A\u1d40\u00b7A/50 + 0.01\u00b7I. "
        "Using 2,000,000 Monte Carlo scenarios (each of 20 workers generates 100,000 using "
        "seed WorkerIndex*1000+42 and the shared Cholesky factor L of \u03a3), "
        "estimate the 1-day 99% Value-at-Risk (VaR) and Conditional VaR (CVaR) of the portfolio loss distribution. "
        "Loss = \u2013(portfolio return). "
        "Return {var_99, cvar_99} as floats."
    ),
    2: (
        "Price 200 down-and-out European call options on a (S, \u03c3, T) grid: "
        "S \u2208 {80,85,90,95,100,105,110,115,120,125}, \u03c3 \u2208 {0.10,0.15,0.20,0.25}, "
        "T \u2208 {0.25,0.5,1.0,2.0,4.0} years. Parameters: K=100, r=0.05, B=0.85\u00b7S. "
        "Use 500,000 GBM paths per grid point; each of 20 workers prices 10 points using "
        "seed WorkerIndex*999+7. Compute delta and vega by central finite difference. "
        "Return a JSON array of {S, sigma, T, price, delta, vega} for all 200 points."
    ),
    3: (
        "Find the shortest Hamiltonian tour through 300 cities. "
        "Load city coordinates (city_id, x, y) by passing this URL as datasetUrl to run_layer:\n"
        f"{HF_RAW}/task_03_cities.jsonl\n"
        "Workers read the file with File.ReadAllText(input.DatasetPath). "
        "Run 20 independent Simulated Annealing trials (\u03b1=0.9995, T\u2080=1000, 500,000 iterations, "
        "2-opt neighbourhood), each worker using seed WorkerIndex*777. "
        "Return {best_tour_length, best_worker_index, all_trial_lengths:[20 floats]}."
    ),
    4: (
        "Simulate a discrete-time SIR epidemic on N=1,000,000 individuals for 365 days across a 20\u00d720 "
        "parameter grid: R\u2080 \u2208 linspace(0.8,4.0,20), \u03b8 \u2208 linspace(0.01,0.20,20). "
        "Parameters: \u03b3=1/14, \u03b2=R\u2080\u00b7\u03b3; when I/N \u2265 \u03b8 apply 50% contact-rate reduction. "
        "Initial conditions: S\u2080=999900, I\u2080=100, R_init=0. "
        "Each of 20 workers simulates 20 parameter combinations (one row of the grid). "
        "Return a JSON array of {R0, theta, peak_infected_frac, attack_rate, days_to_peak, intervention_days} "
        "for all 400 combinations."
    ),
    5: (
        "Train a gradient boosting classifier on a synthetic dataset (80,000 rows, 25 features f0\u2026f24, label). "
        "Load training data by passing this URL as datasetUrl to run_layer:\n"
        f"{HF_RAW}/task_05_data.jsonl\n"
        "Load the hyperparameter grid (20 configs) from:\n"
        f"{HF_RAW}/task_05_configs.jsonl\n"
        "Each of 20 workers trains one config (config_index = WorkerIndex) with 5-fold CV. "
        "Configs: learning_rate\u2208{0.01,0.05,0.1}, max_depth\u2208{3,5,7}, "
        "n_estimators\u2208{100,300}, subsample\u2208{0.8,1.0}. "
        "Return {best_config_index, best_auc_roc, ranked_configs:[{config_index, auc_roc}]}."
    ),
    6: (
        "Fit a Cox proportional hazards model on a synthetic patient dataset and compute 95% bootstrap "
        "confidence intervals for all 5 hazard ratios using B=10,000 resamples. "
        "Load patient data (N=8,000; columns: cov_0\u2026cov_4, time, event) by passing this URL as datasetUrl:\n"
        f"{HF_RAW}/task_06_patients.jsonl\n"
        "Each of 20 workers runs 500 resamples using seed WorkerIndex*1000+b per resample b. "
        "Return {cov_i: {ci_lower, ci_upper, point_estimate}} for i=0..4."
    ),
    7: (
        "Compute the 600\u00d7600 pairwise similarity matrix using simplified Smith-Waterman "
        "(match=+2, mismatch=\u22121, gap=\u22122). "
        "Load sequences (seq_id, length, sequence) by passing this URL as datasetUrl:\n"
        f"{HF_RAW}/task_07_sequences.jsonl\n"
        "Normalise scores to [0,1] by dividing by min(len_i, len_j)\u00d72. "
        "Each of 20 workers computes ~9,000 pairs of the upper-triangular matrix. "
        "Return {top_20_pairs:[{seq_i, seq_j, score}], matrix_checksum (4 d.p.), n_families_above_0p7}."
    ),
    8: (
        "Train a random forest of 200 decision trees on a synthetic regression dataset. "
        "Load training data (60,000 rows, features f0\u2026f29, target) by passing this URL as datasetUrl:\n"
        f"{HF_RAW}/task_08_train.jsonl\n"
        "Test data (10,000 rows) is at:\n"
        f"{HF_RAW}/task_08_test.jsonl\n"
        "Each tree: bootstrap sample, \u221a30\u22485 features/split, max_depth=15, min_leaf=5. "
        "Each of 20 workers grows 10 trees using seeds WorkerIndex*200+treeIndex. "
        "Return {oob_rmse, test_rmse, feature_importances:[{feature, importance}] top 10}."
    ),
    9: (
        "Generate and test 10,000 candidate 512-bit odd integers for primality using Miller-Rabin "
        "(k=20 witness rounds, FP rate < 4^\u221220). For each confirmed prime p, check if (p\u22121)/2 is also prime (safe prime). "
        "Each of 20 workers tests 500 candidates using seed WorkerIndex*9973 to generate 512-bit odd numbers; "
        "witness draws use seed candidate_index. "
        "Return {prime_count, safe_prime_count, empirical_prime_density, "
        "pnt_predicted_density (=1/ln(2^512)), iteration_histogram}."
    ),
    10: (
        "Reprice a portfolio of 300 instruments under 500 stress scenarios using delta-linear approximation: "
        "P&L_s = \u03a3_i w_i\u00b7(\u03b4_i\u00b7shock_s). "
        "Load portfolio (instrument_id, weight, delta_0\u2026delta_14) by passing this URL as datasetUrl:\n"
        f"{HF_RAW}/task_10_portfolio.jsonl\n"
        "Load scenarios (scenario_id, shock_0\u2026shock_14) from:\n"
        f"{HF_RAW}/task_10_scenarios.jsonl\n"
        "Each of 20 workers prices the full portfolio under 25 scenarios (scenario_ids WorkerIndex*25\u2026WorkerIndex*25+24). "
        "Return {worst_10_scenarios:[{scenario_id, pnl}], pnl_var_99, pnl_std, "
        "top_3_loss_drivers per worst scenario}."
    ),
    11: (
        "Score 50,000 molecules against a reference kinase inhibitor fingerprint using Tanimoto similarity "
        "on 2048-bit Morgan fingerprints. "
        "Load molecule library (mol_id, fingerprint as hex string, mw, hbd, hba, logp) by passing this URL as datasetUrl:\n"
        f"{HF_RAW}/task_11_library.jsonl\n"
        "Reference fingerprint: 2048-bit vector with bit-set probability 0.05 generated from seed=0. "
        "Proxy score = 0.7\u00b7Tanimoto + 0.1\u00b7(1\u2212MW/700) + 0.1\u00b7(1\u2212HBD/8) + 0.1\u00b7(1\u2212logP/7). "
        "Lipinski filters: MW\u2264500, HBD\u22645, HBA\u226410, logP\u22645. "
        "Each of 20 workers scores 2,500 molecules (mol_ids WorkerIndex*2500\u2026WorkerIndex*2500+2499). "
        "Return {top_100_mol_ids, lipinski_pass_rate, mean_tanimoto}."
    ),
    12: (
        "Simulate 2,000,000 policy years under a compound Poisson risk process: "
        "claim count N ~ Poisson(\u03bb=200), severity X ~ Lognormal(\u03bc=8, \u03c3=1.5). "
        "Premium P=(1+0.2)\u00b7E[S]; initial surplus U=500,000. "
        "Each of 20 workers simulates 100,000 policy years using "
        "seed_counts=WorkerIndex*2053+1, seed_severities=WorkerIndex*3571+2. "
        "Return {ruin_prob_1yr, ruin_prob_5yr, ruin_prob_10yr, "
        "scr_995 (99.5th-percentile annual loss), expected_deficit_given_ruin}."
    ),
    13: (
        "On a synthetic road-network graph compute: shortest-path lengths from 400 source nodes (Dijkstra), "
        "approximate betweenness centrality, diameter and average path length (hops), "
        "top-20 most critical edges by removal impact. "
        "Load graph nodes (node_id, x, y) by passing this URL as datasetUrl:\n"
        f"{HF_RAW}/task_13_nodes.jsonl\n"
        "Load edges (u, v, weight_minutes) from:\n"
        f"{HF_RAW}/task_13_edges.jsonl\n"
        "Load source assignments (source_id, node, worker_id) from:\n"
        f"{HF_RAW}/task_13_sources.jsonl\n"
        "Each of 20 workers runs Dijkstra from its 20 assigned source nodes (worker_id = WorkerIndex). "
        "Return {top_20_nodes_by_betweenness, top_20_critical_edges, avg_path_length_hops, diameter_hops}."
    ),
    14: (
        "Simulate photon transport through a 50-layer plane-parallel atmosphere. "
        "Layer l: \u03c4_scat[l]=0.1\u00b7exp(\u22120.05\u00b7l), \u03c4_abs[l]=0.02\u00b7exp(\u22120.08\u00b7l), "
        "\u03c9[l]=\u03c4_scat/(\u03c4_scat+\u03c4_abs). Solar zenith \u03b8=30\u00b0; Rayleigh phase function. "
        "Simulate 5,000,000 photons; each of 20 workers traces 250,000 using seed WorkerIndex*6271+3. "
        "Return {toa_radiance, surface_irradiance, heating_rates:[50 floats], ssa_retrieval_rmse}."
    ),
    15: (
        "Compute the k=8 frequency spectrum (4^8=65,536 k-mers) for each of 20 synthetic genomic sequences "
        "and for the combined corpus. "
        "Load sequences (seq_id, length, gc_content, sequence) by passing this URL as datasetUrl:\n"
        f"{HF_RAW}/task_15_sequences.jsonl\n"
        "Each of 20 workers processes one sequence (seq_id = WorkerIndex). "
        "For each sequence: top-50 over/under-represented k-mers vs. null expectation; "
        "repeat regions (k-mer freq>100 in consecutive 10kb windows). "
        "Final layer: Jensen-Shannon divergence between each spectrum and corpus mean. "
        "Return {js_divergences:[20 floats], corpus_top_10_kmers, "
        "frequency_table_checksums:[20 ints], expected_checksum_per_seq: 499993}."
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
    "This document defines 15 benchmark tasks for evaluating PARCS-enabled AI agents vs. "
    "standard sequential agents. Every task is fully self-contained: agents receive only "
    "the task prompt and PARCS MCP credentials \u2014 no external datasets, no file uploads. "
    "Datasets are either defined by closed-form mathematical parameters or generated "
    "deterministically inside workers from a fixed random seed, ensuring reproducibility. "
    "All pre-generated input data and reference answers are published on HuggingFace at "
    "<b>parcs-benchmark/parcs-agent-benchmark</b>. "
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
    [Paragraph("Seed-based generation", data_label),
     Paragraph("Workers regenerate the full synthetic dataset independently from a fixed seed. No data is transferred.", value_style),
     Paragraph("3, 5, 6, 7, 8, 10, 11, 13, 15", value_style)],
    [Paragraph("Mathematical parameters", data_label),
     Paragraph("Data is fully defined by closed-form parameters in the task definition. No generation step needed.", value_style),
     Paragraph("1, 2, 4, 9, 12, 14", value_style)],
    [Paragraph("Layer 0 generation", data_label),
     Paragraph("A single Layer 0 worker generates compact data and passes it via previousLayerResultJson.", value_style),
     Paragraph("(none in this version)", value_style)],
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
strategy_abbrev = {"seed": "Seed", "params": "Params", "layer0": "Layer 0"}
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
    "seed":   "\u2022 Seed-based generation",
    "params": "\u2022 Mathematical parameters",
    "layer0": "\u2022 Layer 0 generation",
}
strategy_color = {
    "seed":   colors.HexColor('#e8f5e9'),
    "params": colors.HexColor('#e3f2fd'),
    "layer0": colors.HexColor('#fff8e1'),
}
strategy_border = {
    "seed":   colors.HexColor('#2e7d32'),
    "params": colors.HexColor('#1565c0'),
    "layer0": colors.HexColor('#f57f17'),
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

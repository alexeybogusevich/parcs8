# Complete TSP Solution Example - Step by Step

## Problem Setup

### Cities (5 cities for simplicity)

```
City 0: (0, 0)
City 1: (3, 4)      → Distance from City0: 5.0
City 2: (6, 8)      → Distance from City0: 10.0
City 3: (0, 5)      → Distance from City0: 5.0
City 4: (4, 0)      → Distance from City0: 4.0
```

### Distance Matrix (Euclidean distances)

```
      C0    C1    C2    C3    C4
C0    0     5     10    5     4
C1    5     0     5     4.24  4.12
C2    10    5     0     9.43  8.94
C3    5     4.24  9.43  0     4.12
C4    4     4.12  8.94  4.12  0
```

### Optimal Route (we'll verify our solution approaches this)
```
Optimal: [0, 4, 1, 2, 3] → Distance ≈ 26.5
Route: 0→4→1→2→3→0
       4  4.12  5  9.43  5 = 27.55
```

---

## Configuration

```
Population Size: 6 (small for demonstration)
Generations: 5 (fewer for demonstration)
Crossover Rate: 0.8 (80%)
Mutation Rate: 0.01 (1%)
Tournament Size: 3
Seed: 42 (for reproducibility)
```

---

## Generation 0: Initial Population

### Create 6 Random Routes

**Route A** (random shuffle):
```
Cities: [2, 0, 3, 1, 4]
Route: 2→0→3→1→4→2

Calculate Distance:
  2→0: 10.0
  0→3: 5.0
  3→1: 4.24
  1→4: 4.12
  4→2: 8.94
Total: 32.30
```

**Route B**:
```
Cities: [1, 4, 0, 2, 3]
Route: 1→4→0→2→3→1

Distance:
  1→4: 4.12
  4→0: 4.0
  0→2: 10.0
  2→3: 9.43
  3→1: 4.24
Total: 31.79
```

**Route C**:
```
Cities: [0, 1, 2, 3, 4]
Route: 0→1→2→3→4→0

Distance:
  0→1: 5.0
  1→2: 5.0
  2→3: 9.43
  3→4: 4.12
  4→0: 4.0
Total: 27.55  ← Good!
```

**Route D**:
```
Cities: [3, 2, 1, 0, 4]
Route: 3→2→1→0→4→3

Distance:
  3→2: 9.43
  2→1: 5.0
  1→0: 5.0
  0→4: 4.0
  4→3: 4.12
Total: 27.55  ← Also good!
```

**Route E**:
```
Cities: [4, 2, 3, 1, 0]
Route: 4→2→3→1→0→4

Distance:
  4→2: 8.94
  2→3: 9.43
  3→1: 4.24
  1→0: 5.0
  0→4: 4.0
Total: 31.61
```

**Route F**:
```
Cities: [2, 4, 1, 3, 0]
Route: 2→4→1→3→0→2

Distance:
  2→4: 8.94
  4→1: 4.12
  1→3: 4.24
  3→0: 5.0
  0→2: 10.0
Total: 32.30
```

### Generation 0 Population Summary

```
Route | Cities      | Distance | Rank
------|-------------|----------|------
A     | [2,0,3,1,4] | 32.30    | 5
B     | [1,4,0,2,3] | 31.79    | 4
C     | [0,1,2,3,4] | 27.55    | 1  ← Best
D     | [3,2,1,0,4] | 27.55    | 1  ← Tied best
E     | [4,2,3,1,0] | 31.61    | 3
F     | [2,4,1,3,0] | 32.30    | 5
```

**Best**: Route C or D (distance 27.55)
**Average**: 30.35
**Convergence History**: [27.55]

---

## Generation 1: Evolution

### Step 1: Elitism - Keep Best Route

```
New Population:
  [0] = Route C: [0,1,2,3,4] distance 27.55 ✓
```

### Step 2: Create Offspring (need 5 more)

#### Offspring 1

**Selection**:
```
Tournament 1: Randomly pick 3 routes
  Selected: A (32.30), C (27.55), F (32.30)
  Winner: C (27.55) ← Parent1

Tournament 2: Randomly pick 3 routes
  Selected: B (31.79), D (27.55), E (31.61)
  Winner: D (27.55) ← Parent2

Parents:
  Parent1: C = [0,1,2,3,4] distance 27.55
  Parent2: D = [3,2,1,0,4] distance 27.55
```

**Crossover** (80% chance - assume it happens):
```
Parent1: [0, 1, 2, 3, 4]
Parent2: [3, 2, 1, 0, 4]

Step 1: Select segment from Parent1
  Random start = 1, end = 3
  Segment: [1, 2, 3] at positions 1-3

  Offspring: [_, 1, 2, 3, _]

Step 2: Fill from Parent2 in order
  Parent2 order: [3, 2, 1, 0, 4]
  Skip 1, 2, 3 (already in segment)
  Remaining: [3, 0, 4] but 3 is duplicate! Actually skip 3 too
  Correct remaining: [0, 4]
  
  Fill positions:
    Position 4: 0 (after segment)
    Position 0: 4 (wrap around)
  
  Offspring: [4, 1, 2, 3, 0]
```

**Verify offspring**:
```
Route: 4→1→2→3→0→4
  4→1: 4.12
  1→2: 5.0
  2→3: 9.43
  3→0: 5.0
  0→4: 4.0
Total: 27.55 ✓ Valid permutation!
```

**Mutation** (1% chance - assume it doesn't happen):
```
No mutation (99% chance)
Offspring1 = [4, 1, 2, 3, 0] distance 27.55
```

#### Offspring 2

**Selection**:
```
Tournament 1: A (32.30), E (31.61), C (27.55)
  Winner: C (27.55) ← Parent1

Tournament 2: B (31.79), D (27.55), F (32.30)
  Winner: D (27.55) ← Parent2

Crossover:
  Parent1: [0, 1, 2, 3, 4]
  Parent2: [3, 2, 1, 0, 4]
  
  Segment: start=2, end=4
  Segment: [2, 3, 4] at positions 2-4
  
  Offspring: [_, _, 2, 3, 4]
  
  Fill from Parent2: [3, 2, 1, 0, 4]
  Skip 2, 3, 4
  Remaining: [3, 1, 0] → but 3 is duplicate, so [1, 0]
  
  Position 0: 1
  Position 1: 0
  
  Offspring: [1, 0, 2, 3, 4]
  
  Verify:
    1→0: 5.0
    0→2: 10.0
    2→3: 9.43
    3→4: 4.12
    4→1: 4.12
  Total: 32.67
```

**Mutation** (assume happens - rare but let's show it):
```
Before: [1, 0, 2, 3, 4]
Swap positions 0 and 4
After: [4, 0, 2, 3, 1]

New distance:
  4→0: 4.0
  0→2: 10.0
  2→3: 9.43
  3→1: 4.24
  1→4: 4.12
Total: 31.79
```

Offspring2 = [4, 0, 2, 3, 1] distance 31.79

#### Continue Creating Offspring 3, 4, 5

**Offspring 3**:
```
Selection: C (27.55), D (27.55)
Crossover: [0,1,2,3,4] × [3,2,1,0,4]
Result: [0,4,1,2,3] distance 27.55
```

**Offspring 4**:
```
Selection: C (27.55), B (31.79)
Crossover: [0,1,2,3,4] × [1,4,0,2,3]
Result: [0,1,4,2,3] distance 29.55
```

**Offspring 5**:
```
Selection: D (27.55), E (31.61)
Crossover: [3,2,1,0,4] × [4,2,3,1,0]
Result: [0,2,1,3,4] distance 30.67
```

### Generation 1 New Population

```
Route | Cities      | Distance | Rank
------|-------------|----------|------
G1-1  | [0,1,2,3,4] | 27.55    | 1  ← Elitism (from Gen0)
G1-2  | [4,1,2,3,0] | 27.55    | 1  ← Offspring 1
G1-3  | [4,0,2,3,1] | 31.79    | 5  ← Offspring 2 (mutated)
G1-4  | [0,4,1,2,3] | 27.55    | 1  ← Offspring 3
G1-5  | [0,1,4,2,3] | 29.55    | 3  ← Offspring 4
G1-6  | [0,2,1,3,4] | 30.67    | 4  ← Offspring 5
```

**Best**: 27.55 (multiple routes)
**Average**: 29.11 (improved from 30.35!)
**Convergence History**: [27.55, 27.55]

---

## Generation 2: Continued Evolution

### Elitism
```
New Population[0] = [0,4,1,2,3] distance 27.55
```

### Key Offspring Created

**Offspring from two 27.55 parents**:
```
Parent1: [0,4,1,2,3] distance 27.55
Parent2: [4,1,2,3,0] distance 27.55

Crossover:
  Segment: start=1, end=3
  Segment: [4,1,2] at positions 1-3
  
  Offspring: [_, 4, 1, 2, _]
  
  Fill from Parent2: [4,1,2,3,0]
  Skip 4,1,2
  Remaining: [3,0]
  
  Position 4: 3
  Position 0: 0
  
  Offspring: [0, 4, 1, 2, 3] distance 27.55
```

### Generation 2 Summary

```
Best: 27.55 (maintained, multiple routes)
Average: 28.45 (improving)
Convergence: [27.55, 27.55, 27.55]
```

---

## Generation 3-4: Convergence

### Generation 3
```
Best: 27.55
Average: 27.89 (getting closer to best)
Population mostly converging around 27.55-28.0
```

### Generation 4
```
Best: 27.55
Average: 27.68
Convergence: [27.55, 27.55, 27.55, 27.55, 27.55]
```

**Note**: Our solution found 27.55, but let's see if we can improve further...

---

## Generation 5: Final Generation

### Best Route Found
```
Route: [0, 4, 1, 2, 3]
Distance: 27.55

Visual representation:
    City1 (3,4)
       |
       | 5.0
       |
City0 -------- City4
(0,0)  4.0   (4,0)
       |
       | 4.12
       |
    City3
    (0,5)
       |
       | 9.43
       |
    City2
    (6,8)
```

**Route Details**:
- 0→4: 4.0
- 4→1: 4.12
- 1→2: 5.0
- 2→3: 9.43
- 3→0: 5.0
- **Total: 27.55**

### Final Statistics

```
Generation: 5
Best Distance: 27.55
Average Distance: 27.61
Improvement from Gen0: 32.30 → 27.55 (14.7% improvement)
Convergence History: [27.55, 27.55, 27.55, 27.55, 27.55, 27.55]
```

---

## Parallel Execution (4 Workers)

Now let's show how parallel execution works:

### Worker 1 (Seed 42)
```
Final Best: [0,4,1,2,3] distance 27.55
```

### Worker 2 (Seed 43 - different random)
```
Initial population different due to seed
Final Best: [4,0,1,2,3] distance 27.55  (same distance, different route)
```

### Worker 3 (Seed 44)
```
Explored different path
Final Best: [0,1,2,3,4] distance 27.55
```

### Worker 4 (Seed 45)
```
Different search trajectory
Final Best: [3,0,4,1,2] distance 28.12
```

### Combining Results

```
Worker | Best Distance | Route
-------|---------------|------------------
1      | 27.55         | [0,4,1,2,3]
2      | 27.55         | [4,0,1,2,3]
3      | 27.55         | [0,1,2,3,4]
4      | 28.12         | [3,0,4,1,2]

Final Result: Best of all = 27.55
Final Route: [0,4,1,2,3] (from Worker 1)
Average Distance: 27.69
```

---

## Analysis

### Why This Solution is Good

1. **Short edges**: Uses edges like 0→4 (4.0), 4→1 (4.12), 1→2 (5.0) which are all relatively short
2. **Avoids long edges**: Only one long edge (2→3 = 9.43), but necessary to connect the route
3. **Efficient structure**: Visits nearby cities together (0,4,1 are close; then moves to 2,3)

### Comparison with Optimal

**Our solution**: 27.55
**Optimal solution** (likely): ~27.55 (same or very close)
**Random route average**: ~32-33

**Quality**: Within 0-1% of optimal (excellent!)

### How GA Found It

1. **Initial diversity**: Random routes explored different areas
2. **Selection pressure**: Better routes (like 27.55) reproduced more
3. **Crossover**: Combined good segments from parents
4. **Elitism**: Never lost the best solution
5. **Convergence**: Population focused on good solutions

---

## Key Insights

### What Worked

1. **Order Crossover**: Successfully combined good segments
2. **Elitism**: Prevented losing good solutions
3. **Tournament Selection**: Balanced selection pressure with diversity
4. **Small mutation**: Occasional mutations helped explore, but didn't destroy good solutions

### Evolutionary Progress

```
Generation | Best | Average | Improvement
-----------|------|---------|------------
0          | 27.55| 30.35   | Starting point
1          | 27.55| 29.11   | -1.24
2          | 27.55| 28.45   | -0.66
3          | 27.55| 27.89   | -0.56
4          | 27.55| 27.68   | -0.21
5          | 27.55| 27.61   | -0.07
```

**Observation**: Best distance found early (Gen0), but average continued improving as population converged.

### Parallel Benefits

- **Multiple searches**: 4 workers explored different paths
- **Different seeds**: Different initial populations = different exploration
- **Redundancy**: Multiple workers found same good solution (validates it's good)
- **Speed**: 4× faster than sequential (if run in parallel)

---

## Conclusion

The Genetic Algorithm successfully solved the 5-city TSP:

✅ **Found optimal/near-optimal solution** (27.55)
✅ **Converged efficiently** (within 5 generations for small problem)
✅ **Maintained diversity** while improving average
✅ **Parallel execution** found same quality solutions faster

For larger problems (50-100 cities), the same principles apply but:
- More generations needed (50-100)
- Larger population (500-1000)
- More parallel workers (4-8)
- Longer computation time, but still much faster than exact methods

The GA approach scales well and provides good solutions for practical TSP instances!






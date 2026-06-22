# Detailed Explanation: How TSP is Solved

## Table of Contents
1. [Problem Definition](#problem-definition)
2. [Why Genetic Algorithms?](#why-genetic-algorithms)
3. [Solution Representation](#solution-representation)
4. [The Genetic Algorithm Framework](#the-genetic-algorithm-framework)
5. [Genetic Operators in Detail](#genetic-operators-in-detail)
6. [Parallel Island Model Architecture](#parallel-island-model-architecture)
7. [Complete Execution Flow](#complete-execution-flow)
8. [Convergence and Stopping Criteria](#convergence-and-stopping-criteria)
9. [Example Walkthrough](#example-walkthrough)

---

## Problem Definition

### The Traveling Salesman Problem (TSP)

**Formal Definition**:
Given a set of cities and distances between every pair of cities, find the shortest possible route that:
1. Visits each city exactly once
2. Returns to the starting city

**Mathematical Formulation**:
- **Input**: Complete graph G = (V, E) with:
  - V = {v₁, v₂, ..., vₙ} (set of n cities)
  - E = all edges (i,j) where i,j ∈ V
  - w(i,j) = distance from city i to city j (Euclidean in our case)
- **Output**: Permutation π of {1, 2, ..., n} that minimizes:
  ```
  Total Distance = Σ(i=1 to n) w(π(i), π((i mod n) + 1))
  ```

**Complexity**:
- **Solution Space**: (n-1)!/2 (for symmetric TSP)
  - For 10 cities: 181,440 possible routes
  - For 50 cities: ~3.04 × 10⁶² possible routes
- **NP-Hard**: No known polynomial-time algorithm
- **Exact algorithms** (like branch-and-bound) only feasible for < 20 cities

---

## Why Genetic Algorithms?

### Advantages for TSP

1. **Exploration of Large Solution Space**: GA maintains a population exploring multiple regions simultaneously
2. **Good Enough Solutions**: Finds near-optimal solutions quickly (acceptable for most applications)
3. **Parallelizable**: Island model allows parallel execution
4. **No Problem-Specific Heuristics Needed**: Works with just distance calculation
5. **Handles Local Optima**: Mutation and population diversity help escape local minima

### Why Not Exact Methods?

- **Branch and Bound**: Exponential time, only works for small instances
- **Dynamic Programming**: O(2ⁿ × n²) memory, impractical for n > 20
- **Brute Force**: O(n!) - completely infeasible

---

## Solution Representation

### Route Class: The Chromosome

Each solution (individual in the population) is represented as a **Route**:

```csharp
public class Route
{
    public List<int> Cities { get; set; }  // Permutation: [0, 3, 1, 4, 2]
    public double TotalDistance { get; private set; }  // Fitness (lower is better)
}
```

**Example Route** for 5 cities:
```
Cities: [0, 3, 1, 4, 2]
Meaning: Visit City0 → City3 → City1 → City4 → City2 → (back to City0)
```

### Distance Calculation

```csharp
public void CalculateDistance()
{
    TotalDistance = 0;
    for (int i = 0; i < Cities.Count; i++)
    {
        int currentCityIndex = Cities[i];
        int nextCityIndex = Cities[(i + 1) % Cities.Count];  // Wrap around
        
        TotalDistance += _cities[currentCityIndex].DistanceTo(_cities[nextCityIndex]);
    }
}
```

**Euclidean Distance** between two cities:
```
Distance = √((x₁ - x₂)² + (y₁ - y₂)²)
```

**Example**:
- City0 at (0, 0), City1 at (3, 4)
- Distance = √((0-3)² + (0-4)²) = √(9 + 16) = 5.0

---

## The Genetic Algorithm Framework

### Basic GA Loop

```csharp
Initialize()              // Create random population
for (generation = 0; generation < maxGenerations; generation++)
{
    Evolve()              // Create new generation
    TrackConvergence()    // Record best distance
    if (Converged()) break;  // Early stopping
}
return GetBestRoute()
```

### Population Structure

**Population**: Collection of Routes (solutions)
- **Size**: Typically 200-1000 individuals
- **Each individual**: A complete route (permutation of cities)
- **Fitness**: Total distance (lower = better)

---

## Genetic Operators in Detail

### 1. Initialization

**Purpose**: Create initial population of random solutions

```csharp
public void Initialize()
{
    _population.Clear();
    
    for (int i = 0; i < _options.PopulationSize; i++)
    {
        var route = new Route(_cities, _random);
        _population.Add(route);
    }
}
```

**How Route is Created**:
1. Start with ordered list: [0, 1, 2, 3, 4]
2. Apply **Fisher-Yates shuffle** to randomize
3. Calculate total distance
4. Result: Random valid route like [3, 1, 4, 0, 2]

**Why Random Initialization?**
- Ensures diversity in initial population
- Covers different regions of solution space
- Provides good starting point for evolution

---

### 2. Selection: Tournament Selection

**Purpose**: Choose parents for reproduction (biased towards better solutions)

```csharp
private Route Select()
{
    const int tournamentSize = 3;
    var tournament = new List<Route>();
    
    // Randomly select 3 individuals
    for (int i = 0; i < tournamentSize; i++)
    {
        var randomIndex = _random.Next(_population.Count);
        tournament.Add(_population[randomIndex]);
    }
    
    // Return the best (shortest distance) from the tournament
    return tournament.OrderBy(r => r.TotalDistance).First();
}
```

**How It Works**:
1. Randomly pick 3 individuals from population
2. Compare their distances
3. Return the one with shortest distance

**Example**:
```
Population: 
  Route A: distance 150.5
  Route B: distance 142.3  ← Winner
  Route C: distance 158.1

Tournament selects: Route B (best of 3)
```

**Why Tournament Selection?**
- **Selective pressure**: Better solutions more likely to reproduce
- **Diversity preserved**: Not always the absolute best (random sampling)
- **Efficient**: O(k) time where k = tournament size (usually 3)
- **Scaling**: Works regardless of fitness value magnitudes

---

### 3. Crossover: Order Crossover (OX)

**Purpose**: Combine two parent routes to create offspring, preserving good segments

**Order Crossover Algorithm**:
1. Select a contiguous segment from Parent1
2. Copy that segment to offspring at same positions
3. Fill remaining positions with cities from Parent2 in their original order (skipping cities already in segment)

**Detailed Step-by-Step**:

```csharp
public Route Crossover(Route other)
{
    int size = Cities.Count;
    int start = _random.Next(size);      // Random start position
    int end = _random.Next(start, size); // Random end position
    
    // Step 1: Copy segment from Parent1 (this route)
    // Example: Parent1 = [0, 1, 2, 3, 4, 5]
    //          Segment: positions 2-4 = [2, 3, 4]
    //          Offspring: [_, _, 2, 3, 4, _]
    
    // Step 2: Fill from Parent2
    // Parent2 = [5, 2, 0, 4, 1, 3]
    // Skip cities already in segment (2, 3, 4)
    // Remaining: [5, 0, 1]
    // Fill in order: [5, 0, _, 2, 3, 4, 1] → wrap around
    // Final: [5, 0, 2, 3, 4, 1]
}
```

**Concrete Example**:

```
Parent1: [0, 1, 2, 3, 4, 5]  (distance: 120.5)
Parent2: [5, 2, 0, 4, 1, 3]  (distance: 135.2)

Step 1: Select segment from Parent1
  Random start = 2, end = 4
  Segment: [2, 3, 4] at positions 2-4
  
  Offspring: [_, _, 2, 3, 4, _]

Step 2: Fill from Parent2 in order
  Parent2 order: [5, 2, 0, 4, 1, 3]
  Skip 2, 3, 4 (already in segment)
  Remaining: [5, 0, 1]
  
  Start filling from position after segment (position 5)
  Position 5: 5
  Position 0: 0 (wraps around)
  Position 1: 1
  
  Final Offspring: [0, 1, 2, 3, 4, 5]
```

**Why Order Crossover?**
- **Preserves segments**: Good sub-routes from Parent1 are kept
- **Preserves relative order**: Ordering information from Parent2 is maintained
- **Always valid**: Produces valid permutations (no duplicate cities)
- **Effective for TSP**: Works well with permutation-based problems

**Crossover Rate**: 0.8 (80% chance of crossover, 20% chance of just copying parent)

---

### 4. Mutation: Swap Mutation

**Purpose**: Introduce randomness to prevent premature convergence and explore new areas

```csharp
public void Mutate()
{
    // Randomly swap two cities
    int index1 = _random.Next(Cities.Count);
    int index2 = _random.Next(Cities.Count);
    
    if (index1 != index2)
    {
        int temp = Cities[index1];
        Cities[index1] = Cities[index2];
        Cities[index2] = temp;
        
        CalculateDistance();  // Recalculate fitness
    }
}
```

**Example**:
```
Before:  [0, 1, 2, 3, 4, 5]
         Swap positions 1 and 4
After:   [0, 4, 2, 3, 1, 5]
```

**Mutation Rate**: 0.01 (1% chance per individual)

**Why Mutation?**
- **Diversity**: Prevents population from getting stuck in local optima
- **Exploration**: Allows reaching new areas of solution space
- **Small changes**: Low rate ensures gradual exploration, not random walk

---

### 5. Elitism

**Purpose**: Ensure the best solution is never lost

```csharp
var bestRoute = GetBestRoute();  // Best from current generation
newPopulation.Add(bestRoute);    // Always keep it
```

**How It Works**:
- Before creating new population, save the best individual
- Add it directly to new population
- Continue creating rest of population through selection/crossover/mutation

**Why Elitism?**
- **Monotonic improvement**: Best distance never increases
- **Guaranteed progress**: Always preserve best found so far
- **Fast convergence**: Good solutions guide evolution

---

## Parallel Island Model Architecture

### Concept: Independent Islands

Instead of one large GA run, we run **multiple independent GA instances** in parallel:

```
Island 1 (Worker 1):  GA with Population 1000, Seed 42
Island 2 (Worker 2):  GA with Population 1000, Seed 43
Island 3 (Worker 3):  GA with Population 1000, Seed 44
Island 4 (Worker 4):  GA with Population 1000, Seed 45

Each island:
- Runs independently
- Has its own population
- Evolves separately
- Finds its own best solution

Final result: Best of all islands
```

### Why Island Model?

1. **Parallel Speedup**: N workers = ~N× faster (each does same work in parallel)
2. **Multiple Search Paths**: Different random seeds = different search trajectories
3. **Better Solutions**: Best of N independent searches usually better than single search
4. **Scalability**: Easy to add more workers

### Architecture Flow

```
Main Module (ParallelMainModule)
│
├─ Create N worker points (via PARCS)
│
├─ Send cities data to all workers
│
├─ Each Worker (ParallelWorkerModule):
│   ├─ Receives cities
│   ├─ Creates GeneticAlgorithm instance
│   ├─ Initializes population
│   ├─ Runs GA for specified generations
│   └─ Returns best route found
│
├─ Collect all results
│
└─ Select best route from all workers
```

---

## Complete Execution Flow

### Phase 1: Setup (Main Module)

```csharp
1. Load cities from file or generate randomly
   Input: cities.txt or GenerateCities(50, seed=42)
   Output: List<City> with 50 cities

2. Create worker points (PARCS)
   For i = 0 to PointsNumber-1:
     point[i] = CreatePoint()
     channel[i] = point[i].CreateChannel()

3. Launch worker modules
   For each point:
     ExecuteClassAsync<ParallelWorkerModule>()
```

### Phase 2: Data Distribution

```csharp
4. Send cities to all workers
   For each channel:
     WriteObjectAsync(cities)
   
   All workers receive same city set
```

### Phase 3: Parallel Evolution (Each Worker)

```csharp
5. Worker receives cities
   cities = ReadObjectAsync<List<City>>()

6. Initialize GA
   ga = new GeneticAlgorithm(cities, options)
   ga.Initialize()  // Create random population

7. Run generations
   for (gen = 0; gen < Generations; gen++)
   {
       ga.Evolve()
       Track convergence
       Check if converged → early stop
   }
```

### Phase 4: Evolution Details (Each Generation)

```csharp
public void Evolve()
{
    newPopulation = []
    
    // Elitism: keep best
    newPopulation.Add(GetBestRoute())
    
    // Create rest of population
    while (newPopulation.Count < PopulationSize)
    {
        // Selection
        parent1 = Select()  // Tournament of 3
        parent2 = Select()  // Tournament of 3
        
        // Crossover (80% chance)
        if (random < 0.8)
            offspring = parent1.Crossover(parent2)
        else
            offspring = new Route(parent1)  // Just copy
        
        // Mutation (1% chance)
        if (random < 0.01)
            offspring.Mutate()  // Swap two cities
        
        newPopulation.Add(offspring)
    }
    
    _population = newPopulation  // Replace old population
}
```

### Phase 5: Result Collection

```csharp
8. Collect results from all workers
   For each channel:
     result = ReadObjectAsync<ModuleOutput>()
     workerResults.Add(result)

9. Combine results
   bestResult = workerResults.OrderBy(r => r.BestDistance).First()
   
   Combined output:
     - BestDistance: best of all workers
     - BestRoute: route from best worker
     - AverageDistance: average across all workers
     - ConvergenceHistory: averaged across workers
```

### Phase 6: Output

```csharp
10. Save results
    - JSON file with full statistics
    - Text file with best route
    - Logging information
```

---

## Convergence and Stopping Criteria

### Convergence Tracking

```csharp
// Track best distance every 5 generations
if (gen % 5 == 0 || gen == generations - 1)
{
    _convergenceHistory.Add(bestDistance);
}
```

### Early Stopping

```csharp
private bool IsConverged()
{
    if (_convergenceHistory.Count < 10) return false;
    
    // Check last 10 recorded values
    var recent = _convergenceHistory.TakeLast(10).ToList();
    var improvement = recent.First() - recent.Last();
    var threshold = recent.First() * 0.001;  // 0.1% of starting value
    
    // Converged if improvement < 0.1%
    return improvement < threshold;
}
```

**Logic**:
- If best distance hasn't improved by >0.1% in last 10 generations → converged
- Stop early to save computation time
- Still runs minimum 10 generations before checking

---

## Example Walkthrough

### Scenario: 10 Cities, 4 Workers, 100 Generations

**Initial Setup**:
```
Cities: 
  City0: (0, 0)
  City1: (3, 4)
  City2: (6, 8)
  ... (10 cities total)

Workers: 4
Population per worker: 1000
Generations: 100
```

### Worker 1 (Seed 42) - Generation 0:

```
Initial Population (sample):
  Route A: [5,2,8,1,9,0,4,7,3,6] distance: 45.3
  Route B: [0,1,2,3,4,5,6,7,8,9] distance: 52.1
  Route C: [8,3,1,6,9,2,0,5,4,7] distance: 48.7
  ... (997 more routes)

Best: Route A with distance 45.3
```

### Worker 1 - Generation 10:

```
After 10 generations of evolution:
  Best distance improved: 45.3 → 42.1
  Average distance improved: 48.5 → 45.2
  Population evolved, better routes emerging
```

### Worker 1 - Generation 50:

```
Best distance: 38.5 (still improving)
Average distance: 41.2
Convergence slowing down
```

### Worker 1 - Generation 100:

```
Final best distance: 36.8
Final average distance: 39.5
Route: [0, 3, 7, 2, 5, 8, 1, 9, 4, 6]
```

### All Workers Complete:

```
Worker 1: Best distance 36.8
Worker 2: Best distance 37.2  (different seed → different search)
Worker 3: Best distance 35.9  ← Winner!
Worker 4: Best distance 37.5

Final Result: Worker 3's route with distance 35.9
```

---

## Performance Characteristics

### Time Complexity

**Per Generation**:
- Selection: O(k × PopulationSize) where k = tournament size (3)
- Crossover: O(n) where n = number of cities
- Mutation: O(1)
- Distance calculation: O(n)
- **Total per generation**: O(PopulationSize × n)

**Total Time**:
- **Sequential**: O(Generations × PopulationSize × n)
- **Parallel (N workers)**: O(Generations × PopulationSize × n / N)

### Space Complexity

- **Per worker**: O(PopulationSize × n)
- **Total (N workers)**: O(N × PopulationSize × n)

### Typical Performance

**For 50 cities**:
- Population: 1000
- Generations: 100
- Workers: 4
- **Time**: ~30-60 seconds (parallel)
- **Speedup**: ~3.5-3.8× vs sequential
- **Solution quality**: Usually within 5-10% of optimal (if known)

---

## Key Design Decisions

### 1. Full Population Per Worker

**Decision**: Each worker gets full population size, not divided.

**Rationale**:
- Better genetic diversity per island
- More effective GA search
- Island model benefits from independent large populations

**Trade-off**: Higher memory usage, but better solutions

### 2. Tournament Selection Size = 3

**Decision**: Always select best of 3 random individuals.

**Rationale**:
- Good balance between selection pressure and diversity
- Standard value in GA literature
- Efficient computation

### 3. Order Crossover

**Decision**: Use OX instead of simpler crossover methods.

**Rationale**:
- Preserves both segments and ordering
- Well-suited for permutation problems
- Standard for TSP

### 4. Low Mutation Rate (1%)

**Decision**: Only 1% chance of mutation per individual.

**Rationale**:
- TSP needs small, incremental changes
- High mutation would destroy good segments
- Population diversity maintained through crossover and selection

### 5. Early Stopping

**Decision**: Stop if improvement < 0.1% over 10 generations.

**Rationale**:
- Saves computation when converged
- Small threshold catches meaningful improvements
- Minimum 10 generations ensures some evolution

---

## Summary

The TSP is solved using a **parallel genetic algorithm** with:

1. **Representation**: Routes as permutations of cities
2. **Selection**: Tournament selection (best of 3)
3. **Crossover**: Order Crossover (preserves segments and order)
4. **Mutation**: Swap mutation (small random changes)
5. **Elitism**: Always preserve best solution
6. **Parallelization**: Island model with N independent workers
7. **Result**: Best route from all workers combined

This approach balances **exploration** (trying new solutions) and **exploitation** (refining good solutions) to find near-optimal routes efficiently.






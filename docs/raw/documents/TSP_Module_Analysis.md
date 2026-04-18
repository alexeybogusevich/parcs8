# TSP Module Analysis and Issues

## Current Implementation Overview

The TSP (Traveling Salesman Problem) module uses a **Genetic Algorithm** approach with parallel execution:

1. **ParallelMainModule**: Creates multiple worker points
2. **ParallelWorkerModule**: Each worker runs an independent GA on the same cities
3. **GeneticAlgorithm**: Standard GA with tournament selection, Order Crossover, and swap mutation
4. **Route**: Represents a solution (permutation of cities)

## Issues Found

### 🔴 Critical Issue #1: Incorrect Order Crossover Implementation

**Location**: `Route.cs`, `Crossover()` method (lines 75-112)

**Problem**: The Order Crossover (OX) implementation is incorrect. The current code tries to fill positions circularly with wrap-around, but OX should maintain relative order from parent2.

**Correct OX Algorithm**:
1. Select a contiguous segment from parent1 (keep it)
2. Remove cities in that segment from consideration
3. Fill remaining positions with cities from parent2 in their **original order**, skipping cities already in the segment

**Current Bug**: The `currentPos = (currentPos + 1) % size` causes wrap-around issues and doesn't properly maintain order.

### 🟡 Issue #2: Population Division Problem

**Location**: `ParallelWorkerModule.cs`, line 49

```csharp
PopulationSize = Math.Max(options.PopulationSize / options.PointsNumber, 50)
```

**Problem**: This divides the population across workers, meaning each worker runs with a smaller population. For an island model (where workers run independently), each island should have a **full population** to maintain diversity and effectiveness.

**Impact**: 
- Each worker has less genetic diversity
- Weaker GA performance per worker
- Less exploration of solution space

**Solution**: Each worker should have the full population size, not divided.

### 🟡 Issue #3: Missing Validation

**Location**: `GeneticAlgorithm.cs`, `Evolve()` method

**Problem**: `Evolve()` can be called without `Initialize()` being called first, which would cause a null reference or empty population error.

**Solution**: Add validation to ensure population is initialized.

### 🟢 Minor Issue #4: Migration Not Connected

The migration code exists but isn't actually connected between distributed workers - it only works within a single worker's population. This is acceptable for the basic parallel model, but the migration feature should use PARCS channels to exchange individuals between workers.

---

## Fixes Needed

1. ✅ Fix Order Crossover implementation
2. ✅ Fix population size division in parallel workers
3. ✅ Add validation checks
4. ⚠️ Optionally improve migration to work across workers via channels






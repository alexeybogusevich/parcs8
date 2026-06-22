# TSP Module для PARCS System

Модуль для розв'язання задачі комівояжера (Traveling Salesman Problem, TSP) з використанням генетичного алгоритму в розподіленій обчислювальній системі PARCS.

## Опис проблеми

Задача комівояжера (TSP) - це класична NP-складна задача оптимізації, яка полягає у знаходженні найкоротшого маршруту, що проходить через всі міста рівно один раз і повертається до початкового міста.

## Архітектура модуля

### Основні компоненти

- **`City`** - представлення міста з координатами та методами розрахунку відстаней
- **`Route`** - представлення маршруту з генетичними операторами
- **`GeneticAlgorithm`** - основна логіка еволюційного алгоритму
- **`CityLoader`** - завантаження та збереження міст з/у файли
- **`ModuleOptions`** - конфігураційні параметри модуля
- **`ModuleOutput`** - структура результатів виконання

### Реалізації

- **`SequentialMainModule`** - послідовна реалізація
- **`ParallelMainModule`** - паралельна реалізація з координацією робочих модулів
- **`ParallelWorkerModule`** - робочий модуль для паралельної обробки

## Нові можливості

### 1. Завантаження вхідних даних з файлу

Модуль підтримує завантаження міст з двох форматів:

#### Текстові файли (.txt)
```
# Формат: ID X Y
0 0.0 0.0
1 100.0 0.0
2 200.0 0.0
...
```

#### JSON файли (.json)
```json
[
  {"Id": 0, "X": 0.0, "Y": 0.0},
  {"Id": 1, "X": 100.0, "Y": 0.0},
  {"Id": 2, "X": 200.0, "Y": 0.0}
]
```

### 2. Генерація тестових даних

Вбудовані патерни генерації міст:
- **Random** - випадковий розподіл
- **Grid** - сітка
- **Clustered** - кластеризований розподіл
- **Circle** - по колу

### 3. Детерміністичні результати

- Фіксоване випадкове насіння забезпечує однакові результати
- Можливість порівняння різних алгоритмів
- Валідація реалізації

## Конфігурація

### Основні параметри

```csharp
public class ModuleOptions
{
    public int CitiesNumber { get; set; } = 50;        // Кількість міст
    public int PopulationSize { get; set; } = 1000;    // Розмір популяції
    public int Generations { get; set; } = 100;        // Максимальна кількість поколінь
    public double MutationRate { get; set; } = 0.01;   // Ймовірність мутації
    public double CrossoverRate { get; set; } = 0.8;   // Ймовірність схрещування
    public int PointsNumber { get; set; } = 4;         // Кількість паралельних точок
    public int Seed { get; set; } = 42;                // Випадкове насіння
    
    // Нові опції для завантаження з файлу
    public bool LoadFromFile { get; set; } = false;    // Завантажувати з файлу
    public string InputFile { get; set; } = "cities.txt"; // Шлях до файлу
    public bool GenerateRandomCities { get; set; } = true; // Генерувати випадкові як fallback
}
```

### Приклади конфігурації

#### Мала задача (швидке тестування)
```csharp
var options = new ModuleOptions
{
    CitiesNumber = 25,
    PopulationSize = 500,
    Generations = 50,
    LoadFromFile = true,
    InputFile = "Examples/TestData/small_grid_16.txt"
};
```

#### Середня задача (основне тестування)
```csharp
var options = new ModuleOptions
{
    CitiesNumber = 64,
    PopulationSize = 1000,
    Generations = 200,
    LoadFromFile = true,
    InputFile = "Examples/TestData/medium_clustered_60.json"
};
```

#### Велика задача (тестування продуктивності)
```csharp
var options = new ModuleOptions
{
    CitiesNumber = 144,
    PopulationSize = 2000,
    Generations = 500,
    PointsNumber = 8,
    LoadFromFile = true,
    InputFile = "Examples/TestData/large_random_144.txt"
};
```

## Використання

### Завантаження з файлу

```csharp
var options = new ModuleOptions
{
    LoadFromFile = true,
    InputFile = "cities.txt",
    PopulationSize = 1000,
    Generations = 200
};

// Модуль автоматично завантажить міста з файлу
var module = new SequentialMainModule();
module.Run(moduleInfo, channel);
```

### Генерація тестових даних

```csharp
// Генерація міст у формі сітки
var cities = CityLoader.GenerateTestCities(64, 42, TestCityPattern.Grid);

// Збереження у файл
CityLoader.SaveToTextFile(cities, "grid_64.txt");
CityLoader.SaveToJsonFile(cities, "grid_64.json");
```

### Тестування детермінізму

```csharp
// Перевірка однаковості результатів
var cities1 = CityLoader.GenerateTestCities(25, 42, TestCityPattern.Random);
var result1 = RunGeneticAlgorithm(cities1, options);

var cities2 = CityLoader.GenerateTestCities(25, 42, TestCityPattern.Random);
var result2 = RunGeneticAlgorithm(cities2, options);

if (Math.Abs(result1.BestDistance - result2.BestDistance) < 0.01)
{
    Console.WriteLine("✓ Детермінізм забезпечено");
}
```

## Тестові дані

### Готові тестові задачі

Директорія `Examples/TestData/` містить готові тестові задачі:

#### Маленькі задачі
- `small_grid_16.txt/json` - 16 міст у формі сітки 4x4
- `tiny_random_10.txt/json` - 10 випадкових міст
- `tiny_clustered_12.txt/json` - 12 міст у кластерах

#### Середні задачі
- `medium_clustered_50.txt/json` - 50 міст у кластерах
- `medium_random_64.txt/json` - 64 випадкових міста
- `medium_circle_64.txt/json` - 64 міста по колу

#### Великі задачі
- `large_circle_100.txt/json` - 100 міст по колу
- `large_random_144.txt/json` - 144 випадкових міста
- `large_clustered_150.txt/json` - 150 міст у кластерах

### Генерація власних тестових даних

```bash
cd modules/Parcs.Modules.TravelingSalesman/Examples
dotnet run --project GenerateTestData.csproj
```

## Генетичний алгоритм

### Основні оператори

1. **Схрещування (Crossover)** - Order Crossover (OX)
2. **Мутація (Mutation)** - Swap, Inversion, Scramble
3. **Відбір (Selection)** - Турнірний відбір
4. **Елітизм** - збереження найкращої особини

### Стратегія еволюції

- Генераційна заміна популяції
- Елітизм для збереження найкращих рішень
- Раннє зупинення при досягненні збіжності
- Моніторинг історії збіжності

## Паралельна реалізація

### Стратегія паралелізації

1. **Розподіл популяції** - кожна робоча точка отримує підпопуляцію
2. **Незалежна еволюція** - кожна точка виконує GA на своїй підпопуляції
3. **Агрегація результатів** - об'єднання найкращих рішень з усіх точок
4. **Координація** - головний модуль керує процесом

### Переваги паралельної реалізації

- **Масштабованість** - лінійне прискорення з кількістю точок
- **Різноманітність** - різні траєкторії пошуку
- **Відмовостійкість** - продовження роботи при збої окремої точки
- **Гнучкість** - налаштування кількості робочих точок

## Висновки

Нові можливості модуля значно покращують:

1. **Відтворюваність** - детерміністичні результати для наукових експериментів
2. **Гнучкість** - підтримка різних форматів вхідних даних
3. **Тестування** - різноманітні тестові задачі для валідації
4. **Порівняння** - можливість об'єктивного порівняння алгоритмів
5. **Професійність** - відповідність стандартам наукових досліджень

Модуль готовий для серйозних експериментів та порівняння різних підходів до розв'язання TSP з використанням генетичних алгоритмів у розподіленій обчислювальній системі PARCS. 
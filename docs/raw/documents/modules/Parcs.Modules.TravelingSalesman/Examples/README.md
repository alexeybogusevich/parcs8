# Приклади використання TSP модуля

Ця директорія містить приклади використання модуля для розв'язання задачі комівояжера (TSP) з використанням генетичного алгоритму.

## Файли

### Основні приклади

- **`ExampleUsage.cs`** - Основний приклад використання модуля з тестуванням різних сценаріїв
- **`GenerateTestData.cs`** - Скрипт для генерації різноманітних тестових даних

### Тестові дані

Директорія `TestData/` містить готові тестові задачі TSP у двох форматах:

#### Текстові файли (.txt)
Формат: кожен рядок містить "ID X Y"
```
# Коментар
0 0.0 0.0
1 100.0 0.0
2 200.0 0.0
...
```

#### JSON файли (.json)
Формат: масив об'єктів з полями Id, X, Y
```json
[
  {"Id": 0, "X": 0.0, "Y": 0.0},
  {"Id": 1, "X": 100.0, "Y": 0.0},
  {"Id": 2, "X": 200.0, "Y": 0.0}
]
```

## Типи тестових задач

### 1. Стандартні набори

#### Маленькі задачі (швидке тестування)
- **`tiny_random_10`** - 10 випадкових міст
- **`tiny_grid_9`** - 9 міст у формі сітки 3x3
- **`tiny_clustered_12`** - 12 міст у 2 кластерах

#### Середні задачі (основне тестування)
- **`medium_random_64`** - 64 випадкових міста
- **`medium_grid_64`** - 64 міста у формі сітки 8x8
- **`medium_clustered_60`** - 60 міст у 6 кластерах
- **`medium_circle_64`** - 64 міста по колу

#### Великі задачі (тестування продуктивності)
- **`large_random_144`** - 144 випадкових міста
- **`large_grid_144`** - 144 міста у формі сітки 12x12
- **`large_clustered_150`** - 150 міст у 15 кластерах

### 2. Benchmark інстанси

- **`eil51`** - 51 місто (стандартна задача з літератури)
- **`eil76`** - 76 міст
- **`eil101`** - 101 місто
- **`symmetric_50`** - 50 міст з симетричним розташуванням
- **`symmetric_100`** - 100 міст з симетричним розташуванням

### 3. Спеціальні випадки

- **`tight_clusters_80`** - 80 міст у дуже тісних кластерах
- **`sparse_100`** - 100 міст з дуже великими відстанями
- **`linear_50`** - 50 міст вздовж лінії
- **`spiral_75`** - 75 міст по спіралі

## Використання

### Запуск основного прикладу

```bash
cd modules/Parcs.Modules.TravelingSalesman/Examples
dotnet run --project ExampleUsage.csproj
```

### Генерація тестових даних

```bash
cd modules/Parcs.Modules.TravelingSalesman/Examples
dotnet run --project GenerateTestData.csproj
```

### Завантаження міст з файлу

```csharp
var options = new ModuleOptions
{
    LoadFromFile = true,
    InputFile = "Examples/TestData/small_grid_16.txt",
    PopulationSize = 500,
    Generations = 100
};

// Модуль автоматично завантажить міста з файлу
var module = new SequentialMainModule();
module.Run(moduleInfo, channel);
```

## Переваги нових можливостей

### 1. Детермінізм
- Фіксоване випадкове насіння забезпечує однакові результати при повторних запусках
- Можливість порівняння різних алгоритмів на однакових даних
- Валідація реалізації та відладка

### 2. Гнучкість вхідних даних
- Підтримка як текстового, так і JSON формату
- Автоматичне визначення формату файлу
- Fallback на генерацію випадкових міст при помилці завантаження

### 3. Різноманітність тестових задач
- Різні геометричні патерни (сітка, кластери, коло, спіраль)
- Різні розміри задач (від 10 до 150 міст)
- Спеціальні випадки для тестування граничних умов

### 4. Легкість порівняння
- Однакові дані для різних алгоритмів
- Стандартизовані формати виводу
- Можливість аналізу збіжності та продуктивності

## Формати файлів

### Текстові файли
- Простий формат для ручного редагування
- Підтримка коментарів (рядки, що починаються з #)
- Автоматичне ігнорування порожніх рядків
- Обробка помилок з детальними повідомленнями

### JSON файли
- Стандартний формат для програмної обробки
- Структуровані дані з типізацією
- Легка інтеграція з іншими системами
- Підтримка складних структур даних

## Створення власних тестових даних

### Програмна генерація

```csharp
// Генерація випадкових міст
var cities = CityLoader.GenerateTestCities(50, 42, TestCityPattern.Random);

// Генерація міст у формі сітки
var gridCities = CityLoader.GenerateTestCities(64, 42, TestCityPattern.Grid);

// Генерація кластеризованих міст
var clusteredCities = CityLoader.GenerateTestCities(100, 42, TestCityPattern.Clustered);

// Генерація міст по колу
var circleCities = CityLoader.GenerateTestCities(80, 42, TestCityPattern.Circle);
```

### Збереження у файл

```csharp
// Збереження у текстовому форматі
CityLoader.SaveToTextFile(cities, "my_cities.txt");

// Збереження у JSON форматі
CityLoader.SaveToJsonFile(cities, "my_cities.json");
```

## Тестування детермінізму

### Перевірка однаковості результатів

```csharp
// Перший запуск
var cities1 = CityLoader.GenerateTestCities(25, 42, TestCityPattern.Random);
var result1 = RunGeneticAlgorithm(cities1, options);

// Другий запуск з тим самим seed
var cities2 = CityLoader.GenerateTestCities(25, 42, TestCityPattern.Random);
var result2 = RunGeneticAlgorithm(cities2, options);

// Перевірка ідентичності
if (Math.Abs(result1.BestDistance - result2.BestDistance) < 0.01)
{
    Console.WriteLine("✓ Детермінізм забезпечено");
}
else
{
    Console.WriteLine("✗ Проблема з детермінізмом");
}
```

### Порівняння з різними seed

```csharp
var seeds = new[] { 42, 123, 456, 789, 999 };
var results = new List<double>();

foreach (var seed in seeds)
{
    var cities = CityLoader.GenerateTestCities(25, seed, TestCityPattern.Random);
    var result = RunGeneticAlgorithm(cities, options);
    results.Add(result.BestDistance);
}

var avgResult = results.Average();
var stdDev = Math.Sqrt(results.Select(r => Math.Pow(r - avgResult, 2)).Average());
Console.WriteLine($"Середнє: {avgResult:F2}, Стандартне відхилення: {stdDev:F2}");
```

## Інтеграція з PARCS системою

### Послідовний модуль

```csharp
var options = new ModuleOptions
{
    LoadFromFile = true,
    InputFile = "cities.txt",
    PopulationSize = 1000,
    Generations = 200,
    SaveResults = true,
    OutputFile = "sequential_results.json"
};

// Модуль автоматично завантажить міста та збереже результати
```

### Паралельний модуль

```csharp
var options = new ModuleOptions
{
    LoadFromFile = true,
    InputFile = "cities.json",
    PopulationSize = 2000,
    Generations = 500,
    PointsNumber = 8,
    SaveResults = true,
    OutputFile = "parallel_results.json"
};

// Модуль розподілить міста між робочими точками
```

## Висновок

Нові можливості завантаження з файлу та різноманітні тестові дані значно покращують:

1. **Відтворюваність експериментів** - детерміністичні результати
2. **Гнучкість тестування** - різні типи задач та розміри
3. **Легкість порівняння** - стандартизовані формати та дані
4. **Професійність** - відповідність стандартам наукових досліджень

Це дозволяє проводити серйозні експерименти та порівняння різних підходів до розв'язання TSP з використанням генетичних алгоритмів. 
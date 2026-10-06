# План дисертації (PhD, F3 Комп'ютерні науки, КНУ)

> Робочий документ. High-level план узгоджено 2026-10-04; далі ітеруємо по розділах.

## 0. Формальні рамки

- **Обсяг:** ОНП PhD F3 КНУ (2025) — 4,5–7 авторських аркушів основного тексту (≈180–280 тис. знаків ≈ 100–155 с.). Ціль — **105–115 с. основного тексту** (вступ → висновки); анотації, зміст, список джерел, додатки — понад це.
- **Оформлення:** Наказ МОН №40 від 12.01.2017 (https://zakon.rada.gov.ua/laws/show/z0155-17#n88), вимоги КНУ (https://asp.knu.ua/index.php/docturantura/192-vymohy-do-oformlennya-dysertatsiy).
- **Публікації (ПКМУ №44):** ≥ 3 статті у фахових виданнях кат. А/Б або Scopus/WoS.
  1. Bohusevych, Derevianchenko. *Leveraging K8s to implement PARCS.NET* — Вісник КНУ, 2024, т.79 №2 — **опубліковано**.
  2. *Building highly scalable parallel compute systems with PARCS, Kubernetes and KEDA* — Вісник КНУ — **на рецензії**.
  3. Богусевич, Свистунов. *PARCS-Agent: інтерфейс взаємодії ШІ-агентів з ПАРКС-кластерами* — Вісник ВПІ — **подано**.
  4. Резерв: стаття про GPU; MDPI-версія PARCS-Agent (`articles/mcp-parcs`); IPTC.
- **Апробація:** Shevchenko Spring 2025 (Fault-tolerance and reliability in PARCS Kubernetes), CIT 2026 (`articles/cit2026`).
- **Тема:** зараз затверджено «Дослідження, розробка та автоматизація системи ПАРКС на платформі .NET із застосуванням Kubernetes». Узгодити з керівником уточнення через вчену раду.

## 1. Наскрізна ідея

**Відображення моделі керуючого простору (КП) ПАРКС на хмарно-нативну інфраструктуру на трьох рівнях:**

1. **Інфраструктурний** — точка КП = под; створення точки = запит ресурсу → *еластичний КП* (KEDA + Cluster Autoscaler / Karpenter).
2. **Ресурсний** — точка має тип обчислювача (CPU/GPU) → *гетерогенний КП*.
3. **Програмний** — автор програми КП — ШІ-агент (PARCS-Agent).

Теоретична опора для п.3: класична програма ПАРКС = (1) опис структури КП + (2) програма налаштування + (3) локальні АМ (Анісімов, Дерев'янченко). У PARCS-Agent агент пише лише (3), а (1)+(2) задаються викликами інструментів; IPTC зводить (1)+(2) до однієї ітерації агентного циклу → «паралельне програмування природною мовою».

- **Робоча назва:** «Методи та засоби хмарно-нативної реалізації технології ПАРКС: еластичне масштабування, гетерогенні обчислення та агентне програмування».
- **Об'єкт:** процеси організації паралельних асинхронних рекурсивних обчислень у хмарних середовищах.
- **Предмет:** методи і засоби відображення КП ПАРКС на контейнерну інфраструктуру, його автомасштабування, гетерогенного виконання та керування ШІ-агентами.

## 2. Структура (~110 с.)

| Розділ | Зміст | С. | Джерело матеріалу |
|---|---|---|---|
| **Вступ** | актуальність, мета/завдання, об'єкт/предмет, методи, новизна, практичне значення, особистий внесок, апробація, публікації, структура | 7 | — |
| **1. Аналіз стану проблеми** | теорія ПАРКС (Глушков–Анісімов 1980; КП, АМ, рекурсивність; зв'язок з CSP); історія реалізацій (Pascal → PARCS-JAVA → PARCS.NET 2015/2018 → PARCS-WCF 2020 → PARCS-K8s 2024); сучасні засоби (MPI, Spark/Ray/Dask, Kubernetes batch: Volcano/Kueue, serverless); автомасштабування (HPA, CA, KEDA, Karpenter); GPU в K8s; LLM-агенти й HPC (ReAct, CodeAct, MCP, PTC, ParEval). Постановка задачі | 22 | магістерська р.1, вступи статей 1–3 |
| **2. Модель і архітектура хмарно-нативного ПАРКС** | формальне відображення КП → об'єкти K8s; архітектура Host/Daemon/Portal; ізоляція АМ (AssemblyLoadContext); протокол сигналів, TCP-фреймінг, in-process канали; відмовостійкість і резервування | 18 | стаття 1, звіт про резервування |
| **3. Еластичне масштабування і мультихмарне розгортання** | модель «точка = под» (ScaledJob + черга запитів точок); аналітична модель затримки T = T_node + T_pod + T_comp/k + T_comm і поріг доцільності; AKS (Service Bus), GKE (Pub/Sub, Workload Identity, Terraform), AWS EKS (SQS, Karpenter, IRSA); порівняння холодного старту, вартості, квот; експерименти (TSP/GA тощо) | 22 | стаття 2, `docs/gcp-deployment-guide.md`, AWS (нове) |
| **4. Гетерогенні (GPU) обчислення** | тип точки як атрибут КП (`RequiresGpu`); GPU-пули (taints/tolerations, device plugin, scale-to-zero); ILGPU з CPU-fallback; модулі MonteCarloPi, MatMul, Floyd–Warshall, PoW, NBody; CPU vs GPU vs multi-GPU, вартість на одиницю роботи | 16 | гілка `feature/GPU`, `docs/gpu_specification.docx` |
| **5. PARCS-Agent** | MCP-сервер, контракт `IAgentComputation`, шарова модель fan-out/fan-in як підклас КП, побічний канал датасетів, автокорекція (Roslyn); бенчмарк 15 задач (послідовний Python REPL vs PARCS, кілька LLM); IPTC (JitGen + PTC + PARCS MCP), кейс VaR: baseline / PTC / IPTC | 22 | стаття 3, CIT2026, `misc/mcp_eval`, IPTC (нове) |
| **Висновки** | | 4 | |
| Список джерел (~120–150), додатки (маніфести, код, список публікацій, акт впровадження в навчальний процес КНУ) | | понад обсяг | |

Кожен розділ завершується висновками до розділу.

## 3. Наукова новизна (кандидати)

- **Вперше** запропоновано модель еластичного КП, де створення точки ініціює подієве виділення інфраструктурних ресурсів; аналітична модель затримки й межі доцільності.
- **Вперше** запропоновано метод взаємодії ШІ-агентів з ПАРКС-кластером через MCP, за якого агент генерує лише послідовний код АМ, а розподілення забезпечує платформа.
- **Удосконалено** модель КП введенням типізованих (гетерогенних) точок CPU/GPU.
- **Набула подальшого розвитку** .NET-реалізація ПАРКС: хмарно-агностична (AKS / GKE / EKS) з автоматизованим розгортанням (IaC).
- *(опційно)* метод інкрементного програмного виклику інструментів (IPTC) для PARCS-Agent.

## 4. Що треба доробити (трекер)

| # | Задача | Пріоритет | Статус |
|---|---|---|---|
| 1 | GPU-експерименти (потрібна квота ≥2–4 GPU; зараз GKE 1×T4) | критично | ⏳ чекаємо квоту |
| 2 | Повний прогін бенчмарку PARCS-Agent (`misc/mcp_eval`) | критично | ☐ |
| 3 | Кількісна оцінка масштабування: speedup/efficiency 1…N точок, заміри холодного старту пода/вузла | критично | ◐ інструментовано логування затримок (Host: «All N daemons connected … in Xs», Daemon: «picked up Ys after it was published»); експерименти не проведені |
| 4 | AWS EKS: абстракція черги точок, SQS-тригер KEDA, Karpenter, IaC, маніфест | важливо | ✅ код + IaC (2026-10-04): `Parcs.Core/Messaging` (Pub/Sub, Service Bus, SQS), `infra/aws/main.tf` (validate OK), `kube/deployment.aws.yaml`, `kube/aws-karpenter.yaml`, `docs/aws-deployment-guide.md`; **не розгорнуто в AWS** |
| 5 | IPTC: QuickJS-виконавець + JitGen + PTC над PARCS MCP; експеримент VaR | важливо | ☐ |
| 6 | Перевірити/показати рекурсивність у KEDA-моделі (демон створює дочірні точки) | важливо | ✅ виявлено, що була зламана (на демоні не було `IPointCreationService`); виправлено: `CallbackTcpServer` + `QueuePointCreationService` у Core, працюють і на демоні; покрито тестами `tests/Parcs.Core.Tests`; потрібна демонстрація на кластері (модуль із вкладеними точками) |
| 7 | Ресурсно-орієнтоване планування (автовибір CPU/GPU для точки) | розширення | ☐ |
| 8 | Автентифікація, багатокористувацький режим | → «подальші дослідження» | — |

### Побічні виправлення (2026-10-04)

- Azure-шлях був фактично зламаний: код підтримував лише Pub/Sub, а `deployment.azure.yaml` очікував `ServiceBus__*`. Тепер провайдер обирається `PointQueue__Provider`.
- Локальний режим (без черги) у Host падав у `PointCreationService` з `daemonHostUrl = null`; тепер `ModuleInfo` при вимкненій черзі використовує прямий TCP до демонів.
- `infra/gcp/main.tf` був обрізаний (незакритий `output`) і мав два блоки `addons_config` — виправлено, `terraform validate` OK. Демону надано `pubsub.publisher` (для вкладених точок).
- `kube/deployment.azure.yaml` мав 2136 нульових байтів у кінці — прибрано.

## 5. Ризики

- **Особистий внесок в IPTC:** JitGen — розробка А. Свистунова; чітко розмежувати (інтеграція з ПАРКС, методика, експерименти — ваші).
- **Перетин з магістерською (2023):** PARCS-NET-K8 на .NET 7 / AKS — база для розділу 2; новизна — з періоду аспірантури (KEDA, GPU, MCP, мультихмарність).
- **Бібліографія:** у статті 1 посилання Anisimov et al. (2018) має DOI статті 2023 р.; стаття 2 посилається на `github.com/alexeybogusevich/parcs8` (репозиторій — parcs7).

## 6. Ключові джерела

- Anisimov A.V., Derevianchenko O.V., Kuliabko P.P., Fedorus O.M. *PARCS Technology: Concept and Implementations*. Cybernetics and Systems Analysis, 2023, 59, 832–843. https://doi.org/10.1007/s10559-023-00619-6
- Anisimov A.V. et al. *Programming System PARCS*. JCC, 2017, 5(9), 129–139. https://doi.org/10.4236/jcc.2017.59009
- Anisimov A.V., Fedorus O.M. *Development and Prospects of the PARCS-WCF System*. CSA, 2020. https://doi.org/10.1007/s10559-020-00230-z
- Glushkov V.M., Anisimov A.V. *Controlling spaces in asynchronous parallel computations*. Cybernetics, 1980.
- Anthropic. *Advanced tool use (Programmatic Tool Calling)*. https://www.anthropic.com/engineering/advanced-tool-use
- JitGen. https://github.com/AntonSvystunov/jitgen

## Локальні матеріали

- Статті: `C:\Users\obohusevych\Desktop\KNU\PhD\Leveraging K8s to implement PARCS.docx`, `...\Стаття 2\Authors. Scalabale parallel compute systems with K8, PARCS, KEDA.docx`, `...\Стаття 3\Article.docx`
- Магістерська: `C:\Users\obohusevych\Desktop\KNU\Diploma\PARCS.NET\Paper\final\Богусевич_маг.роб.pdf`
- Дослідницька пропозиція: `C:\Users\obohusevych\Desktop\KNU\PhD\Дослідницька_Пропозиція.docx`
- У репозиторії: `articles/`, `docs/wiki/`, `misc/mcp_eval/`, `docs/gpu_specification.docx`

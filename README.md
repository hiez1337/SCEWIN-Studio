# SCEWIN Studio

<p align="center">
  <img src="docs/screenshots/01_dashboard.png" alt="SCEWIN Studio Dashboard" width="900" />
</p>

<p align="center">
  <a href="https://github.com/hiez1337/SCEWIN-Studio/releases"><img src="https://img.shields.io/github/v/release/hiez1337/SCEWIN-Studio?color=60CDFF&label=Release&logo=github" alt="GitHub Release"></a>
  <a href="https://github.com/hiez1337/SCEWIN-Studio/actions"><img src="https://img.shields.io/github/actions/workflow/status/hiez1337/SCEWIN-Studio/release.yml?branch=main&label=Build%20%26%20Release&logo=githubactions" alt="Build Status"></a>
  <img src="https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet" alt=".NET 8.0">
  <img src="https://img.shields.io/badge/Platform-Windows%2010%20%2F%2011%20x64-0078D6?logo=windows" alt="Platform Windows">
  <img src="https://img.shields.io/badge/Architecture-WPF%20%2F%20Fluent%20Design-00C853" alt="UI Architecture">
</p>

**SCEWIN Studio** — современный графический конфигуратор (GUI) на базе **.NET 8** и **Fluent Design (Windows 11 UI)** для низкоуровневого управления, редактирования и сравнительного анализа переменных **UEFI NVRAM** с помощью официальной системной утилиты **AMI SCEWIN (AMISCE)**.

Приложение спроектировано для безопасного и удобного твикинга систем на базе процессоров **AMD Ryzen (AM4 / AM5)** и платформ Intel, поддерживая работу с полными дампами BIOS, содержащими **3,000–4,000+ параметров**.

---

## 📸 Скриншоты интерфейса / UI Showcase

### 1. 🖥️ Панель управления (Dashboard)
Статус подключения к AMI Aptio V, определение материнской платы, версия BIOS, контрольная сумма HII CRC32 и быстрые карточки рекомендаций.
<p align="center">
  <img src="docs/screenshots/01_dashboard.png" alt="Dashboard" width="850" />
</p>

### 2. ⚡ Память и Тайминги (Multi-Tier Architecture)
Трёхуровневая классификация параметров с разделением на **AMD CBS** (аппаратный уровень AGESA), **MSI Click BIOS OC Engine** (оверлей OEM) и служебные дубликаты **AMD PBS**.
<p align="center">
  <img src="docs/screenshots/02_memory_tuning.png" alt="Memory & Timings" width="850" />
</p>

### 3. 🔌 PCIe, ASPM и Бифуркация линий
Управление состояниями энергосбережения шины PCI Express (L0s, L1, L1 Substates), ReBAR (Resizable BAR) и конфигурацией разделения слотов PCIe.
<p align="center">
  <img src="docs/screenshots/03_pcie_power.png" alt="PCIe Power & ASPM" width="850" />
</p>

### 4. 🚀 Процессор, PBO и C-States
Тонкая настройка Precision Boost Overdrive (PBO), Curve Optimizer для каждого ядра, лимитов по току и мощности (PPT, TDC, EDC), состояний энергосбережения (Global C-States, DF C-States, CPPC).
<p align="center">
  <img src="docs/screenshots/04_cpu_power.png" alt="CPU Power & PBO" width="850" />
</p>

### 5. 📋 Каталог всех токенов (Raw Tokens)
Виртуализированный каталог на 4,000+ параметров NVRAM с мгновенным debounced-поиском, фильтрацией по категориям и чип-фильтрами.
<p align="center">
  <img src="docs/screenshots/05_raw_tokens.png" alt="Raw Tokens Catalog" width="850" />
</p>

### 6. 🔍 Двустороннее сравнение дампов BIOS (Dual-Dump Compare)
Сопоставление двух независимых дампов (например, вашего профиля и профиля друга) с автоматическим обнаружением различий, фильтрацией по категориям и экспортом отчётов.
<p align="center">
  <img src="docs/screenshots/06_dual_dump_comparison.png" alt="Dual Dump Comparison" width="850" />
</p>

### 7. ⚙️ Настройки и автоматический загрузчик SCEWIN
Встроенный поиск локальных утилит SCEWIN, проверка драйверов ядра AMI (`amifldrv64.sys`) и загрузчик последней протестированной версии утилиты в один клик.
<p align="center">
  <img src="docs/screenshots/07_settings.png" alt="Settings & Tools" width="850" />
</p>

---

## 🌟 Ключевые возможности

- 🚀 **Оптимизация под 3,000–4,000+ параметров**:
  - Полная виртуализация всех списков (`VirtualizingPanel.VirtualizationMode="Recycling"` с `CacheLengthUnit="Item"`).
  - Асинхронный парсинг дампа в пуле потоков без зависания UI.
  - Кэширование тяжёлых строковых свойств в `ScewinToken` и фоновый debounced-поиск.
  - Нулевые просадки FPS при прокрутке и мгновенное переключение вкладок.
- 🛡️ **Безопасное применение изменений**:
  - Автоматическое создание бэкапа перед записью (`nvram_backup_<timestamp>.txt`).
  - Просмотр предпросмотра изменений в реальном времени (Diff Viewer) в правой боковой панели.
  - Корректная генерация diff-скрипта с оригинальным `HIICrc32`.
- 🌐 **Полная локализация (Русский / English)**:
  - 100% покрытие строк интерфейса с возможностью переключения на лету кнопкой в шапке.
- 📦 **Встроенные драйверы AMI**:
  - Автоматическая распаковка официальных утилит и драйверов ядра `amifldrv64.sys` / `amigendrv64.sys`.

---

## 📦 Скачать готовый релиз (GitHub Releases)

Свежие готовые сборки собираются автоматически в **[GitHub Releases](https://github.com/hiez1337/SCEWIN-Studio/releases)**:

| Файл | Описание | Кому подходит |
|---|---|---|
| **`SCEWIN_Studio.exe`** | Одиночный переносимый файл (Single-File, всё включено) | **Рекомендуется** всем пользователям (не требует установки .NET) |
| **`SCEWIN_Studio_win-x64_SelfContained.zip`** | Полный переносимый архив с зависимостями | Для распаковки в отдельную папку |
| **`SCEWIN_Studio_win-x64_FrameworkDependent.zip`** | Компактная версия (~5 МБ) | Если уже установлен [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) |

---

## 🛠️ Сборка из исходников

### Требования:
- Windows 10/11 x64 (версия 19041+)
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### Клонирование и сборка:
```powershell
git clone https://github.com/hiez1337/SCEWIN-Studio.git
cd SCEWIN-Studio

# Сборка Release
dotnet build src/SCEWIN_Studio/SCEWIN_Studio.csproj -c Release

# Запуск тестов
dotnet test SCEWIN_Studio.sln -c Release
```

---

## 🧪 Автоматизированные тесты

Проект покрыт набором из **141 xUnit теста**:
- Тестирование парсера на реальных дампах BIOS AMD AM4 (B550) и AM5 (X670/B650).
- Нагрузочные тесты на дампы размером 3,500–4,000 токенов (парсинг < 350 мс).
- Валидация работы `ObservableRangeCollection` и многопоточного поиска.
- Тесты алгоритма двустороннего сопоставления (`DumpComparer`).
- Проверка корректности ресурсов локализации и упаковки.

---

## ⚠️ Предупреждение / Disclaimer

*SCEWIN Studio работает напрямую с переменными NVRAM вашей материнской платы через драйвер ядра AMI. Неправильное изменение некоторых параметров (например, критических напряжений или несовместимых частот памяти) может потребовать сброса BIOS (Clear CMOS). Всегда сохраняйте резервные копии.*

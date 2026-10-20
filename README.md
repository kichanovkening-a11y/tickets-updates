# Билеты

Переносная программа Windows x64: залы, спектакли, расценовки, показы, выдача и возвраты билетов.

Готовые сборки — во вкладке Releases. Для первого запуска нужен `Tickets-Windows-x64.zip`. Дальнейшие обновления: кнопка **Обновление** в программе.

Рабочая база `Data/tickets.db` и резервные копии не включаются в пакеты обновления. Тестовая база создаётся из `data/seed.snapshot.json.gz`; старые выдачи не перенесены.

Сборка: .NET SDK 8.0.408, WPF, self-contained Windows x64, runtime 8.0.32.

```powershell
python scripts/restore_seed.py
dotnet run --project tests/Tickets.Tests -- data/seed.db
dotnet publish src/Tickets.App -c Release -r win-x64 --self-contained true -p:RuntimeFrameworkVersion=8.0.32 -o release/Tickets
```

GitHub Actions проверяет учёт, собирает приложение, запускает окно на Windows и публикует релиз. Сначала меняется `version.json`, затем исходники; публикация запускается при записи в main.

Печать, накладные и редактор печатных макетов — следующие этапы.

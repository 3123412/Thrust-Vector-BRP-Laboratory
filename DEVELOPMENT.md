# Разработка

Вектор Тяги: Лаборатория BRP. Unity 6000.6.7f1, Windows и OpenXR.

## Состояние

Реализованы 50 модулей, 20 миссий, свободная сборка, ступени, орбиты, возвращение капсулы, спасение и стыковка. Ангар, интерфейс и ракета сохранены в Laboratory.unity. Spaceport.unity содержит редактируемое окружение космодрома.

Переработка 30 октября: новый ангар и стартовый комплекс из Blender, объединение мешей модулей, пространственная компоновка VR-панелей, исправления масштаба Земли и ландшафта, число Маха и волновое сопротивление. Проверки: 101 успешно, ошибок нет. Подробности — Docs/VERIFICATION.md.

Текущая доставка: запись нового ролика и финальная Windows-сборка. После запуска EXE обновить архивы и GitHub Release. Не считать этот этап завершённым до проверки файлов публикации.

## Команды

Unity Pipeline: 7800. Указывать --project-path 'C:/Developing/My project' --caller plugin --skill unity-cli. Параметры Pipeline идут после --. Для длительных операций передавать --timeout 180 или 600 после разделителя.

После изменения C# вызвать AssetDatabase.Refresh и проверить console status: compilationFailed=false, compiling=false. В Play Mode не менять и не импортировать файлы Assets.

Сцена: BRP.Editor.StudioSceneBuilder.Build(). Проверки: VerificationSuite.Run(), FlightScenarioVerification.Run(). Кампания: CampaignAcceptance в Play Mode. Сборка: ContentBuilder.BuildWindows().

Blender Bridge: 8765. Генератор окружения — Tools/build_world_v2.py. Исходники — SourceArt. При тайм-ауте MCP сначала проверить завершение экспорта; не запускать второй генератор одновременно.

Запись: Tools/record_showcase.ps1 в PowerShell 7 при запущенной игре в редакторе. Архивы: Tools/package_delivery.py после коммита исходников. Публикация: Tools/publish_release.py после проверки сборки и отправки коммитов.

Git: main, https://github.com/3123412/Thrust-Vector-BRP-Laboratory.git. Сообщения коммитов с маленькой буквы на русском.
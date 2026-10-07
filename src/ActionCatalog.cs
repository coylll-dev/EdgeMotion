using System;

namespace EdgeMotion
{
    public sealed class ActionOption
    {
        public DesktopAction Value { get; private set; }
        public string Title { get; private set; }
        public string Detail { get; private set; }
        public string Shortcut { get; private set; }
        public ActionOption(DesktopAction value,string title,string detail,string shortcut)
        { Value=value; Title=title; Detail=detail; Shortcut=shortcut; }
        public override string ToString() { return Title; }
    }
    public static class ActionCatalog
    {
        public static readonly ActionOption[] All = {
            new ActionOption(DesktopAction.Desktop,"Показать рабочий стол","Свернуть окна. Повторный жест вернёт их.","Win + D"),
            new ActionOption(DesktopAction.TaskView,"Все открытые окна","Обзор окон и виртуальных рабочих столов.","Win + Tab"),
            new ActionOption(DesktopAction.Back,"Назад","Предыдущая страница в браузере или папка в Проводнике.","Alt + ←"),
            new ActionOption(DesktopAction.VoiceTyping,"Голосовой ввод","Диктовка текста в активное поле ввода.","Win + H"),
            new ActionOption(DesktopAction.QuickSettings,"Быстрые настройки","Wi-Fi, громкость и другие настройки Windows 11.","Win + A"),
            new ActionOption(DesktopAction.PreviousApp,"Предыдущее окно","Переключиться на последнее использованное окно.","Alt + Tab"),
            new ActionOption(DesktopAction.NextApp,"Переключить окно обратно","Обойти список окон в обратном направлении.","Alt + Shift + Tab"),
            new ActionOption(DesktopAction.Start,"Открыть Пуск","Меню приложений Windows.","Win"),
            new ActionOption(DesktopAction.Search,"Поиск Windows","Найти приложение, файл или настройку.","Win + S"),
            new ActionOption(DesktopAction.None,"Не выполнять действие","Жест распознаётся, но не посылает клавиши.","—")
        };
        public static ActionOption Find(DesktopAction value)
        { foreach (var item in All) if (item.Value==value) return item; return All[All.Length-1]; }
        public static string GestureName(Gesture gesture)
        {
            switch (gesture)
            {
                case Gesture.Home: return "Домой"; case Gesture.Recent: return "Недавние";
                case Gesture.Back: return "Назад"; case Gesture.Voice: return "Голос";
                case Gesture.QuickSettings: return "Шторка"; case Gesture.PreviousApp: return "Свайп влево";
                case Gesture.NextApp: return "Свайп вправо"; default: return "Жест";
            }
        }
    }
}

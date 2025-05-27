using System.Collections.Generic;

namespace MdSearch_1._0
{
    public static class FieldTranslationHelper
    {
        public static readonly Dictionary<string, string> Translations = new Dictionary<string, string>
        {
            { "Encoding", "Кодировка" },
            { "PageCount", "Количество страниц" },
            { "LineCount", "Количество строк" },
            { "Language", "Язык документа" },
            { "Version", "Версия документа" },
            { "Creator", "Создатель" },
            { "ColumnCount", "Количество столбцов" },
            { "ImageCount", "Количество изображений" },
            { "TableCount", "Количество таблиц" },
            { "SymbolCountWithSpaces", "Количество символов (с пробелами)" },
            { "SymbolCountWithoutSpaces", "Количество символов (без пробелов)" },
            { "WordCount", "Количество слов" },
            { "Resolution", "Разрешение" },
            { "ColorDepth", "Глубина цвета" },
            { "Orientation", "Ориентация" },
            { "CompressionLevel", "Уровень сжатия" },
            { "ColorProfile", "Цветовой профиль" },
            { "CameraModel", "Модель камеры" },
            { "Geolocation", "Геолокация" },
            { "PreviewImage", "Превью" },
            { "ScalingLevel", "Уровень масштабирования" },
            { "Duration", "Длительность" },
            { "SampleRate", "Частота дискретизации" },
            { "ChannelCount", "Количество каналов" },
            { "AudioBitrate", "Битрейт аудио" },
            { "TrackTitle", "Название трека" },
            { "Artist", "Исполнитель" },
            { "Album", "Альбом" },
            { "ReleaseYear", "Год выпуска" },
            { "Genre", "Жанр" },
            { "VideoCodec", "Видеокодек" },
            { "AudioCodec", "Аудиокодек" },
            { "FormulaCount", "Количество формул" },
            { "FrameRate", "Частота кадров" },
            { "AudioTrack", "Аудиодорожка" },
            { "Description", "Описание" },
            { "FileFormat", "Формат файла" },
            { "UserName", "Имя пользователя" },
            { "DataTransferRate", "Скорость передачи данных" },
            { "TotalBitrate", "Общий битрейт" },
            { "FileName", "Имя файла" },
            { "UploadDate", "Дата загрузки" },
            { "CreationDate", "Дата создания" },
            { "ModificationDate", "Дата изменения" },
            { "FilePath", "Путь к файлу" },
            { "FileSize", "Размер файла" }
        };

        public static string Translate(string changeType)
        {
            const string prefix = "Изменение поля: ";
            if (changeType.StartsWith(prefix))
            {
                string fieldName = changeType.Substring(prefix.Length);
                return Translations.TryGetValue(fieldName, out var translated)
                    ? $"Изменение поля: {translated}"
                    : changeType;
            }
            return changeType;
        }
    }
}
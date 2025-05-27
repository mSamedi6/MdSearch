using System;
using System.Globalization;
using System.Windows.Data;

public class MetadataModel
{
    public int FileID { get; set; }
    public string FileName { get; set; } = "Нет данных";
    public string FileFormat { get; set; } = "Нет данных";
    public string UserName { get; set; } = "Нет данных";
    public string Creator { get; set; } = "Нет данных";
    public string FilePath { get; set; } = "Нет данных";
    public string PageCount { get; set; } = "Нет данных";
    public string LineCount { get; set; } = "Нет данных";
    public string ColumnCount { get; set; } = "Нет данных";
    public string SymbolCountWithSpaces { get; set; } = "Нет данных";
    public string SymbolCountWithoutSpaces { get; set; } = "Нет данных";
    public string Resolution { get; set; } = "Нет данных";
    public string ColorDepth { get; set; } = "Нет данных";
    public string WordCount { get; set; } = "Нет данных";
    public string CompressionLevel { get; set; } = "Нет данных";
    public string Duration { get; set; } = "Нет данных";
    public string SampleRate { get; set; } = "Нет данных";
    public string Language { get; set; } = "Нет данных";
    public string Version { get; set; } = "Нет данных";
    public string Orientation { get; set; } = "Нет данных";
    public string ChannelCount { get; set; } = "Нет данных";
    public string FrameRate { get; set; } = "Нет данных";
    public string VideoCodec { get; set; } = "Нет данных";
    public string AudioCodec { get; set; } = "Нет данных";
    public long FileSize { get; set; }
    public string FormattedFileSize => FileUtils.FormatFileSize(FileSize, showBytes: true);
    public DateTime UploadDate { get; set; }
    public DateTime CreationDate { get; set; }
    public int AudioTrack { get; set; }
    public int ReleaseYear { get; set; } 
    public int DataTransferRate { get; set; }
    public string Genre { get; set; } = "Нет данных";
    public DateTime ModificationDate { get; set; }
    public int AudioBitrate { get; set; }
    public int VideoBitrate { get; set; }
    public string VideoBitrateString => VideoBitrate == 0 ? "Нет данных" : $"{VideoBitrate}";
    public string Description { get; set; } = "Нет данных";
    public string TrackTitle { get; set; } = "Нет данных";
    public string Artist { get; set; } = "Нет данных";
    public string Album { get; set; } = "Нет данных";
    public string ColorProfile { get; set; } = "Нет данных";
    public string ScalingLevel { get; set; } = "Нет данных";
    public string CameraModel { get; set; } = "Нет данных";
    public string Geolocation { get; set; } = "Нет данных";
    public int FormulaCount { get; set; }
    public int ImageCount { get; set; }
    public string ImageCountString => ImageCount == 0 ? "Нет данных" : $"{ImageCount}";
    public int TableCount { get; set; }
    public string Encoding { get; set; } = "Нет данных";
    public string PreviewImage { get; set; } = "Нет данных";
    public string CompressionType { get; set; }
    public int TotalBitrate { get; set; }
    public string SHA256 { get; internal set; }
    public string MimeType { get; internal set; }
    public bool IsHidden { get; internal set; }
}

public static class FileUtils
{
    public static string FormatFileSize(long bytes, bool showBytes = true)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        if (bytes == 0) return "0 B";

        int order = (int)(Math.Log(bytes) / Math.Log(1024));
        if (order >= sizes.Length) order = sizes.Length - 1;

        double num = Math.Round(bytes / Math.Pow(1024, order), 2);

        // Если не нужно показывать байты, возвращается отформатированный размер
        if (!showBytes)
            return $"{num:0.##} {sizes[order]}";

        // Иначе добавляется исходное значение
        return $"{num:0.##} {sizes[order]} ({bytes} байт)";
    }
}
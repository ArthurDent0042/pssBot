﻿namespace pssBot.Models
{
    public class Announce
    {
        public string Id { get; set; } = string.Empty;
        public string? Category { get; set; } = string.Empty;
        public string? Type { get; set; } = string.Empty;
        public string? Resolution { get; set; } = string.Empty;
        public string? Name { get; set; } = string.Empty;
        public string? Uploader { get; set; } = string.Empty;
        public string? Url { get; set; } = string.Empty;
        public string? Size { get; set; } = string.Empty;
        public string? FreeLeech { get; set; } = string.Empty;
        public string? DoubleUpload { get; set; } = string.Empty;
    }
}